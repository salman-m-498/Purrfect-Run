using UnityEngine;

/// <summary>
/// WebGL-specific: Ensures keyboard input works properly by maintaining canvas focus
/// In WebGL, the browser doesn't give keyboard focus to the game canvas automatically.
/// This script forces focus on startup and after any mouse/touch interaction.
/// </summary>
public class WebGLInputFocusManager : MonoBehaviour
{
    private static WebGLInputFocusManager instance;
    private bool hasInitialized = false;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Initial focus attempt on startup
        FocusCanvas();
        Debug.Log("🎮 WebGLInputFocusManager: Canvas focus requested on startup");
#endif
    }

    void Update()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Detect any user interaction and ensure focus
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.touchCount > 0)
        {
            FocusCanvas();
        }
        
        // Periodically ensure focus (every 30 frames = ~0.5 seconds at 60fps)
        if (Time.frameCount % 30 == 0)
        {
            FocusCanvas();
        }
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private void FocusCanvas()
    {
        // Focus the canvas so keyboard events work
        Application.ExternalEval("window.focus();");
    }
#else
    private void FocusCanvas()
    {
        // No-op on non-WebGL platforms
    }
#endif
}
