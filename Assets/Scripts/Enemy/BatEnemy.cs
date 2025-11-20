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
    private Rigidbody playerRb;
    private Transform cachedTransform;
    private Vector3 idleCenter;
    private float swoopTimer;
    private float attackTimer;
    private bool isSwooping;
    private bool isDying;
    private MaterialPropertyBlock matProps; // Avoid material instances
    
    [Header("Formation")]
    public float formationRadius = 4f; // radius of orbit around player
    public float formationArcDegrees = 140f; // arc centered on world-right where bats will prefer to stay
    public float formationHeight = 1.5f; // vertical offset from player
    public float formationLerp = 4f; // how fast bats move into formation
    
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
        BillboardManager.Register(this.transform);
        // Get player reference from GameManager
        player = GameManager.Instance?.playerController?.transform;
        if (player != null)
            playerRb = GameManager.Instance?.playerController?.GetComponent<Rigidbody>();
        
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
            FollowFormation();
        }
    }

    private void IdleFloat()
    {
        // Deprecated - now use FollowFormation for positional behaviour.
        FollowFormation();
    }

    private void FollowFormation()
    {
        // Compute formation positions based on active bats
        var active = EnemyManager.GetActiveEnemies();
        int count = active.Count;
        if (count <= 0 || player == null)
        {
            // fallback to simple idle
            Vector3 floatPos = idleCenter;
            floatPos.y += Mathf.Sin(Time.time * stats.idleFloatSpeed) * stats.idleFloatAmplitude;
            cachedTransform.position = Vector3.Lerp(cachedTransform.position, floatPos, Time.deltaTime * stats.moveSpeed);
            LookAtPlayerSmooth();
            return;
        }

        int idx = active.IndexOf(this);
        if (idx < 0) idx = 0;

        // Arc is centered on world right (Vector3.right). We distribute bats along the arc.
        float arc = Mathf.Clamp(formationArcDegrees, 0f, 360f);
        float half = arc * 0.5f;
        float angleDeg = 0f;
        if (count == 1)
        {
            angleDeg = 0f;
        }
        else
        {
            float step = arc / (count - 1);
            angleDeg = -half + step * idx; // -half => left-most, +half => right-most (relative to world-right center)
        }

        // Convert angle relative to world-right into a direction on XZ plane
        Quaternion rot = Quaternion.AngleAxis(angleDeg, Vector3.up);
        Vector3 dir = rot * Vector3.right; // world-right rotated

        // Lead the formation slightly by player's velocity so bats 'keep up'
        Vector3 lead = Vector3.zero;
        if (playerRb != null)
        {
            lead = new Vector3(playerRb.velocity.x, 0f, playerRb.velocity.z) * 0.35f;
        }

        Vector3 desired = player.position + dir * formationRadius + Vector3.up * formationHeight + lead;

        // Smooth move towards formation position
        float lerpT = 1f - Mathf.Exp(-formationLerp * Time.deltaTime);
        cachedTransform.position = Vector3.Lerp(cachedTransform.position, desired, lerpT * (stats.moveSpeed * 0.5f + 0.5f));

        // gentle look at player
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
        BillboardManager.Billboards.Remove(this.transform);
        
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