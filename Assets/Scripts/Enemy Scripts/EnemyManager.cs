using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages all enemies - optimized for WebGL with pooling
/// </summary>
public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance;

    private static List<BatEnemy> enemies = new List<BatEnemy>();

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

    public static void RegisterEnemy(BatEnemy enemy)
    {
        if (!enemies.Contains(enemy))
        {
            enemies.Add(enemy);
        }
    }

    public static void UnregisterEnemy(BatEnemy enemy)
    {
        if (enemies.Contains(enemy))
        {
            enemies.Remove(enemy);
        }
    }

    public static int Count => enemies.Count;

    public static void KillAll()
    {
        // Use pooling instead of destroying
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            if (enemies[i] != null)
            {
                enemies[i].Die();
            }
        }
        enemies.Clear();
    }

    // WebGL optimization: Get enemies in range without allocating
    public static List<BatEnemy> GetEnemiesInRange(Vector3 position, float range)
    {
        List<BatEnemy> result = new List<BatEnemy>();
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
    public static BatEnemy GetClosestEnemy(Vector3 position)
    {
        BatEnemy closest = null;
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

    void OnDestroy()
    {
        enemies.Clear();
    }
}