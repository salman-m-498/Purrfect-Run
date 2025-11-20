using UnityEngine;

/// <summary>
/// Trigger-based state changer: Walk into a trigger volume and transition to a new GameState.
/// Integrates with GameManager for state management.
/// 
/// USAGE:
/// 1. Create an empty GameObject in your scene
/// 2. Add a Sphere/Box/Capsule Collider with isTrigger = true
/// 3. Add this script to the same GameObject
/// 4. Set the target GameState in the Inspector
/// 5. Optionally configure one-time-only, player tag, and exit behavior
/// </summary>
public class StateChangeTrigger : MonoBehaviour
{
    [Header("State Configuration")]
    [Tooltip("GameState to transition to when player enters")]
    public GameState targetState = GameState.Playing;

    [Header("Trigger Behavior")]
    [Tooltip("If true, trigger only works once then disables")]
    public bool oneTimeOnly = true;

    [Tooltip("If true, trigger on exit instead of enter")]
    public bool triggerOnExit = false;

    [Tooltip("Tag to check for on entering object (empty = any)")]
    public string playerTag = "Player";

    [Tooltip("Show debug logs in console")]
    public bool debugLogging = true;

    [Header("Optional Callbacks")]
    [Tooltip("Optional delay before changing state (seconds)")]
    public float delayBeforeStateChange = 0f;

    private bool hasTriggered = false;
    private Collider triggerCollider;

    private void Start()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            Debug.LogError($"StateChangeTrigger on {gameObject.name}: No Collider found! Add a Collider with isTrigger = true.");
            enabled = false;
            return;
        }

        if (!triggerCollider.isTrigger)
        {
            Debug.LogWarning($"StateChangeTrigger on {gameObject.name}: Collider isTrigger is false. Setting to true.");
            triggerCollider.isTrigger = true;
        }

        if (debugLogging)
            Debug.Log($"StateChangeTrigger initialized on {gameObject.name}. Target state: {targetState}");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnExit) return; // Only handle exit
        
        CheckAndTrigger(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!triggerOnExit) return; // Only handle enter
        
        CheckAndTrigger(other);
    }

    private void CheckAndTrigger(Collider other)
    {
        // Check if already triggered (one-time-only mode)
        if (oneTimeOnly && hasTriggered)
        {
            if (debugLogging)
                Debug.Log($"StateChangeTrigger on {gameObject.name}: Already triggered once. Ignoring.");
            return;
        }

        // Check player tag if specified
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag))
        {
            return;
        }

        // Check if GameManager exists
        if (GameManager.Instance == null)
        {
            Debug.LogError($"StateChangeTrigger on {gameObject.name}: GameManager.Instance is null!");
            return;
        }

        if (debugLogging)
            Debug.Log($"StateChangeTrigger on {gameObject.name}: Player {other.gameObject.name} triggered. Changing to state: {targetState}");

        hasTriggered = true;

        // Apply delay if specified
        if (delayBeforeStateChange > 0f)
        {
            StartCoroutine(DelayedStateChange());
        }
        else
        {
            ChangeState();
        }
    }

    private void ChangeState()
    {
        GameManager.Instance.ChangeGameState(targetState);
        
        // Disable trigger after use if one-time-only
        if (oneTimeOnly)
        {
            triggerCollider.enabled = false;
        }
    }

    private System.Collections.IEnumerator DelayedStateChange()
    {
        yield return new WaitForSeconds(delayBeforeStateChange);
        ChangeState();
    }

    // Manual reset (useful for testing or resetting trigger for multiple uses)
    public void ResetTrigger()
    {
        hasTriggered = false;
        triggerCollider.enabled = true;
        if (debugLogging)
            Debug.Log($"StateChangeTrigger on {gameObject.name}: Reset");
    }

    // Optional: Visualize trigger area in editor
    private void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + col.bounds.center - transform.position, col.bounds.size);
    }
}
