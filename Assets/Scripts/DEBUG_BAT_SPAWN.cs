using UnityEngine;

/// <summary>
/// DEBUG SCRIPT: Add this to an empty GameObject to verify bat spawning and attack setup.
/// This will log what's happening when bats spawn and attack.
/// 
/// CHECKLIST for fixing bat spawn/attack issues:
/// 1. ✓ FIXED: EnemyPoolManager.SpawnEnemy() had dequeue bug - NOW FIXED
/// 2. ✓ FIXED: BatEnemy.Initialize() wasn't resetting timers - NOW FIXED  
/// 3. ✓ FIXED: BatEnemy.AttemptAttackPlayer() was looking for PlayerController instead of HealthSystem - NOW FIXED
/// 4. ✓ FIXED: PlayerController didn't have healthSystem reference - NOW ADDED
/// 5. ✓ FIXED: PlayerController.Grounded() wasn't calling healthSystem.SetGrounded() - NOW ADDED
/// 
/// TO USE THIS DEBUG SCRIPT:
/// - Attach to an empty GameObject in your scene
/// - Check the Console output when you start a wave
/// - Look for these patterns:
///   * "Spawned bat at..." → bat is instantiated correctly
///   * "BatEnemy initialized with player ref: True" → bat found the player
///   * "BatEnemy attacked player for..." → bat is attacking
///   * If not seeing these, the issue is in console logs below
/// </summary>
public class DebugBatSpawn : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("======= BAT SPAWN DEBUG =======");
        Debug.Log("Checking scene setup...");
        
        // Check GameManager
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("❌ GameManager not found!");
            return;
        }
        Debug.Log("✓ GameManager found");
        
        // Check HealthSystem
        if (gm.healthSystem == null)
        {
            Debug.LogError("❌ HealthSystem not found in GameManager!");
        }
        else
        {
            Debug.Log($"✓ HealthSystem found. Current health: {gm.healthSystem.GetCurrentHealth()}");
        }
        
        // Check PlayerController
        if (gm.playerController == null)
        {
            Debug.LogError("❌ PlayerController not found in GameManager!");
        }
        else
        {
            Debug.Log("✓ PlayerController found");
        }
        
        // Check EnemyPoolManager
        EnemyPoolManager epm = EnemyPoolManager.Instance;
        if (epm == null)
        {
            Debug.LogError("❌ EnemyPoolManager not found!");
        }
        else
        {
            Debug.Log($"✓ EnemyPoolManager found. Pool size: {epm.GetAvailableCount()}");
        }
        
        // Check WaveController
        WaveController wc = FindObjectOfType<WaveController>();
        if (wc == null)
        {
            Debug.LogWarning("⚠ WaveController not found (optional if starting waves manually)");
        }
        else
        {
            Debug.Log("✓ WaveController found");
        }
        
        Debug.Log("======= END BAT SPAWN DEBUG =======");
    }
}
