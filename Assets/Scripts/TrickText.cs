using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Controls the animation, movement, scaling, and destruction of a single trick text pop-up.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TrickText : MonoBehaviour
{
    private TMP_Text textComponent;
    
    [Header("Animation Settings")]
    public float lifetime = 1.5f;
    public float floatSpeed = 1.5f;
    public float scalePunch = 1.2f;
    public float scalePunchDuration = 0.2f;
    public float fadeDelay = 1.0f;

    private float timer;
    private Vector3 originalScale;

    void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
        originalScale = transform.localScale;
    }

    /// <summary>
    /// Initializes the text and starts the animation routine.
    /// </summary>
    /// <param name="trickName">The name of the trick (e.g., "Kickflip")</param>
    /// <param name="score">The score added (e.g., 200)</param>
    /// <param name="comboMultiplier">The current combo multiplier.</param>
    public void Setup(string trickName, int score, float comboMultiplier)
    {
        textComponent.text = $"{trickName}\n<size=120%>+ {score}</size>";
        
        // Use color and size based on combo/score magnitude
        if (comboMultiplier >= 2.0f)
        {
            textComponent.color = Color.yellow; // High Combo
            textComponent.text += $" <size=70%>(x{comboMultiplier:F1})</size>";
        }
        else if (score >= 400)
        {
            textComponent.color = Color.cyan; // High Score Trick
        }
        else if (trickName == "Grind")
        {
            textComponent.color = Color.green; // Continuous Action
        }
        else
        {
            textComponent.color = Color.white; // Standard
        }
        
        StartCoroutine(AnimateRoutine());
    }

    private IEnumerator AnimateRoutine()
    {
        // 1. Initial Punch Scale
        Vector3 punchScaleTarget = originalScale * scalePunch;
        float elapsed = 0f;
        while (elapsed < scalePunchDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scalePunchDuration;
            transform.localScale = Vector3.Lerp(originalScale, punchScaleTarget, t);
            yield return null;
        }
        transform.localScale = punchScaleTarget;
        
        // 2. Return to normal scale
        elapsed = 0f;
        while (elapsed < scalePunchDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scalePunchDuration;
            transform.localScale = Vector3.Lerp(punchScaleTarget, originalScale, t);
            yield return null;
        }
        transform.localScale = originalScale;

        // 3. Float and Hold
        timer = 0f;
        while (timer < fadeDelay)
        {
            timer += Time.deltaTime;
            transform.position += Vector3.up * floatSpeed * Time.deltaTime;
            yield return null;
        }
        
        // 4. Fade Out
        float fadeDuration = lifetime - fadeDelay;
        Color startColor = textComponent.color;
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            textComponent.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            transform.position += Vector3.up * floatSpeed * Time.deltaTime; // Continue floating
            yield return null;
        }

        // 5. Destroy
        Destroy(gameObject);
    }
}