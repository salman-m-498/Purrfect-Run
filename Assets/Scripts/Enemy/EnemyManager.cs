using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages all enemies - optimized for WebGL with pooling
/// </summary>
public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance;

    private static List<IEnemy> enemies = new List<IEnemy>();


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Don't persist to avoid conflicts with GameManager
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static void RegisterEnemy(IEnemy enemy)
    {
        if (!enemies.Contains(enemy))
        {
            enemies.Add(enemy);
        }
    }

    public static void UnregisterEnemy(IEnemy enemy)
    {
        if (enemies.Contains(enemy))
        {
            enemies.Remove(enemy);
        }
    }

    public static int Count => enemies.Count;

    public static void KillAll()
    {
        Debug.Log($"EnemyManager: Killing all {enemies.Count} enemies");
        
        // Create a copy of the list to avoid modification during iteration
        List<IEnemy> enemiesToKill = new List<IEnemy>(enemies);
        
        // Use pooling instead of destroying
        for (int i = enemiesToKill.Count - 1; i >= 0; i--)
        {
            if (enemiesToKill[i] != null)
            {
                try
                {
                    enemiesToKill[i].Die();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Error killing enemy: {e.Message}");
                }
            }
        }
        
        // Clear the main list
        enemies.Clear();
        
        Debug.Log("✅ All enemies killed and list cleared");
    }

    // Also add this helper method to check if system is ready
    public static bool IsReady()
    {
        return Instance != null;
    }

    // Add this to help debug enemy spawning issues
    public static void LogEnemyStatus()
    {
        if (Instance == null)
        {
            Debug.LogWarning("EnemyManager instance is NULL!");
            return;
        }
        
        Debug.Log($"EnemyManager Status: {enemies.Count} total enemies registered");
        
        int activeCount = 0;
        int inactiveCount = 0;
        
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                if (enemy.gameObject.activeSelf)
                    activeCount++;
                else
                    inactiveCount++;
            }
        }
        
        Debug.Log($"  - Active: {activeCount}");
        Debug.Log($"  - Inactive: {inactiveCount}");
        Debug.Log($"  - Null: {enemies.Count - activeCount - inactiveCount}");
    }


    public static event System.Action<IEnemy> OnEnemyDeath;
    public static void NotifyDeath(IEnemy dead) => OnEnemyDeath?.Invoke(dead);

    // WebGL optimization: Get enemies in range without allocating
    public static List<IEnemy> GetEnemiesInRange(Vector3 position, float range)
    {
        List<IEnemy> result = new List<IEnemy>();
        float rangeSqr = range * range;
        
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null && enemies[i].gameObject.activeSelf)
            {
                float sqrDist = (enemies[i].transform.position - position).sqrMagnitude;
                if (sqrDist <= rangeSqr)
                {
                    result.Add(enemies[i]);
                }
            }
        }
        
        return result;
    }

    // Get closest enemy (for targeting)
    public static IEnemy GetClosestEnemy(Vector3 position)
    {
        IEnemy closest = null;
        float closestDist = float.MaxValue;
        
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null && enemies[i].gameObject.activeSelf)
            {
                float sqrDist = (enemies[i].transform.position - position).sqrMagnitude;
                if (sqrDist < closestDist)
                {
                    closestDist = sqrDist;
                    closest = enemies[i];
                }
            }
        }
        
        return closest;
    }

    /// <summary>
    /// Return a list of currently active enemies. Allocates a new list.
    /// Use sparingly (e.g. once per frame) to avoid GC pressure.
    /// </summary>
    public static List<IEnemy> GetActiveEnemies()
    {
        List<IEnemy> result = new List<IEnemy>();
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e != null && e.gameObject.activeSelf)
                result.Add(e);
        }
        return result;
    }

    void OnDestroy()
    {
        enemies.Clear();
    }
}