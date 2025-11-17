using UnityEngine;
using System;
using Unity.VisualScripting;

/// <summary>
/// Optimized bat enemy with pooling support, hitboxes, and death animations
/// </summary>
public class BatEnemy : MonoBehaviour
{
    public static event Action<BatEnemy> OnBatDeath;

    [Header("Stats (use ScriptableObject for shared stats)")]
    public EnemyStats stats;
    
    [Header("Combat")]
    public float health;
    public SphereCollider attackHitbox;
    public float attackCooldown = 1f;
    public LayerMask playerLayer;
    
    [Header("Visual")]
    public Renderer batRenderer;
    public Transform visualRoot;
    
    // Cached references (WebGL optimization)
    private Transform player;
    private Transform cachedTransform;
    private Vector3 idleCenter;
    private float swoopTimer;
    private float attackTimer;
    private bool isSwooping;
    private bool isDying;
    private MaterialPropertyBlock matProps; // Avoid material instances
    
    // Death animation
    private float deathTimer;
    private Vector3 originalScale;
    private const float DEATH_DURATION = 0.5f;
    
    void Awake()
    {
        cachedTransform = transform; // Cache for WebGL
        matProps = new MaterialPropertyBlock();
        
        if (visualRoot == null)
            visualRoot = transform;
        
        originalScale = visualRoot.localScale;
        
        // Setup attack hitbox
        if (attackHitbox == null)
        {
            attackHitbox = gameObject.AddComponent<SphereCollider>();
            attackHitbox.isTrigger = true;
            attackHitbox.radius = stats != null ? stats.attackRange : 1.5f;
        }
    }

    public void Initialize(EnemyStats enemyStats)
    {
        stats = enemyStats;
        health = stats.maxHealth;
        idleCenter = cachedTransform.position;
        isDying = false;
        isSwooping = false;
        swoopTimer = 0f;
        attackTimer = 0f;
        
        // Get player reference from GameManager
        player = GameManager.Instance?.playerController?.transform;
        
        if (player == null)
        {
            Debug.LogWarning("BatEnemy: Player not found via GameManager!");
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
        
        if (player == null)
        {
            Debug.LogError("BatEnemy: Could not find player! Check tag or GameManager reference.");
        }
        
        Debug.Log($"BatEnemy initialized with player ref: {player != null}. Health: {health}");
        
        EnemyManager.RegisterEnemy(this);
    }

    void Update()
    {
        if (isDying)
        {
            UpdateDeathAnimation();
            return;
        }
        
        if (player == null) return;
        
        CodeBasedAnimation();
        StateMachine();
    }

    private void StateMachine()
    {
        // Use sqrMagnitude for WebGL optimization (avoids sqrt)
        float sqrDist = (cachedTransform.position - player.position).sqrMagnitude;
        float detectRangeSqr = stats.detectRange * stats.detectRange;
        
        swoopTimer -= Time.deltaTime;
        attackTimer -= Time.deltaTime;

        if (sqrDist < detectRangeSqr && swoopTimer <= 0)
        {
            isSwooping = true;
            swoopTimer = stats.swoopCooldown;
        }

        if (isSwooping)
        {
            SwoopAttack();
        }
        else
        {
            IdleFloat();
        }
    }

    private void IdleFloat()
    {
        Vector3 floatPos = idleCenter;
        floatPos.y += Mathf.Sin(Time.time * stats.idleFloatSpeed) * stats.idleFloatAmplitude;

        cachedTransform.position = Vector3.Lerp(
            cachedTransform.position, 
            floatPos, 
            Time.deltaTime * stats.moveSpeed
        );
        
        LookAtPlayerSmooth();
    }

    private void SwoopAttack()
    {
        cachedTransform.position = Vector3.MoveTowards(
            cachedTransform.position,
            player.position,
            stats.swoopSpeed * Time.deltaTime
        );

        LookAtPlayerSmooth();

        // Use sqrMagnitude for performance
        float sqrDist = (cachedTransform.position - player.position).sqrMagnitude;
        float attackRangeSqr = stats.attackRange * stats.attackRange;
        
        if (sqrDist < attackRangeSqr && attackTimer <= 0)
        {
            AttemptAttackPlayer();
        }

        if (sqrDist < 0.25f) // 0.5^2
        {
            isSwooping = false;
            idleCenter = cachedTransform.position;
        }
    }

    private void LookAtPlayerSmooth()
    {
        Vector3 dir = player.position - cachedTransform.position;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            cachedTransform.rotation = Quaternion.Slerp(
                cachedTransform.rotation, 
                targetRot, 
                8f * Time.deltaTime
            );
        }
    }

