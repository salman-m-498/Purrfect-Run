using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AutoAttackSystem: Automatically targets nearest enemy and fires pooled projectiles.
/// Designed for performance: uses an internal object pool and minimal allocations.
/// Attach to the player GameObject and configure stats in inspector.
/// </summary>
public class AutoAttackSystem : MonoBehaviour
{
    [Header("Core Stats")]
    [Tooltip("Base damage dealt by each projectile")]
    public float damage = 10f;

    [Tooltip("Chance (0-1) to crit")]
    [Range(0f, 1f)]
    public float critChance = 0.1f;

    [Tooltip("Crit damage multiplier (e.g. 1.5 -> 150% damage)")]
    public float critDamage = 1.5f;

    [Tooltip("Base attacks per second (before Attack Speed percent modifier)")]
    public float attacksPerSecond = 1f;

    [Tooltip("Attack speed as a percent of base (100 = normal, 200 = double rate)")]
    [Range(10f, 500f)]
    public float attackSpeedPercent = 100f;

    [Header("Projectile Settings")]
    [Tooltip("How many projectiles fire per attack cycle")]
    public int projectileCount = 1;

    [Tooltip("How many bounces each projectile can perform between enemies")]
    public int projectileBounces = 1;

    [Tooltip("Projectile travel speed (units/sec)")]
    public float projectileSpeed = 20f;

    [Tooltip("Projectile prefab (must have Projectile component). If null, system will try to create a basic visualless projectile at runtime.")]
    public Projectile projectilePrefab;

    [Tooltip("Maximum distance a projectile will search for next target when bouncing")]
    public float bounceSearchRadius = 10f;

    [Tooltip("How close a projectile must be to consider it a hit")]
    public float hitRadius = 0.5f;

    [Header("Pool / Performance")]
    [Tooltip("Initial pool size for projectiles")]
    public int initialPoolSize = 20;

    private float nextFireTime = 0f;
    private Queue<Projectile> pool = new Queue<Projectile>();
    private Transform poolParent;

    private Transform ownerTransform;

    private void Awake()
    {
        ownerTransform = transform;
        poolParent = new GameObject("Projectile Pool").transform;
        poolParent.SetParent(transform);

        if (projectilePrefab == null)
        {
            // Create a minimal runtime projectile if none assigned
            GameObject go = new GameObject("_AutoAttackProjectile_Prefab");
            Projectile p = go.AddComponent<Projectile>();
            p.visual = null; // no visuals by default
            projectilePrefab = p;
        }

        // Prewarm pool
        for (int i = 0; i < initialPoolSize; i++)
        {
            InstantiateNewProjectileToPool();
        }
    }

    private Projectile InstantiateNewProjectileToPool()
    {
        Projectile p = Instantiate(projectilePrefab.gameObject, poolParent).GetComponent<Projectile>();
        p.gameObject.SetActive(false);
        p.InitReturnCallback(ReturnProjectileToPool);
        pool.Enqueue(p);
        return p;
    }

    private void ReturnProjectileToPool(Projectile p)
    {
        p.gameObject.SetActive(false);
        p.transform.SetParent(poolParent, true);
        pool.Enqueue(p);
    }

    private Projectile GetProjectileFromPool()
    {
        if (pool.Count == 0)
        {
            // grow pool as needed
            return InstantiateNewProjectileToPool();
        }

        Projectile p = pool.Dequeue();
        p.gameObject.SetActive(true);
        return p;
    }

    private void Update()
    {
        if (Time.time >= nextFireTime)
        {
            TryFire();
            float effectiveRate = attacksPerSecond * (attackSpeedPercent / 100f);
            float interval = (effectiveRate > 0f) ? (1f / effectiveRate) : 1f;
            nextFireTime = Time.time + interval;
        }
    }

    private void TryFire()
    {
        if (GameManager.Instance == null) return;

        // get closest enemy
        BatEnemy target = EnemyManager.GetClosestEnemy(ownerTransform.position);
        if (target == null) return;

        // Fire projectileCount projectiles using small spread
        for (int i = 0; i < projectileCount; i++)
        {
            Projectile p = GetProjectileFromPool();
            // compute spread direction
            Vector3 dir = (target.transform.position - ownerTransform.position).normalized;
            if (projectileCount > 1)
            {
                float spreadAngle = 8f; // degrees total spread default
                float offset = Mathf.Lerp(-spreadAngle, spreadAngle, (float)i / (projectileCount - 1));
                dir = Quaternion.Euler(0f, offset, 0f) * dir;
            }

            float roll = Random.value;
            bool isCrit = roll <= critChance;
            float appliedDamage = isCrit ? damage * critDamage : damage;

            p.transform.position = ownerTransform.position;
            p.transform.rotation = Quaternion.LookRotation(dir);
            p.SetState(appliedDamage, projectileSpeed, projectileBounces, hitRadius, target.transform, bounceSearchRadius, this);
        }
    }

    // Public API for other systems to adjust stats at runtime
    public void SetAttackSpeedPercent(float percent)
    {
        attackSpeedPercent = Mathf.Max(1f, percent);
    }

    public void SetProjectilePrefab(Projectile prefab)
    {
        projectilePrefab = prefab;
    }
}
