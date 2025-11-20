using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Chest: A single chest instance that displays items and handles selection.
/// Implements Vampire Survivors-style slot machine animation.
/// </summary>
public class Chest : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private float openDuration = 1f;
    [SerializeField] private float slotSpinDuration = 0.5f;
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float bounceHeight = 0.5f;

    [Header("Visuals")]
    [SerializeField] private GameObject chestLidGO;
    [SerializeField] private List<Transform> slotTransforms = new List<Transform>();
    [SerializeField] private List<SpriteRenderer> slotRenderers = new List<SpriteRenderer>();

    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;

    private List<PermanentItemData> chestItems;
    private ChestSystem chestSystem;
    private bool isOpening = false;
    private Vector3 startPosition;
    private PermanentItemData selectedItem;

    // Events
    public System.Action<PermanentItemData> OnItemSelected;

    private void Start()
    {
        startPosition = transform.position;
        
        // Auto-find components if not assigned
        if (chestLidGO == null)
            chestLidGO = transform.Find("Lid")?.gameObject;
        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>();
    }

    /// <summary>
    /// Initialize this chest with items
    /// </summary>
    public void Initialize(List<PermanentItemData> items, ChestSystem system)
    {
        chestItems = items;
        chestSystem = system;

        // Update visuals to show slot count
        UpdateSlotDisplay();

        // Start opening animation after delay
        Invoke(nameof(StartOpeningAnimation), 0.5f);
    }

    /// <summary>
    /// Display the chest slots
    /// </summary>
    private void UpdateSlotDisplay()
    {
        for (int i = 0; i < slotTransforms.Count; i++)
        {
            if (i < chestItems.Count)
            {
                // Show slot with item
                slotTransforms[i].gameObject.SetActive(true);
                if (slotRenderers[i] != null && chestItems[i].icon != null)
                {
                    slotRenderers[i].sprite = chestItems[i].icon;
                    slotRenderers[i].color = chestItems[i].GetRarityColor();
                }
            }
            else
            {
                // Hide empty slots
                slotTransforms[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Start the chest opening sequence
    /// </summary>
    private void StartOpeningAnimation()
    {
        if (isOpening) return;
        isOpening = true;

        StartCoroutine(OpenChestSequence());
    }

    /// <summary>
    /// Chest opening animation sequence (Vampire Survivors style)
    /// </summary>
    private IEnumerator OpenChestSequence()
    {
        // Phase 1: Lid opens
        yield return StartCoroutine(OpenLid());

        // Phase 2: Slots spin/animate
        yield return StartCoroutine(SpinSlots());

        // Phase 3: Wait for player input (or auto-select)
        yield return StartCoroutine(WaitForSelection());

        // Phase 4: Close chest and destroy
        yield return StartCoroutine(CloseChest());
    }

    private IEnumerator OpenLid()
    {
        float elapsed = 0f;
        Quaternion startRotation = chestLidGO.transform.localRotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(90f, 0f, 0f); // Rotate to open

        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            float t = openCurve.Evaluate(elapsed / openDuration);
            
            chestLidGO.transform.localRotation = Quaternion.Lerp(startRotation, endRotation, t);
            
            // Bounce
            transform.position = startPosition + Vector3.up * Mathf.Sin(t * Mathf.PI) * bounceHeight;

            yield return null;
        }

        chestLidGO.transform.localRotation = endRotation;
    }

    private IEnumerator SpinSlots()
    {
        float elapsed = 0f;

        while (elapsed < slotSpinDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slotSpinDuration;

            // Spin each slot
            for (int i = 0; i < slotTransforms.Count; i++)
            {
                slotTransforms[i].Rotate(Vector3.forward, 360f * Time.deltaTime / slotSpinDuration);
                
                // Scale animation
                float scale = Mathf.Lerp(0.5f, 1f, t);
                slotTransforms[i].localScale = Vector3.one * scale;
            }

            yield return null;
        }

        // Reset rotations
        foreach (var slot in slotTransforms)
        {
            slot.rotation = Quaternion.identity;
            slot.localScale = Vector3.one;
        }
    }

    private IEnumerator WaitForSelection()
    {
        // Wait for player to click or select an item
        // For now, auto-select random item after delay
        yield return new WaitForSeconds(1f);

        // Auto-select random item
        int selectedIndex = Random.Range(0, chestItems.Count);
        SelectItem(selectedIndex);
    }

    /// <summary>
    /// Select an item from the chest
    /// </summary>
    public void SelectItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= chestItems.Count) return;

        selectedItem = chestItems[slotIndex];
        OnItemSelected?.Invoke(selectedItem);

        // Notify chest system
        if (chestSystem != null)
        {
            chestSystem.OnChestRewardSelected(selectedItem);
        }

        Debug.Log($"Selected item: {selectedItem.itemName}");
    }

    private IEnumerator CloseChest()
    {
        // Fade out
        float elapsed = 0f;
        float fadeDuration = 0.5f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            }
            yield return null;
        }

        // Destroy chest
        Destroy(gameObject);
    }

    /// <summary>
    /// Get the items in this chest
    /// </summary>
    public List<PermanentItemData> GetChestItems() => new List<PermanentItemData>(chestItems);
}
