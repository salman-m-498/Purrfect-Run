using UnityEngine;

/// <summary>
/// FallDetector - Monitors player position and health for loss conditions.
/// Works with both standard GameManager and EndlessGameManager.
/// Loss conditions:
/// - Falls too far below level (Y < threshold)
/// - Wanders off the Z=0 plane
/// - Health reaches 0
/// </summary>
public class FallDetector : MonoBehaviour
{
    [Header("Detection Parameters")]
    public float fallDeathHeight = -20f;          // Y position below which player dies
    public float outOfBoundsZDistance = 10f;      // How far from Z=0 before falling off
    public float checkInterval = 0.1f;            // How often to check (optimization)

    [Header("References")]
    public PlayerController playerController;
    public GameManager gameManager;
    public EndlessGameManager endlessGameManager;
    public HealthSystem healthSystem;

    private float lastCheckTime = 0f;
    private bool hasTriggeredFailure = false;

    void Start()
    {
        FindReferences();
    }

    void Update()
    {
        // Prevent multiple failure triggers
        if (hasTriggeredFailure) return;

        // Check periodically instead of every frame for performance
        if (Time.time - lastCheckTime < checkInterval) return;
        lastCheckTime = Time.time;

        CheckFallConditions();
        CheckDeathCondition();
    }

    // ============================================================
    // LOSS CONDITIONS
    // ============================================================

    private void CheckFallConditions()
    {
        if (playerController == null) return;

        Vector3 playerPos = playerController.transform.position;

        // Loss 1: Fall off the level (too far below)
        if (playerPos.y < fallDeathHeight)
        {
            TriggerFailure("Fell off the level!");
            return;
        }

        // Loss 2: Wander off the Z=0 plane
        if (Mathf.Abs(playerPos.z) > outOfBoundsZDistance)
        {
            TriggerFailure("Wandered off the track!");
            return;
        }
    }

    private void CheckDeathCondition()
    {
        if (healthSystem == null) return;

        if (healthSystem.GetCurrentHealth() <= 0)
        {
            TriggerFailure("Health depleted!");
        }
    }

    // ============================================================
    // FAILURE HANDLER
    // ============================================================

    private void TriggerFailure(string reason)
    {
        hasTriggeredFailure = true;

        Debug.Log($"💀 FallDetector: {reason}");

        // Notify appropriate manager
        if (endlessGameManager != null && endlessGameManager.currentState == EndlessGameManager.EndlessGameState.Playing)
        {
            endlessGameManager.EndGame(reason);
        }
        else if (gameManager != null)
        {
            gameManager.ChangeGameState(GameState.LevelFailed);
        }
    }

    // ============================================================
    // UTILITIES
    // ============================================================

    private void FindReferences()
    {
        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();
        if (endlessGameManager == null)
            endlessGameManager = FindObjectOfType<EndlessGameManager>();
        if (healthSystem == null)
            healthSystem = FindObjectOfType<HealthSystem>();
    }

    public void ResetForNewLevel()
    {
        hasTriggeredFailure = false;
    }

    // ============================================================
    // DEBUG
    // ============================================================

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (playerController == null) return;

        Vector3 playerPos = playerController.transform.position;

        // Draw fall threshold (red line)
        Gizmos.color = Color.red;
        Vector3 fallLineStart = playerPos - Vector3.right * 50 + Vector3.up * fallDeathHeight;
        Vector3 fallLineEnd = playerPos + Vector3.right * 50 + Vector3.up * fallDeathHeight;
        Gizmos.DrawLine(fallLineStart, fallLineEnd);

        // Draw Z bounds (yellow lines)
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            playerPos - Vector3.forward * outOfBoundsZDistance,
            playerPos + Vector3.forward * outOfBoundsZDistance
        );

        // Draw safe zone (green box)
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireCube(
            playerPos + Vector3.up * 2,
            new Vector3(100, 40, outOfBoundsZDistance * 2)
        );
    }
#endif
}
