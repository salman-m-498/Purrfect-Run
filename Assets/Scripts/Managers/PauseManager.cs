using UnityEngine;
using System;

/// <summary>
/// PauseManager: Central system for handling pause/resume.
/// Supports stacked pause requests and fires events.
/// </summary>
public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    private int pauseStack = 0;

    public bool IsPaused => pauseStack > 0;

    // Events
    public event Action OnPause;
    public event Action OnResume;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Request pause. Must be paired with Resume().
    /// </summary>
    public void Pause()
    {
        pauseStack++;
        if (pauseStack == 1)
        {
            Time.timeScale = 0f;
            OnPause?.Invoke();
            Debug.Log("[PauseManager] Game paused.");
        }
    }

    /// <summary>
    /// Request resume. Must match a previous Pause() call.
    /// </summary>
    public void Resume()
    {
        if (pauseStack == 0)
        {
            Debug.LogWarning("[PauseManager] Resume() called without Pause().");
            return;
        }

        pauseStack--;
        if (pauseStack == 0)
        {
            Time.timeScale = 1f;
            OnResume?.Invoke();
            Debug.Log("[PauseManager] Game resumed.");
        }
    }

    /// <summary>
    /// Force resume all pauses (use with caution).
    /// </summary>
    public void ForceResumeAll()
    {
        pauseStack = 0;
        Time.timeScale = 1f;
        OnResume?.Invoke();
        Debug.Log("[PauseManager] Force resumed all pauses.");
    }
}