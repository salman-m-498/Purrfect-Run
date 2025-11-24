using UnityEngine;
using System;
using Unity.VisualScripting;

/// <summary>
/// Optimized bat enemy with pooling support, hitboxes, and death animations
/// Modified to stay in front of fast-moving player (skating game)
/// Includes terrain following for sloped courses
/// </summary>
public class BatEnemy : MonoBehaviour, IEnemy
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
    
    [Header("Terrain Following")]
    public LayerMask groundLayer;
    public float hoverHeight = 2f; // Height above ground
    public float maxRaycastDistance = 20f; // How far to check for ground
    public float terrainFollowSpeed = 8f; // How fast to adjust to terrain changes
    public bool debugRaycast = false; // Show raycast in scene view
    
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
    private float currentGroundHeight; // Cached ground height
    
    [Header("Formation - Front of Player")]
    public float formationDistance = 8f; // distance ahead of player
    public float formationWidth = 6f; // horizontal spread
    public float formationHeight = 1.5f; // vertical offset from ground
    public float formationLerp = 3f; // how fast bats move into formation
    public float velocityPrediction = 0.5f; // how far ahead to predict player position

    [Header("Catch-Up System")]
    public float catchUpDistance = 40f;      // how far behind before bats boost
    public float catchUpSpeedMultiplier = 3f; // speed boost
    public float catchUpHeight = 2f;          // hover height during boost

    
    // Death animation
    private float deathTimer;
    private Vector3 originalScale;
    private const float DEATH_DURATION = 0.5f;

    EnemyStats IEnemy.stats => stats;

    
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
        currentGroundHeight = cachedTransform.position.y;
        
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
        
        // Initial ground check
        UpdateGroundHeight();
    }

    void Update()
    {
        if (isDying)
        {
            UpdateDeathAnimation();
            return;
        }
        
        if (player == null) return;
        
        UpdateGroundHeight();
        CodeBasedAnimation();
        StateMachine();
    }

    private void UpdateGroundHeight()
    {
        // Raycast downward to find ground
        RaycastHit hit;
        Vector3 rayStart = cachedTransform.position + Vector3.up * 5f; // Start raycast above current position
        
        if (Physics.Raycast(rayStart, Vector3.down, out hit, maxRaycastDistance, groundLayer))
        {
            // Smoothly adjust to new ground height
            float targetHeight = hit.point.y + hoverHeight;
            currentGroundHeight = Mathf.Lerp(currentGroundHeight, targetHeight, Time.deltaTime * terrainFollowSpeed);
            
            if (debugRaycast)
            {
                Debug.DrawLine(rayStart, hit.point, Color.green);
            }
        }
        else
        {
            if (debugRaycast)
            {
                Debug.DrawLine(rayStart, rayStart + Vector3.down * maxRaycastDistance, Color.red);
            }
        }
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
        if (CatchUpToPlayer())
        return; // skip all normal AI until caught up

        if (isSwooping)
        {
            SwoopAttack();
        }
        else
        {
            FollowFormation();
        }
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
            floatPos.y = currentGroundHeight + Mathf.Sin(Time.time * stats.idleFloatSpeed) * stats.idleFloatAmplitude;
            cachedTransform.position = Vector3.Lerp(cachedTransform.position, floatPos, Time.deltaTime * stats.moveSpeed);
            LookAtPlayerSmooth();
            return;
        }

        int idx = active.IndexOf(this);
        if (idx < 0) idx = 0;

        // PREDICT where player will be based on velocity
        Vector3 predictedPos = player.position;
        if (playerRb != null)
        {
            predictedPos += playerRb.velocity * velocityPrediction;
        }

        // Player moves on world right (Vector3.right), so we place enemies AHEAD on that axis
        Vector3 forwardOffset = Vector3.right * formationDistance;

        // Distribute bats horizontally (forward/back relative to movement direction)
        // and vertically (up/down)
        float horizontalSpacing = 0f;
        float verticalOffset = 0f;
        
        if (count == 1)
        {
            // Single bat: dead center in front
            horizontalSpacing = 0f;
            verticalOffset = formationHeight;
        }
        else
        {
            // Multiple bats: spread them in a grid pattern
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            int row = idx / cols;
            int col = idx % cols;
            
            // Center the grid
            float colOffset = (col - (cols - 1) * 0.5f) * (formationWidth / cols);
            float rowOffset = row * 1.5f; // slight depth variation
            
            // Horizontal is perpendicular to movement (world forward for lateral spread)
            horizontalSpacing = colOffset;
            verticalOffset = formationHeight + rowOffset * 0.5f;
        }

        // Calculate final position: ahead of player + horizontal spread
        Vector3 lateralOffset = Vector3.forward * horizontalSpacing;
        
        // Use terrain-following height instead of fixed height
        Vector3 desired = predictedPos + forwardOffset + lateralOffset;
        
        // Check ground at desired XZ position
        RaycastHit hit;
        float desiredHeight = currentGroundHeight + verticalOffset;
        if (Physics.Raycast(desired + Vector3.up * 10f, Vector3.down, out hit, 50f, groundLayer))
        {
            desiredHeight = hit.point.y + hoverHeight + verticalOffset;
        }
        
        desired.y = desiredHeight;

        // Smooth move towards formation position with faster response for skating speed
        float lerpT = 1f - Mathf.Exp(-formationLerp * Time.deltaTime);
        Vector3 newPos = Vector3.Lerp(
            cachedTransform.position, 
            desired, 
            lerpT * (stats.moveSpeed * 1.5f) // Increased multiplier for fast skating
        );
        
        // Ensure Y position follows terrain smoothly
        newPos.y = Mathf.Lerp(cachedTransform.position.y, desiredHeight, Time.deltaTime * terrainFollowSpeed);
        
        cachedTransform.position = newPos;

        // Look at player
        LookAtPlayerSmooth();
    }

    private void SwoopAttack()
    {
        // Swoop toward predicted player position
        Vector3 targetPos = player.position;
        if (playerRb != null)
        {
            targetPos += playerRb.velocity * 0.3f;
        }

        // Maintain terrain following even during swoop
        RaycastHit hit;
        if (Physics.Raycast(targetPos + Vector3.up * 10f, Vector3.down, out hit, 50f, groundLayer))
        {
            targetPos.y = hit.point.y + hoverHeight * 0.5f; // Lower hover during attack
        }
        else
        {
            targetPos.y = currentGroundHeight + hoverHeight * 0.5f;
        }

        cachedTransform.position = Vector3.MoveTowards(
            cachedTransform.position,
            targetPos,
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

    private bool CatchUpToPlayer()
    {
        // Check if the bat is far behind the player on the world X axis (player moves right)
        float dx = player.position.x - cachedTransform.position.x;

        if (dx > catchUpDistance)
        {
            // Target = directly behind player
            Vector3 target = player.position - Vector3.right * 5f;

            // Keep terrain height
            RaycastHit hit;
            if (Physics.Raycast(target + Vector3.up * 10f, Vector3.down, out hit, 50f, groundLayer))
                target.y = hit.point.y + catchUpHeight;
            else
                target.y = currentGroundHeight + catchUpHeight;

            cachedTransform.position = Vector3.MoveTowards(
                cachedTransform.position,
                target,
                stats.moveSpeed * catchUpSpeedMultiplier * Time.deltaTime
            );

            LookAtPlayerSmooth();
            return true;    // we are catching up (skip normal logic)
        }

        return false;       // not catching up
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
        EnemyManager.NotifyDeath(this);
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