using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Creates a cartoon-style circular zoom transition effect (iris out/in)
/// Used for dramatic game over transitions
/// </summary>
public class CircularTransition : MonoBehaviour
{
    public static CircularTransition Instance { get; private set; }

    [Header("Transition Settings")]
    public Image transitionImage;
    public Material circleMaterial; // Uses a shader for circular mask
    public float transitionDuration = 1.5f;
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Optional Center Target")]
    public Transform focusTarget; // If set, transition centers on this (e.g., player)
    
    private Camera mainCamera;
    private Canvas canvas;
    private bool isTransitioning = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        mainCamera = Camera.main;
        canvas = GetComponentInParent<Canvas>();
        
        // Start fully open (no transition)
        if (transitionImage != null)
        {
            transitionImage.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Zoom to black from a specific world position (like the player)
    /// </summary>
    public IEnumerator ZoomToBlack(Vector3 worldPosition, System.Action onComplete = null)
    {
        isTransitioning = true;
        
        if (transitionImage == null)
        {
            Debug.LogError("CircularTransition: transitionImage is null!");
            onComplete?.Invoke();
            yield break;
        }

        // Convert world position to screen space for centering the circle
        Vector2 screenCenter = GetScreenCenter(worldPosition);
        
        // Activate transition image
        transitionImage.gameObject.SetActive(true);
        transitionImage.color = Color.black;
        
        // Set material properties
        Material mat = transitionImage.material;
        if (mat != null)
        {
            mat.SetVector("_Center", screenCenter);
        }
        
        // Animate from full screen (radius 1) to zero (closed)
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime; // Use unscaled time in case game is paused
            float t = elapsed / transitionDuration;
            float curveValue = transitionCurve.Evaluate(t);
            
            // Radius goes from 1.5 (fully open) to 0 (fully closed)
            float radius = Mathf.Lerp(1.5f, 0f, curveValue);
            
            if (mat != null)
            {
                mat.SetFloat("_Radius", radius);
            }
            
            yield return null;
        }
        
        // Ensure fully closed
        if (mat != null)
        {
            mat.SetFloat("_Radius", 0f);
        }
        
        isTransitioning = false;
        onComplete?.Invoke();
    }

    /// <summary>
    /// Zoom from black to open (reverse transition)
    /// </summary>
    public IEnumerator ZoomFromBlack(Vector3 worldPosition, System.Action onComplete = null)
    {
        isTransitioning = true;
        
        if (transitionImage == null)
        {
            Debug.LogError("CircularTransition: transitionImage is null!");
            onComplete?.Invoke();
            yield break;
        }

        Vector2 screenCenter = GetScreenCenter(worldPosition);
        
        transitionImage.gameObject.SetActive(true);
        transitionImage.color = Color.black;
        
        Material mat = transitionImage.material;
        if (mat != null)
        {
            mat.SetVector("_Center", screenCenter);
        }
        
        // Animate from zero (closed) to full screen (radius 1.5)
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / transitionDuration;
            float curveValue = transitionCurve.Evaluate(t);
            
            float radius = Mathf.Lerp(0f, 1.5f, curveValue);
            
            if (mat != null)
            {
                mat.SetFloat("_Radius", radius);
            }
            
            yield return null;
        }
        
        // Deactivate when fully open
        transitionImage.gameObject.SetActive(false);
        
        isTransitioning = false;
        onComplete?.Invoke();
    }

    /// <summary>
    /// Simple fade to black (fallback if shader isn't available)
    /// </summary>
    public IEnumerator FadeToBlack(System.Action onComplete = null)
    {
        isTransitioning = true;
        
        if (transitionImage == null)
        {
            Debug.LogError("CircularTransition: transitionImage is null!");
            onComplete?.Invoke();
            yield break;
        }

        transitionImage.gameObject.SetActive(true);
        
        float elapsed = 0f;
        Color startColor = new Color(0, 0, 0, 0);
        Color endColor = Color.black;
        
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / transitionDuration;
            transitionImage.color = Color.Lerp(startColor, endColor, t);
            yield return null;
        }
        
        transitionImage.color = endColor;
        isTransitioning = false;
        onComplete?.Invoke();
    }

    private Vector2 GetScreenCenter(Vector3 worldPosition)
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        
        if (mainCamera == null)
            return new Vector2(0.5f, 0.5f); // Center of screen as fallback
        
        // Convert world position to viewport coordinates (0-1 range)
        Vector3 viewportPos = mainCamera.WorldToViewportPoint(worldPosition);
        
        // Clamp to screen bounds
        viewportPos.x = Mathf.Clamp01(viewportPos.x);
        viewportPos.y = Mathf.Clamp01(viewportPos.y);
        
        return new Vector2(viewportPos.x, viewportPos.y);
    }

    public bool IsTransitioning => isTransitioning;
}