    private void CodeBasedAnimation()
    {
        float flap = Mathf.Sin(Time.time * stats.flapSpeed) * stats.flapAmplitude + 1.0f;
        visualRoot.localScale = new Vector3(flap, originalScale.y, flap);
    }

    // ============================================================
    // ATTACK SYSTEM
    // ============================================================

    private void AttemptAttackPlayer()
    {
        if (player == null) return;
        
        attackTimer = attackCooldown;
        
        // Damage player via HealthSystem
        HealthSystem healthSystem = player.GetComponentInParent<HealthSystem>();
        if (healthSystem != null)
        {
            bool damaged = healthSystem.ApplyDamage(stats.attackDamage, this.gameObject);
            if (damaged)
            {
                Debug.Log($"BatEnemy attacked player for {stats.attackDamage} damage! Health now: {healthSystem.GetCurrentHealth()}");
            }
        }
        else
        {
            Debug.LogWarning($"BatEnemy: Could not find HealthSystem on player at {player.name}!");
        }
    }

    // ============================================================
    // DAMAGE & DEATH
    // ============================================================

    public void TakeDamage(float damage)
    {
        if (isDying) return;
        
        health -= damage;
        
        // Visual feedback (flash red)
        StartCoroutine(FlashDamage());
        
        if (health <= 0)
        {
            Die();
        }
    }

    private System.Collections.IEnumerator FlashDamage()
    {
        if (batRenderer != null)
        {
            batRenderer.GetPropertyBlock(matProps);
            matProps.SetColor("_Color", Color.red);
            batRenderer.SetPropertyBlock(matProps);
            
            yield return new WaitForSeconds(0.1f);
            
            matProps.SetColor("_Color", Color.white);
            batRenderer.SetPropertyBlock(matProps);
        }
    }

    public void Die()
    {
        if (isDying) return;
        
        isDying = true;
        deathTimer = 0f;
        
        OnBatDeath?.Invoke(this);
        EnemyManager.UnregisterEnemy(this);
        
        // Disable colliders
        if (attackHitbox != null)
            attackHitbox.enabled = false;
    }

    private void UpdateDeathAnimation()
    {
        deathTimer += Time.deltaTime;
        float t = deathTimer / DEATH_DURATION;
        
        // Shrink + fade
        visualRoot.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
        
        if (batRenderer != null)
        {
            batRenderer.GetPropertyBlock(matProps);
            matProps.SetColor("_Color", new Color(1, 1, 1, 1 - t));
            batRenderer.SetPropertyBlock(matProps);
        }
        
        if (t >= 1f)
        {
            ReturnToPool();
        }
    }

    // ============================================================
    // POOLING
    // ============================================================

    private void ReturnToPool()
    {
        EnemyPoolManager.Instance.ReturnToPool(this);
    }

    public void ResetForPooling()
    {
        isDying = false;
        isSwooping = false;
        health = stats.maxHealth;
        swoopTimer = 0f;
        attackTimer = 0f;
        visualRoot.localScale = originalScale;
        
        if (batRenderer != null)
        {
            batRenderer.GetPropertyBlock(matProps);
            matProps.SetColor("_Color", Color.white);
            batRenderer.SetPropertyBlock(matProps);
        }
        
        if (attackHitbox != null)
            attackHitbox.enabled = true;
    }

    void OnDestroy()
    {
        EnemyManager.UnregisterEnemy(this);
    }
}