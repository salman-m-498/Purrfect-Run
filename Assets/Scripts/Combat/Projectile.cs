using UnityEngine;
using System;

/// <summary>
/// Lightweight homing projectile used by AutoAttackSystem.
/// Movement is handled in Update with simple steering to target (no physics required).
/// On hit it calls enemy.TakeDamage(...) and will try to bounce to another enemy if bounces remain.
/// </summary>
public class Projectile : MonoBehaviour
{
    [Header("Optional Visual")]
    public GameObject visual; // optional child visual

    private float damage;
    private float speed;
    private int bouncesRemaining;
    private float hitRadius = 0.5f;
    private Transform target;
    private float bounceSearchRadius = 8f;
    private AutoAttackSystem ownerSystem;
    private Action<Projectile> returnToPool;
    private float maxLifetime = 8f;
    private float lifeTimer = 0f;

    private void OnEnable()
    {
        lifeTimer = 0f;
        EnsureNonPhysics();
    }

    public void InitReturnCallback(Action<Projectile> callback)
    {
        returnToPool = callback;
    }

    /// <summary>
    /// Configure the projectile to begin moving toward a target.
    /// </summary>
    public void SetState(float damage, float speed, int bounces, float hitRadius, Transform initialTarget, float bounceSearchRadius, AutoAttackSystem owner)
    {
        this.damage = damage;
        this.speed = speed;
        this.bouncesRemaining = Mathf.Max(0, bounces);
        this.hitRadius = hitRadius;
        this.target = initialTarget;
        this.bounceSearchRadius = bounceSearchRadius;
        this.ownerSystem = owner;
        lifeTimer = 0f;
        // Ensure physics won't push rigidbodies on collision: prefer trigger-only or pure transform movement
        EnsureNonPhysics();
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Make sure the projectile won't apply physics forces on other Rigidbodies.
    /// This enforces colliders to be triggers and any Rigidbody to be kinematic or collision-disabled.
    /// It is safe to call multiple times.
    /// </summary>
    private void EnsureNonPhysics()
    {
        // Make all colliders triggers so physics collision resolution doesn't apply forces
        var cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            try { cols[i].isTrigger = true; } catch { }
        }

        // If there's a Rigidbody, make it kinematic and disable collision response
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false; // still safe because colliders are triggers
        }
    }

    private void Update()
    {
        lifeTimer += Time.deltaTime;
        if (lifeTimer > maxLifetime)
        {
            Return();
            return;
        }

        if (target == null)
        {
            // Try to find any nearby target
            BatEnemy fallback = EnemyManager.GetClosestEnemy(transform.position);
            if (fallback != null)
            {
                target = fallback.transform;
            }
            else
            {
                Return();
                return;
            }
        }

        // Move toward target
        Vector3 toTarget = target.position - transform.position;
        float dist = toTarget.magnitude;

        if (dist <= hitRadius)
        {
            HitTarget();
            return;
        }

        Vector3 move = toTarget.normalized * speed * Time.deltaTime;
        transform.position += move;
        if (visual != null)
            visual.transform.rotation = Quaternion.LookRotation(move.normalized);
    }

    private void HitTarget()
    {
        if (target == null)
        {
            Return();
            return;
        }

        BatEnemy be = target.GetComponent<BatEnemy>();
        if (be != null)
        {
            be.TakeDamage(damage);
        }

        // bounce logic
        if (bouncesRemaining > 0)
        {
            bouncesRemaining--;
            // find next closest enemy within bounceSearchRadius (excluding current target)
            BatEnemy next = FindNextBounceTarget(target.position);
            if (next != null && next.transform != target)
            {
                target = next.transform;
                return; // continue flying to next target
            }
        }

        // no bounces left or no next target -> return to pool
        Return();
    }

    private BatEnemy FindNextBounceTarget(Vector3 fromPos)
    {
        // Use EnemyManager to get closest enemy; if it's the current target, try to find the next by scanning nearby list
        BatEnemy closest = EnemyManager.GetClosestEnemy(fromPos);
        if (closest == null) return null;

        if (closest.transform == target)
        {
            // brute-force search through manager list for one within bounceSearchRadius
            var list = typeof(EnemyManager).GetField("enemies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (list != null)
            {
                var enemies = list.GetValue(null) as System.Collections.Generic.List<BatEnemy>;
                if (enemies != null)
                {
                    float bestDist = float.MaxValue;
                    BatEnemy best = null;
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        var e = enemies[i];
                        if (e == null || e.transform == target || !e.gameObject.activeSelf) continue;
                        float d = (e.transform.position - fromPos).sqrMagnitude;
                        if (d <= bounceSearchRadius * bounceSearchRadius && d < bestDist)
                        {
                            bestDist = d;
                            best = e;
                        }
                    }
                    return best;
                }
            }
            // fallback: no reflection access -> return null
            return null;
        }

        // ensure within bounce radius
        if ((closest.transform.position - fromPos).sqrMagnitude <= bounceSearchRadius * bounceSearchRadius)
            return closest;

        return null;
    }

    private void Return()
    {
        // cleanup
        target = null;
        ownerSystem = null;
        returnToPool?.Invoke(this);
    }
}
