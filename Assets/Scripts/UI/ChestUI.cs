using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ChestUI : MonoBehaviour
{
    [Header("References")]
    public GameObject rootPanel;
    public Image chestImage;          // <- drag the CLOSED sprite here
    public Sprite chestOpenSprite;    // <- drag the OPEN sprite here
    public Image itemIcon;
    public TMP_Text itemNameText;
    public TMP_Text itemInfoText;
    public Button continueButton;
    public Transform itemIconParent;  // empty GO that holds itemIcon (used as particle spawn point)

    [Header("Juice - Timing")]
    public float chestShakeTime = 0.35f;
    public float chestOpenTime = 0.25f;
    public float itemRevealDelay = 0.15f;
    public float itemBounceTime = 0.5f;
    public float glowFadeTime = 1.2f;

    [Header("Juice - FX")]
    public ParticleSystem burstParticles; // assign a simple circular burst
    public Image glowImage;               // white radial glow behind item (optional)
    public AnimationCurve punchCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Idle Animation Settings")]
    public float floatSpeed = 2f;
    public float floatHeight = 15f;
    public float glowRotationSpeed = 30f;
    public float glowPulseSpeed = 3f;
    public float minParticleBurstInterval = 2f;
    public float maxParticleBurstInterval = 4f;

    private bool isBusy = false;

    void Start()
    {
        rootPanel.SetActive(false);
        continueButton.onClick.AddListener(OnContinue);
        if (glowImage) glowImage.color = Color.clear;
    }

    void Update()
    {
        // Allow keyboard/touch input to continue (Space, Enter, or any click)
        if (rootPanel.activeSelf && continueButton.interactable)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                OnContinue();
            }
        }
    }

    public void ShowChest()
    {
        Debug.Log(">>> ShowChestReward CALLED");
        if (isBusy) return;
        
        // Ensure the ChestUI GameObject itself is active for coroutines
        gameObject.SetActive(true);
        transform.SetAsLastSibling(); // ensure on top of other UI
        
        rootPanel.SetActive(true);
        continueButton.interactable = false;
        itemIcon.gameObject.SetActive(false);
        if (glowImage) glowImage.color = Color.clear;

        // Pause AFTER UI is active
        PauseManager.Instance?.Pause();

        // kick off the whole sequence
        StartCoroutine(WholeSequence());
    }

    IEnumerator WholeSequence()
    {
        isBusy = true;

        // 1. Chest shakes while "unlocking"
        yield return StartCoroutine(ShakeChest());

        // 2. Swap sprite to open + quick pop
        chestImage.sprite = chestOpenSprite;
        yield return StartCoroutine(PunchScale(chestImage.transform, 1.15f, chestOpenTime));

        // 3. Wait a tiny beat, then reveal item with particles + glow
        yield return new WaitForSecondsRealtime(itemRevealDelay);
        ItemSystem items = FindObjectOfType<ItemSystem>();
        if (items != null && items.GrantRandomItem(out ItemSystem.ItemDef def))
        {
            itemIcon.sprite = def.icon;
            itemNameText.text = def.displayName;
            itemInfoText.text = def.info;
            itemIcon.gameObject.SetActive(true);
            if (burstParticles) burstParticles.Play();
            StartCoroutine(FadeInGlow());
            yield return StartCoroutine(PunchScale(itemIcon.transform, 1.3f, itemBounceTime));
            
            // Start endless animations
            StartCoroutine(IdleFloatItem());
            StartCoroutine(IdleRotateGlow());
            StartCoroutine(IdlePulseGlow());
            StartCoroutine(PeriodicParticleBurst());
            FindObjectOfType<ItemUI>()?.RefreshHUD();
        }

        continueButton.interactable = true;
        isBusy = false;
    }

    IEnumerator ShakeChest()
    {
        Vector3 basePos = chestImage.transform.localPosition;
        float elapsed = 0;
        while (elapsed < chestShakeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float strength = Mathf.Lerp(4f, 0, elapsed / chestShakeTime);
            Vector3 offset = Random.insideUnitCircle * strength;
            chestImage.transform.localPosition = basePos + offset;
            yield return null;
        }
        chestImage.transform.localPosition = basePos;
    }

    IEnumerator PunchScale(Transform target, float peak, float duration)
    {
        Vector3 original = target.localScale;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float scale = 1f + punchCurve.Evaluate(t) * (peak - 1f);
            target.localScale = original * scale;
            yield return null;
        }
        target.localScale = original;
    }

    IEnumerator FadeInGlow()
    {
        if (!glowImage) yield break;
        Color c = glowImage.color;
        for (float t = 0; t < glowFadeTime; t += Time.unscaledDeltaTime)
        {
            c.a = Mathf.Lerp(0, 0.85f, t / glowFadeTime);
            glowImage.color = c;
            yield return null;
        }
        c.a = 0.85f;
        glowImage.color = c;
    }

    // ==================== ENDLESS IDLE ANIMATIONS ====================
    
    IEnumerator IdleFloatItem()
    {
        if (itemIcon == null) yield break;
        
        Vector3 basePos = itemIcon.transform.localPosition;
        float elapsed = 0f;
        
        while (itemIcon.gameObject.activeSelf && rootPanel.activeSelf)
        {
            elapsed += Time.unscaledDeltaTime;
            
            // Gentle float up and down
            float yOffset = Mathf.Sin(elapsed * floatSpeed) * floatHeight;
            itemIcon.transform.localPosition = basePos + Vector3.up * yOffset;
            
            yield return null;
        }
        
        // Reset position when done
        itemIcon.transform.localPosition = basePos;
    }
    
    IEnumerator IdleRotateGlow()
    {
        if (!glowImage) yield break;
        
        float elapsed = 0f;
        
        while (glowImage.gameObject.activeSelf && rootPanel.activeSelf)
        {
            elapsed += Time.unscaledDeltaTime;
            
            // Slow rotation
            glowImage.transform.localRotation = Quaternion.Euler(0, 0, elapsed * glowRotationSpeed);
            
            yield return null;
        }
    }
    
    IEnumerator IdlePulseGlow()
    {
        if (!glowImage) yield break;
        
        float elapsed = 0f;
        
        while (glowImage.gameObject.activeSelf && rootPanel.activeSelf)
        {
            elapsed += Time.unscaledDeltaTime;
            
            // Pulsing alpha between 0.6 and 1.0
            float alpha = Mathf.Lerp(0.6f, 1.0f, (Mathf.Sin(elapsed * glowPulseSpeed) + 1f) / 2f);
            Color c = glowImage.color;
            c.a = alpha;
            glowImage.color = c;
            
            yield return null;
        }
    }
    
    IEnumerator PeriodicParticleBurst()
    {
        if (!burstParticles) yield break;
        
        // Wait a bit before first burst
        yield return new WaitForSecondsRealtime(1.5f);
        
        while (itemIcon.gameObject.activeSelf && rootPanel.activeSelf)
        {
            // Play particle burst
            burstParticles.Play();
            
            // Wait random interval before next burst
            float waitTime = Random.Range(minParticleBurstInterval, maxParticleBurstInterval);
            yield return new WaitForSecondsRealtime(waitTime);
        }
    }

    void OnContinue()
    {
        // Stop all animations by disabling the panel
        rootPanel.SetActive(false);
        PauseManager.Instance?.Resume();
    }
}