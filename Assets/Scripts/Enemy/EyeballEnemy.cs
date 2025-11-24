using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Eyeball — Tracking Beam / Hunter
/// Telegraph: Shows where beam will fire (player can dodge during this)
/// Beam: Fires at locked position, dealing damage once
/// Always stays TOP-LEFT or TOP-RIGHT in front of skating player
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class EyeballEnemy : MonoBehaviour, IEnemy
{
    public static event Action<EyeballEnemy> OnEyeballDeath;

    [Header("Core")]
    public EnemyStats stats;

    [Header("Combat")]
    public float health;
    public LayerMask playerLayer;
    public LayerMask beamHitLayers; // What the beam can hit (player + obstacles)
    public float beamDamage = 15f;
    public float telegraphDuration = 1f;
    public float attackCooldown = 2.5f;
    public float beamDuration = 0.25f;
    public float beamRange = 60f;
    public float beamWidth = 0.5f; // SphereCast radius for easier hits

    [Header("Visual")]
    public Renderer eyeRenderer;
    public Transform visualRoot;
    public LineRenderer line;              // shared LR: telegraph → beam

    [Header("Line Appearance")]
    [ColorUsage(true, true)] public Color telegraphColor = new Color(1, 0, 0, 0.8f);
    public float telegraphWidth = 0.04f;
    [ColorUsage(true, true)] public Color beamCoreColor = new Color(3, 3, 3, 1);
    [ColorUsage(true, true)] public Color beamGlowColor  = new Color(3, 0, 3, 0.6f);
    public float beamCoreWidth = 0.25f;
    public float beamGlowWidth  = 0.7f;

    [Header("Terrain Following")]
    public LayerMask groundLayer;
    public float hoverHeight = 4f;
    public float terrainFollowSpeed = 8f;
    public float maxRaycastDist = 20f;

    [Header("Formation — FRONT-LEFT or FRONT-RIGHT")]
    public float leadDistance = 10f;       // ahead on world-right (movement direction)
    public float sideOffset = 5f;          // left/right spread (perpendicular to movement)
    public float formationLerp = 4f;

    // cached
    private Transform player;
    private Rigidbody playerRb;
    private Transform cachedTransform;
    private MaterialPropertyBlock matProps;
    private Vector3 originalScale;
    private float currentGroundY;

    // state
    private bool isDying;
    private float attackTimer;
    private bool isTelegraphing;
    private float telegraphTimer;
    private bool isBeaming;
    private float beamTimer;
    private bool hasDealtDamageThisBeam; // Prevent multiple damage calls

    // Locked target for beam
    private Vector3 lockedTargetPosition;

    // materials swapped on line
    private Material matTelegraph;
    private Material matBeamCore;
    private Material matBeamGlow;

    EnemyStats IEnemy.stats => stats;

    void Awake()
    {
        cachedTransform = transform;
        matProps = new MaterialPropertyBlock();
        if (visualRoot != null)
            originalScale = visualRoot.localScale;

        CreateLineMaterials();
        SetupLineRenderer();
    }

    #region LineRenderer Setup
    private void CreateLineMaterials()
    {
        // unlit, no-fog, bright PS1 colours
        var shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            Debug.LogWarning("EyeballEnemy: Unlit/Color shader not found, using default");
            shader = Shader.Find("Standard");
        }
        
        matTelegraph  = new Material(shader) { color = telegraphColor };
        matBeamCore   = new Material(shader) { color = beamCoreColor };
        matBeamGlow   = new Material(shader) { color = beamGlowColor };
    }

    private void SetupLineRenderer()
    {
        if (!line) line = GetComponent<LineRenderer>();
        if (!line)
        {
            line = gameObject.AddComponent<LineRenderer>();
            Debug.Log("EyeballEnemy: Added LineRenderer component");
        }
        
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.enabled = false;
        SetTelegraphMode();
    }

    private void SetTelegraphMode()
    {
        line.material = matTelegraph;
        line.startWidth = telegraphWidth;
        line.endWidth   = telegraphWidth;
        line.material.color = telegraphColor;
    }

    private void SetBeamMode()
    {
        // two-line look: core + glow via simple trick — just one line, wide glow material
        line.material = matBeamGlow;
        line.startWidth = beamGlowWidth;
        line.endWidth   = beamGlowWidth;
        line.material.color = beamGlowColor;
        // if you want a true two-pass glow you could duplicate the LR in Awake; overkill for PS1 style
    }
    #endregion

    public void Initialize(EnemyStats enemyStats)
    {
        stats = enemyStats;
        health = stats.maxHealth;
        isDying = false;
        attackTimer = attackCooldown * 0.5f; // Start with half cooldown
        isTelegraphing = false;
        isBeaming = false;
        hasDealtDamageThisBeam = false;

        player = GameManager.Instance?.playerController?.transform;
        if (player) playerRb = player.GetComponent<Rigidbody>();
        
        if (!player)
        {
            Debug.LogError("EyeballEnemy: no player!");
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        EnemyManager.RegisterEnemy(this);
        BillboardManager.Billboards.Add(cachedTransform);
        UpdateGroundHeight();
        
        // Set beamHitLayers to include player if not set
        if (beamHitLayers == 0)
        {
            beamHitLayers = playerLayer;
            Debug.LogWarning("EyeballEnemy: beamHitLayers not set, using playerLayer");
        }
        
        Debug.Log($"EyeballEnemy initialized. PlayerLayer: {playerLayer.value}, BeamHitLayers: {beamHitLayers.value}");
    }

    void Update()
    {
        if (isDying) return;
        if (!player) return;

        UpdateGroundHeight();
        AttackUpdate();
        MoveToCorner();
    }

    #region Movement
    private void UpdateGroundHeight()
    {
        Vector3 rayStart = cachedTransform.position + Vector3.up * 5f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, maxRaycastDist, groundLayer))
            currentGroundY = Mathf.Lerp(currentGroundY, hit.point.y + hoverHeight,
                                        Time.deltaTime * terrainFollowSpeed);
    }

    private void MoveToCorner()
    {
        if (player == null) return;
        
        Vector3 predicted = player.position;
        if (playerRb) predicted += playerRb.velocity * 0.4f;

        // Player moves along world right (Vector3.right)
        // So "left" and "right" (perpendicular to movement) are forward/back
        // We want BOTH eyeballs ahead (on right axis) but spread left/right (forward/back axis)
        
        bool isLeftSide = (GetInstanceID() & 1) == 0;
        Vector3 lateralOffset = isLeftSide ? Vector3.forward : Vector3.back;

        // Position: ahead of player + lateral spread (both in front, spread left-right)
        Vector3 desired = predicted
                        + Vector3.right * leadDistance    // ahead on movement axis
                        + lateralOffset * sideOffset;     // left or right perpendicular to movement
        desired.y = currentGroundY;

        float t = 1f - Mathf.Exp(-formationLerp * Time.deltaTime);
        cachedTransform.position = Vector3.Lerp(cachedTransform.position, desired, t);

        // Always look at player
        Vector3 dir = player.position - cachedTransform.position;
        if (dir.sqrMagnitude > 0.01f)
            cachedTransform.rotation = Quaternion.Slerp(cachedTransform.rotation,
                                                        Quaternion.LookRotation(dir),
                                                        8f * Time.deltaTime);
    }
    #endregion

    #region Attack — Telegraph → Beam
    private void AttackUpdate()
    {
        attackTimer -= Time.deltaTime;

        if (!isTelegraphing && !isBeaming && attackTimer <= 0f)
            StartTelegraph();

        if (isTelegraphing)
        {
            telegraphTimer -= Time.deltaTime;
            UpdateTelegraphLine();
            
            // slow pulse on telegraph line
            float pulse = Mathf.PingPong(Time.time * 3f, 1f);
            line.material.color = Color.Lerp(telegraphColor * 0.5f, telegraphColor, pulse);

            if (telegraphTimer <= 0f)
                StartBeam();
        }

        if (isBeaming)
        {
            beamTimer -= Time.deltaTime;
            UpdateBeamLine();
            
            // Deal damage ONCE at the start of the beam
            if (!hasDealtDamageThisBeam)
            {
                BeamCastDamage();
                hasDealtDamageThisBeam = true;
            }
            
            if (beamTimer <= 0f)
                EndBeam();
        }
    }

    private void StartTelegraph()
    {
        if (player == null) return;
        
        isTelegraphing = true;
        telegraphTimer = telegraphDuration;
        SetTelegraphMode();
        line.enabled = true;
        
        Debug.Log($"EyeballEnemy: Started telegraph. Duration: {telegraphDuration}s");
    }

    private void UpdateTelegraphLine()
    {
        if (player == null) return;
        
        // Show where we're aiming during telegraph (player can see and dodge)
        line.SetPosition(0, cachedTransform.position);
        line.SetPosition(1, player.position);
    }

    private void StartBeam()
    {
        if (player == null) return;
        
        isTelegraphing = false;
        isBeaming = true;
        beamTimer = beamDuration;
        hasDealtDamageThisBeam = false;
        
        // LOCK the target position where player was when telegraph ended
        lockedTargetPosition = player.position;
        
        SetBeamMode();
        line.enabled = true;
        
        Debug.Log($"EyeballEnemy: Firing beam! Locked target at {lockedTargetPosition}");
    }

    private void UpdateBeamLine()
    {
        // Draw beam to LOCKED position (doesn't track during beam)
        Vector3 start = cachedTransform.position;
        Vector3 dir = (lockedTargetPosition - start).normalized;
        Vector3 end = start + dir * beamRange;
        
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private void BeamCastDamage()
    {
        Vector3 start = cachedTransform.position;
        Vector3 dir = (lockedTargetPosition - start).normalized;
        
        Debug.Log($"EyeballEnemy: Casting beam from {start} toward {lockedTargetPosition}");
        Debug.DrawRay(start, dir * beamRange, Color.red, 1f);
        
        // Use SphereCast for more forgiving hits (easier for player to get hit if they don't dodge)
        RaycastHit[] hits = Physics.SphereCastAll(start, beamWidth, dir, beamRange, beamHitLayers);
        
        Debug.Log($"EyeballEnemy: SphereCast hit {hits.Length} objects");
        
        if (hits.Length > 0)
        {
            // Find the closest hit
            RaycastHit closestHit = hits[0];
            float closestDist = Vector3.Distance(start, hits[0].point);
            
            foreach (var hit in hits)
            {
                float dist = Vector3.Distance(start, hit.point);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestHit = hit;
                }
            }
            
            Debug.Log($"EyeballEnemy: Closest hit: {closestHit.collider.name} at distance {closestDist}");
            
            // Try to damage the hit object
            HealthSystem hs = closestHit.collider.GetComponent<HealthSystem>();
            if (hs == null)
            {
                hs = closestHit.collider.GetComponentInParent<HealthSystem>();
            }
            
            if (hs != null)
            {
                bool damaged = hs.ApplyDamage(beamDamage, gameObject);
                Debug.Log($"EyeballEnemy: Beam hit {closestHit.collider.name}! Damage applied: {damaged}, Damage amount: {beamDamage}");
            }
            else
            {
                Debug.LogWarning($"EyeballEnemy: Hit {closestHit.collider.name} but no HealthSystem found!");
            }
            
            // Visual feedback - shorten beam to hit point
            line.SetPosition(1, closestHit.point);
        }
        else
        {
            Debug.Log("EyeballEnemy: Beam cast hit nothing!");
        }
    }

    private void EndBeam()
    {
        isBeaming = false;
        line.enabled = false;
        attackTimer = attackCooldown;
        hasDealtDamageThisBeam = false;
        
        Debug.Log($"EyeballEnemy: Beam ended. Next attack in {attackCooldown}s");
    }

    // cancel on hit
    public void TakeDamage(float dmg)
    {
        if (isDying) return;
        health -= dmg;
        
        Debug.Log($"EyeballEnemy took {dmg} damage. Health: {health}/{stats.maxHealth}");
        
        // Cancel telegraph if hit during windup
        if (isTelegraphing)
        {
            isTelegraphing = false;
            line.enabled = false;
            attackTimer = attackCooldown * 0.5f; // Shorter cooldown after interrupt
            Debug.Log("EyeballEnemy: Telegraph interrupted by damage!");
        }
        
        StartCoroutine(FlashDamage());
        if (health <= 0) Die();
    }

    private IEnumerator FlashDamage()
    {
        if (eyeRenderer != null)
        {
            eyeRenderer.GetPropertyBlock(matProps);
            matProps.SetColor("_Color", Color.red);
            eyeRenderer.SetPropertyBlock(matProps);
            yield return new WaitForSeconds(0.1f);
            matProps.SetColor("_Color", Color.white);
            eyeRenderer.SetPropertyBlock(matProps);
        }
    }
    #endregion

    #region Death & Pooling
    public void Die()
    {
        if (isDying) return;
        isDying = true;
        line.enabled = false;
        
        Debug.Log("EyeballEnemy died!");
        
        OnEyeballDeath?.Invoke(this);
        EnemyManager.NotifyDeath(this);
        EnemyManager.UnregisterEnemy(this);
        BillboardManager.Billboards.Remove(cachedTransform);
        StartCoroutine(DeathShrink());
    }

    private IEnumerator DeathShrink()
    {
        if (visualRoot == null) yield break;
        
        Vector3 orig = visualRoot.localScale;
        for (float t = 0; t < 0.5f; t += Time.deltaTime)
        {
            visualRoot.localScale = Vector3.Lerp(orig, Vector3.zero, t / 0.5f);
            yield return null;
        }
        ReturnToPool();
    }

    private void ReturnToPool() => EnemyPoolManager.Instance.ReturnToPool(this);

    public void ResetForPooling()
    {
        isDying = false;
        health = stats.maxHealth;
        attackTimer = 0f;
        isTelegraphing = false;
        isBeaming = false;
        hasDealtDamageThisBeam = false;
        line.enabled = false;
        
        if (visualRoot != null)
            visualRoot.localScale = originalScale;
            
        if (eyeRenderer != null)
        {
            eyeRenderer.GetPropertyBlock(matProps);
            matProps.SetColor("_Color", Color.white);
            eyeRenderer.SetPropertyBlock(matProps);
        }
    }

    void OnDestroy() => EnemyManager.UnregisterEnemy(this);
    #endregion
}