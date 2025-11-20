using UnityEngine;
using UnityEngine.UI;

public class StaminaBarUI : MonoBehaviour
{
    [Header("Bar Components")]
    [SerializeField] private Image fillImage;           // The foreground fill image
    [SerializeField] private Image backgroundImage;     // Optional background image
    
    private void Awake()
    {
        // Auto-find components if not assigned
        if (fillImage == null)
        {
            fillImage = transform.GetChild(0).GetComponent<Image>();
            if (fillImage == null)
            {
                Debug.LogError("StaminaBarUI: Fill Image not found! Please assign it in inspector or ensure child object has Image component.");
            }
        }
        
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }
    }
    
    [Header("Colors")]
    public Color fullStaminaColor = new Color(0.2f, 0.8f, 1f);    // Blue
    public Color mediumStaminaColor = new Color(1f, 0.6f, 0.2f);  // Orange
    public Color lowStaminaColor = new Color(1f, 0.2f, 0.2f);     // Red
    
    [Header("Animation")]
    public float smoothSpeed = 10f;   // How fast the bar fills/depletes
    public float flickerSpeed = 10f;  // Speed of low stamina flicker
    public float flickerIntensity = 0.2f;
    
    private float targetFill = 1f;
    private float currentFill = 1f;

    public void UpdateStamina(float currentStamina, float maxStamina)
    {
        targetFill = currentStamina / maxStamina;
    }

    public static StaminaBarUI CreateStaminaBar(Transform canvas)
    {
        Debug.Log("Creating Stamina Bar...");
        
        // Create the main bar object
        GameObject barObj = new GameObject("StaminaBar");
        barObj.transform.SetParent(canvas, false);
        
        // Add the UI components
        StaminaBarUI staminaBar = barObj.AddComponent<StaminaBarUI>();
        Image background = barObj.AddComponent<Image>();
        
        // Create the fill image
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(barObj.transform, false);
        Image fillImage = fillObj.AddComponent<Image>();
        
        // Set up the RectTransforms
        RectTransform barRect = barObj.GetComponent<RectTransform>();
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        
        // Configure the bar position and size
        barRect.anchorMin = new Vector2(0.5f, 0);
        barRect.anchorMax = new Vector2(0.5f, 0);
        barRect.pivot = new Vector2(0.5f, 0);
        barRect.sizeDelta = new Vector2(300, 20);
        
        // Check if we're in Screen Space - Camera mode
        Canvas parentCanvas = canvas.GetComponent<Canvas>();
        if (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            // Adjust position for Screen Space - Camera
            barRect.anchoredPosition = new Vector2(0, 80); // Higher up to ensure visibility
            
            // Ensure proper scaling and depth
            barObj.transform.localScale = Vector3.one;
            barObj.transform.localPosition = new Vector3(barObj.transform.localPosition.x, 
                                                       barObj.transform.localPosition.y, 
                                                       0); // Set to same depth as other UI elements
        }
        else
        {
            barRect.anchoredPosition = new Vector2(0, 50);
        }
        
        // Ensure the bar is visible in the scene
        Debug.Log($"Stamina bar created at position: {barRect.anchoredPosition}, size: {barRect.sizeDelta}");
        
        // Configure the fill to match parent size
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        
        // Set up the images
        background.color = new Color(0, 0, 0, 0.5f);
        fillImage.color = staminaBar.fullStaminaColor;
        
        // Set fill settings
        background.type = Image.Type.Filled;
        background.fillMethod = Image.FillMethod.Horizontal;
        background.fillOrigin = (int)Image.OriginHorizontal.Left;
        background.fillAmount = 1;
        
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        
        // Assign references
        staminaBar.fillImage = fillImage;
        staminaBar.backgroundImage = background;
        
        return staminaBar;
    }

    private void Update()
    {
        if (fillImage == null) return;
        
        // Smoothly interpolate current fill to target
        currentFill = Mathf.Lerp(currentFill, targetFill, Time.deltaTime * smoothSpeed);
        fillImage.fillAmount = currentFill;

        // Update color based on stamina level
        if (currentFill > 0.7f)
        {
            fillImage.color = fullStaminaColor;
        }
        else if (currentFill > 0.3f)
        {
            fillImage.color = Color.Lerp(mediumStaminaColor, fullStaminaColor, (currentFill - 0.3f) / 0.4f);
        }
        else
        {
            // Base low stamina color
            Color targetColor = Color.Lerp(lowStaminaColor, mediumStaminaColor, currentFill / 0.3f);
            
            // Add flicker effect when low
            if (currentFill < 0.3f)
            {
                float flicker = 1f + (Mathf.Sin(Time.time * flickerSpeed) * flickerIntensity);
                targetColor *= flicker;
            }
            
            fillImage.color = targetColor;
        }
    }
}