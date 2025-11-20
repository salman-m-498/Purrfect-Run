using UnityEngine;
using TMPro;
using System.Collections;

public class ComboTextSpawner : MonoBehaviour
{
    [Header("Text Settings")]
    public GameObject textPrefab;  // Assign a TextMeshPro prefab in inspector
    public float displayTime = 1.5f;
    public float floatSpeed = 2f;
    public float fadeSpeed = 1f;
    
    [Header("Animation Settings")]
    public float scaleTime = 0.2f;
    public float maxScale = 1.5f;
    public Color normalColor = Color.white;
    public Color comboColor = Color.yellow;
    public Color multiFlipColor = new Color(1f, 0.5f, 0f); // Orange

    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
    }

    public void SpawnTrickText(string trickName, Vector3 position, int multiplier = 1, float comboMultiplier = 1f)
    {
        if (textPrefab == null)
        {
            Debug.LogError("Text prefab not assigned to ComboTextSpawner!");
            return;
        }

        // Convert world position to screen position
        Vector3 screenPos = mainCam.WorldToScreenPoint(position);
        GameObject textObj = Instantiate(textPrefab, Vector3.zero, Quaternion.identity);
        textObj.transform.SetParent(transform, false);

        // Set the position in screen space
        RectTransform rectTransform = textObj.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // Adjust position to be relative to canvas center
            rectTransform.anchoredPosition = new Vector2(
                screenPos.x - Screen.width/2f, 
                screenPos.y - Screen.height/2f
            );
            
            // Reset scale to prevent any inherited scaling issues
            rectTransform.localScale = Vector3.one * 0.5f; // Reduce the base scale to make text smaller
        }
        
        TMP_Text tmpText = textObj.GetComponent<TMP_Text>();
        if (tmpText != null)
        {
            // Set text content based on multiplier
            string displayText = multiplier > 1 ? $"{multiplier}x {trickName}!" : $"{trickName}!";
            if (comboMultiplier > 1f)
            {
                displayText += $"\nCombo x{comboMultiplier:F1}";
            }
            tmpText.text = displayText;
            
            // Set color based on type
            if (comboMultiplier > 1f)
                tmpText.color = comboColor;
            else if (multiplier > 1)
                tmpText.color = multiFlipColor;
            else
                tmpText.color = normalColor;

            StartCoroutine(AnimateText(textObj, tmpText));
        }
    }

    private IEnumerator AnimateText(GameObject textObj, TMP_Text tmpText)
    {
        RectTransform rectTransform = textObj.GetComponent<RectTransform>();
        Vector2 startPos = rectTransform.anchoredPosition;
        Vector3 baseScale = Vector3.one * 0.5f; // Keep the smaller base scale

        // Initial scale animation
        float elapsed = 0f;
        while (elapsed < scaleTime)
        {
            elapsed += Time.deltaTime;
            float scale = Mathf.Lerp(1f, maxScale, elapsed / scaleTime);
            rectTransform.localScale = baseScale * scale;
            yield return null;
        }

        // Scale back down
        elapsed = 0f;
        while (elapsed < scaleTime)
        {
            elapsed += Time.deltaTime;
            float scale = Mathf.Lerp(maxScale, 1f, elapsed / scaleTime);
            rectTransform.localScale = baseScale * scale;
            yield return null;
        }

        // Float up and fade out
        elapsed = 0f;
        Color startColor = tmpText.color;

        while (elapsed < displayTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, (elapsed / displayTime) * fadeSpeed);
            tmpText.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            
            // Float upward in screen space
            rectTransform.anchoredPosition = startPos + Vector2.up * (elapsed * floatSpeed * 100f);
            yield return null;
        }

        Destroy(textObj);
    }
}