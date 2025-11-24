using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Floating Skull – AOE burst caster
/// ALWAYS stays in front of player (world-right offset)
/// PS1-style vertex & material animation only
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class SkullEnemy : MonoBehaviour, IEnemy
{
    public static event Action<SkullEnemy> OnSkullDeath;

    [Header("Core")]
    public EnemyStats stats;

    [Header("Combat")]
    public float health;
    public LayerMask playerLayer;
    public float aoeRadius = 4f;
    public float chargeTime = 1.5f;
    public float aoeCooldown = 3f;

    [Header("Visual")]
    public Renderer skullRenderer;          // jaw + cranium
    public Transform visualRoot;            // whole skull
    public Transform orbPivot;              // empty at mouth – orb scales from here

    [Header("AOE VFX")]
    public GameObject aoeRingPrefab;     // quad ring prefab
    public GameObject chargeSwirlPrefab; // rotating charge effect
    private GameObject activeSwirl;      


    [Header("Terrain Following")]
    public LayerMask groundLayer;
    public float hoverHeight = 2f;
    public float terrainFollowSpeed = 8f;
    public float maxRaycastDist = 20f;

    [Header("Formation Front of Player")]
    public float leadDistance = 7f;       // how far ahead (world right)
    public float formationHeight = 1.8f;
    public float formationLerp = 4f;

    [Header("Animation Tweakables")]
    [Tooltip("Hover bob speed (Hz)")]
    public float bobSpeed = 1.5f;

    [Tooltip("Hover bob height (m)")]
    public float bobAmplitude = 0.5f;

    [Header("Jaw – position open/close")]
    [Tooltip("Local Y offset when mouth is closed")]
    public float jawClosedY = 0f;

    [Tooltip("Local Y offset when mouth is open")]
    public float jawOpenY = -0.1f;

    [Tooltip("How fast the jaw moves to target (Hz)")]
public float jawLerpSpeed = 8f;

    // cached
    private Transform player;
    private Rigidbody playerRb;
    private Transform cachedTransform;
    private MaterialPropertyBlock matProps;
    private float currentGroundY;
    private Vector3 originalScale;

    // state
    private bool isDying;
    private float aoeTimer;
    private float chargeTimer;
    private enum Phase { Idle, Charging, Exploding }
    private Phase phase = Phase.Idle;

    // Animation timers
    private float bobTimer;
    private float jawTimer;

    EnemyStats IEnemy.stats => stats;

    void Awake()
    {
        cachedTransform = transform;
        matProps = new MaterialPropertyBlock();
        originalScale = visualRoot.localScale;
        if (!orbPivot) orbPivot = visualRoot;
    }

    public void Initialize(EnemyStats enemyStats)
    {
        stats = enemyStats;
        health = stats.maxHealth;
        isDying = false;
        phase = Phase.Idle;
        aoeTimer = 0f;
        currentGroundY = cachedTransform.position.y;

        player = GameManager.Instance?.playerController?.transform;
        if (player) playerRb = player.GetComponent<Rigidbody>();
        if (!player) Debug.LogError("SkullEnemy: no player!");

        EnemyManager.RegisterEnemy(this);
        BillboardManager.Billboards.Add(this.transform);
        UpdateGroundHeight();
    }

    void Update()
    {
        if (isDying) { Die(); return; }
        if (!player) return;

        UpdateGroundHeight();
        CodeAnimation();
        StateMachine();
    }

    #region Terrain & Movement
    private void UpdateGroundHeight()
    {
        Vector3 rayStart = cachedTransform.position + Vector3.up * 5f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, maxRaycastDist, groundLayer))
            currentGroundY = Mathf.Lerp(currentGroundY, hit.point.y + hoverHeight,
                                        Time.deltaTime * terrainFollowSpeed);
    }

    private void MoveToFront()
    {
        Vector3 predicted = player.position;
        if (playerRb) predicted += playerRb.velocity * 0.4f;

        Vector3 desired = predicted
                        + Vector3.right * leadDistance
                        + Vector3.up   * formationHeight;

        desired.y = currentGroundY + formationHeight;

        float t = 1f - Mathf.Exp(-formationLerp * Time.deltaTime);
        cachedTransform.position = Vector3.Lerp(cachedTransform.position, desired, t);

        Vector3 dir = player.position - cachedTransform.position;
        if (dir.sqrMagnitude > 0.01f)
            cachedTransform.rotation = Quaternion.Slerp(cachedTransform.rotation,
                                                        Quaternion.LookRotation(dir),
                                                        8f * Time.deltaTime);
    }
    #endregion

    #region State Machine
    private void StateMachine()
    {
        aoeTimer -= Time.deltaTime;

        switch (phase)
        {
            case Phase.Idle:
                MoveToFront();
                // && (player.position - cachedTransform.position).sqrMagnitude <= (aoeRadius * aoeRadius * 2.25f)
                                   
                if (aoeTimer <= 0)
                {
                    phase = Phase.Charging;
                    chargeTimer = chargeTime;
                }
                break;

            case Phase.Charging:
                ChargeOrb();
                if ((chargeTimer -= Time.deltaTime) <= 0)
                    Explode();
                break;

            case Phase.Exploding:
                break;
        }
    }
    #endregion

    #region Attack
    private void ChargeOrb()
    {
        float t = 1f - (chargeTimer / chargeTime);

        // === ORB SCALE PULSE ===
        float pulse = Mathf.Lerp(0.1f, 1.2f, t);
        pulse += Mathf.Sin(Time.time * 30f) * 0.05f; // jitter
        orbPivot.localScale = Vector3.one * pulse;

        // === COLOR SHIFT (White → Magenta → White) ===
        //Color c = Color.Lerp(Color.white, Color.magenta, Mathf.PingPong(t * 2f, 1f));
        //skullRenderer.GetPropertyBlock(matProps);
        //matProps.SetColor("_Color", c);
        //skullRenderer.SetPropertyBlock(matProps);

        // === SWIRL VFX CREATION ===
        if (activeSwirl == null)
        {
            activeSwirl = Instantiate(chargeSwirlPrefab, orbPivot.position, Quaternion.identity, orbPivot);
            activeSwirl.transform.localScale = Vector3.zero;
        }

        // === SWIRL ANIMATION ===
        activeSwirl.transform.localScale = Vector3.one * Mathf.Lerp(0f, 1f, t);
        activeSwirl.transform.Rotate(0f, 0f, -200f * Time.deltaTime);
        BillboardManager.Billboards.Add(activeSwirl.transform);
    }


    private void Explode()
    {
        phase = Phase.Exploding;

        SpawnAOERing();

        if (activeSwirl != null)
        {
            BillboardManager.Billboards.Remove(activeSwirl.transform);
            Destroy(activeSwirl);


        Collider[] hits = Physics.OverlapSphere(cachedTransform.position, aoeRadius, playerLayer);
        foreach (var h in hits)
            if (h.TryGetComponent(out HealthSystem hs))
                hs.ApplyDamage(stats.attackDamage, gameObject);

        StartCoroutine(ExplosionRoutine());
        }
    }


    private IEnumerator ExplosionRoutine()
    {
        float dur = 0.35f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float r = Mathf.Lerp(0.2f, aoeRadius, t / dur);
            orbPivot.localScale = Vector3.one * r;

            skullRenderer.GetPropertyBlock(matProps);
            matProps.SetColor("_Color", new Color(1, 0.5f, 0, 1 - t / dur));
            skullRenderer.SetPropertyBlock(matProps);
            yield return null;
        }
        orbPivot.localScale = Vector3.zero;

        aoeTimer = aoeCooldown;
        phase = Phase.Idle;
    }
    private void SpawnAOERing()
    {
        if (!aoeRingPrefab) return;

        GameObject ring = Instantiate(aoeRingPrefab, cachedTransform.position, Quaternion.identity);
        BillboardManager.Billboards.Add(ring.transform);
        StartCoroutine(AOERingAnim(ring));
    }

    private IEnumerator AOERingAnim(GameObject ring)
    {
        Transform t = ring.transform;
        float dur = 0.4f;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        Renderer r = ring.GetComponent<Renderer>();

        float startScale = 0.1f;
        float endScale = aoeRadius * 2f;

        for (float time = 0; time < dur; time += Time.deltaTime)
        {
            float p = time / dur;

            // scale outward
            float s = Mathf.Lerp(startScale, endScale, p);
            t.localScale = new Vector3(s, s, s);

            // fade out
            float alpha = 1f - p;
            r.GetPropertyBlock(block);
            block.SetColor("_Color", new Color(1f, 0.2f, 0.9f, alpha)); // neon purple
            r.SetPropertyBlock(block);

            // rotate slightly
            t.Rotate(0f, 0f, 90f * Time.deltaTime);

            yield return null;
        }

        Destroy(ring);
        BillboardManager.Billboards.Remove(ring.transform);
    }

    #endregion

    #region Animation
    private void CodeAnimation()
    {
        // Hover bob
        bobTimer += Time.deltaTime * bobSpeed;
        float bob = Mathf.Sin(bobTimer) * bobAmplitude;
        Vector3 rootPos = visualRoot.localPosition;
        rootPos.y = bob;                 // whole skull bobs
        visualRoot.localPosition = rootPos;

        // Jaw open/close (position-based)
        bool open = phase == Phase.Charging;
        float targetY = open ? jawOpenY : jawClosedY;
        Vector3 jawPos = visualRoot.localPosition;
        jawPos.y = Mathf.Lerp(jawPos.y, targetY, Time.deltaTime * jawLerpSpeed);
        visualRoot.localPosition = jawPos;
    }
    #endregion

    #region Damage & Death
    public void TakeDamage(float dmg)
    {
        if (isDying) return;
        health -= dmg;
        StartCoroutine(FlashDamage());
        if (health <= 0) Die();
    }

    private IEnumerator FlashDamage()
    {
        skullRenderer.GetPropertyBlock(matProps);
        matProps.SetColor("_Color", Color.red);
        skullRenderer.SetPropertyBlock(matProps);
        yield return new WaitForSeconds(0.12f);
        matProps.SetColor("_Color", Color.white);
        skullRenderer.SetPropertyBlock(matProps);
    }

    public void Die()
    {
        if (isDying) return;
        isDying = true;
        OnSkullDeath?.Invoke(this);
        EnemyManager.NotifyDeath(this);
        EnemyManager.UnregisterEnemy(this);
        BillboardManager.Billboards.Remove(this.transform);
        StartCoroutine(DeathShrink());
    }

    private IEnumerator DeathShrink()
    {
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
        phase = Phase.Idle;
        aoeTimer = 0;
        visualRoot.localScale = originalScale;
        orbPivot.localScale = Vector3.zero;
        skullRenderer.GetPropertyBlock(matProps);
        matProps.SetColor("_Color", Color.white);
        skullRenderer.SetPropertyBlock(matProps);
    }

    void OnDestroy() => EnemyManager.UnregisterEnemy(this);
    #endregion
}