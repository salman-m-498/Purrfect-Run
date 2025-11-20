using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
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
                Debug.LogError("HealthBarUI: Fill Image not found! Please assign it in inspector or ensure child object has Image component.");
            }
        }
        
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }
    }
    
    [Header("Colors")]
    public Color fullHealthColor = new Color(1f, 0.2f, 0.2f);    // Red
    public Color mediumHealthColor = new Color(1f, 0.6f, 0.2f);  // Orange
    public Color lowHealthColor = new Color(1f, 0.6f, 0.2f);  // Orange
    [Header("Animation")]
    public float smoothSpeed = 10f;   // How fast the bar fills/depletes
    public float flickerSpeed = 10f;  // Speed of low health flicker
    public float flickerIntensity = 0.2f;
    
    private float targetFill = 1f;
    private float currentFill = 1f;

    public void UpdateHealth(float currentHealth, float maxHealth)
    {
        targetFill = currentHealth / maxHealth;
    }

    public static HealthBarUI CreateHealthBar(Transform canvas)
    {
        Debug.Log("Creating Health Bar...");
        
        // Create the main bar object
        GameObject barObj = new GameObject("HealthBar");
        barObj.transform.SetParent(canvas, false);
        
        // Add the UI components
        HealthBarUI healthBar = barObj.AddComponent<HealthBarUI>();
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
        Debug.Log($"Health bar created at position: {barRect.anchoredPosition}, size: {barRect.sizeDelta}");
        
        // Configure the fill to match parent size
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        
        // Set up the images
        background.color = new Color(0, 0, 0, 0.5f);
        fillImage.color = healthBar.fullHealthColor;
        
        // Set fill settings
        background.type = Image.Type.Filled;
        background.fillMethod = Image.FillMethod.Horizontal;
        background.fillOrigin = (int)Image.OriginHorizontal.Left;
        background.fillAmount = 1;
        
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        
        // Assign references
        healthBar.fillImage = fillImage;
        healthBar.backgroundImage = background;
        
        return healthBar;
    }

    private void Update()
    {
        if (fillImage == null) return;
        
        // Smoothly interpolate current fill to target
        currentFill = Mathf.Lerp(currentFill, targetFill, Time.deltaTime * smoothSpeed);
        fillImage.fillAmount = currentFill;

        // Update color based on health level
        if (currentFill > 0.7f)
        {
            fillImage.color = fullHealthColor;
        }
        else if (currentFill > 0.3f)
        {
            fillImage.color = Color.Lerp(mediumHealthColor, fullHealthColor, (currentFill - 0.3f) / 0.4f);
        }
        else
        {
            // Base low health color
            Color targetColor = Color.Lerp(lowHealthColor, mediumHealthColor, currentFill / 0.3f);
            
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