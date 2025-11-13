using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 0f;
    public float maxMoveSpeed = 10f;
    public float pushForce = 2f;
    public float pushSmoothTime = 0.5f;
    public float pushCooldown = 5f;
    public float jumpForce = 3f;

    [Header("Trick Settings")]
    public float swipeThreshold = 50f;
    public float trickForce = 5f;
    public float rotationSpeed = 360f;

    [Header("Manual Settings")]
    public bool isManual = false;
    public float manualTiltSpeed = 80f;
    public float manualStartAngle = 10f;
    public float maxManualAngle = 25f;
    public float manualSafeRange = 15f;
    public float manualReturnSpeed = 5f;
    public float manualFailTime = 0.5f;

    [Header("Grind Settings")]
    public float grindJumpOffForce = 1.5f;
    public float grindDetectionRadius = 0.3f;
    
    [Header("References")]
    public Transform boardVisual;

    [Header("State")]
    public bool isGrounded;
    public bool canMove = true;
    public bool canPush = true;
    public bool canOllie = true;
    public bool isGrinding = false;

    [Header("Combo System")]
    public int currentCombo = 0;
    public float comboMultiplier = 1f;
    public float comboTimeWindow = 1.5f;

    [Header("Collision Layers")]
    public LayerMask groundLayer;
    public LayerMask grindLayer;
    public float raycastDistance = 0.2f;

    public int maxPushes = 5;
    public float CurrentAirTime => 0f;
    public float CurrentSpeed => 0f;
    public bool LastLandingPerfect => false;

    // Private variables
    private float manualTargetAngle = 0f;
    private float manualFailTimer = 0f;
    private bool isHoldingManual = false;
    private Vector2 initialSwipePos;
    private float lastGroundedTime = 0f;
    private float lastTrickTime;
    private string lastTrickName = "";
    private Quaternion baseLocalRot;
    private Quaternion boardVisualBaseLocalRot;
    private Rigidbody rb;
    private int pushCount = 0;
    private float targetSpeed;
    private Coroutine currentPushRoutine;
    private Vector2 startTouchPos;
    private Vector2 endTouchPos;
    private Collider currentGrindCollider = null;

    // References
    private DollyCam cam;
    private GameManager gameManager;
    private StaminaSystem staminaSystem;
    private ScoreSystem scoreSystem;

    private readonly Dictionary<string, int> trickScores = new Dictionary<string, int>()
    {
        {"Ollie", 100},
        {"Kickflip", 200},
        {"Heelflip", 200},
        {"Pop Shove-It", 250},
        {"Tre Flip", 500},
        {"Backflip", 400},
        {"Frontflip", 400},
        {"Manual", 50},
        {"Grind", 75}
    };

    // ============================================================
    // INITIALIZATION
    // ============================================================

    public void Initialize(GameManager manager)
    {
        gameManager = manager;
        cam = manager.cam;
        staminaSystem = manager.staminaSystem;
        scoreSystem = manager.scoreSystem;
        
        if (staminaSystem == null)
        {
            Debug.LogError("StaminaSystem not found in GameManager!");
        }
        
        if (scoreSystem == null)
        {
            Debug.LogError("ScoreSystem not found in GameManager!");
        }

        rb = GetComponent<Rigidbody>();
        targetSpeed = moveSpeed;
        baseLocalRot = transform.localRotation;
        
        if (boardVisual != null)
        {
            boardVisual.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            boardVisualBaseLocalRot = boardVisual.localRotation;
        }
        else
        {
            boardVisualBaseLocalRot = baseLocalRot;
        }

        ResetState();
    }

    public void ResetState()
    {
        moveSpeed = 0f;
        targetSpeed = 0f;
        currentCombo = 0;
        comboMultiplier = 1f;
        pushCount = 0;
        lastTrickName = "";
        isGrinding = false;
        currentGrindCollider = null;
        
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.rotation = Quaternion.identity;
        if (boardVisual != null)
        {
            boardVisual.localRotation = boardVisualBaseLocalRot;
        }
    }

    void Start()
    {
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
            if (gameManager != null)
            {
                Initialize(gameManager);
            }
            else
            {
                Debug.LogError("No GameManager found in scene!");
            }
        }

        if (boardVisual != null)
        {
            boardVisual.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            boardVisualBaseLocalRot = boardVisual.localRotation;
        }
        else
        {
            boardVisualBaseLocalRot = baseLocalRot;
        }
    }

    // ============================================================
    // UPDATE LOOPS
    // ============================================================

    void Update()
    {
        HandleInput();
        HandleManualInput();
        Grounded();
        CheckGrindStatus();
    }

    private void FixedUpdate()
    {
        Move();
        
        if (isGrounded && !isGrinding)
        {
            CorrectBoardOrientation();
        }
        
        // Score points while grinding
        if (isGrinding)
        {
            ScoreGrindPoints();
        }
    }

    // ============================================================
    // GRIND SYSTEM (SIMPLIFIED)
    // ============================================================

    private void CheckGrindStatus()
    {
        // Only check for grinding when in the air
        if (isGrounded)
        {
            if (isGrinding)
            {
                EndGrind();
            }
            return;
        }

        RaycastHit hit;
        Vector3 rayOrigin = transform.position;
        
        // Check if we're near a grindable surface
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, grindDetectionRadius, grindLayer))
        {
            // Start grinding if we're not already
            if (!isGrinding && rb.velocity.y <= 0.5f)
            {
                StartGrind(hit.collider);
            }
        }
        else if (isGrinding)
        {
            // We've left the grind surface
            EndGrind();
        }
    }

    private void StartGrind(Collider grindCollider)
    {
        if (isGrinding && currentGrindCollider == grindCollider) return;
        
        isGrinding = true;
        currentGrindCollider = grindCollider;
        
        Debug.Log($"Started Grind on: {grindCollider.name}");
        
        if (staminaSystem != null)
        {
            staminaSystem.OnGrindStart();
        }
        
        AddToCombo("Grind", 1);
    }

    private void EndGrind()
    {
        if (!isGrinding) return;
        
        isGrinding = false;
        currentGrindCollider = null;
        
        if (staminaSystem != null)
        {
            staminaSystem.OnGrindEnd();
        }
        
        Debug.Log("Grind Ended");
    }

    private void ScoreGrindPoints()
    {
        if (trickScores.TryGetValue("Grind", out int baseScore))
        {
            float timePoints = Time.fixedDeltaTime * baseScore;
            AddScoreToSystem(Mathf.RoundToInt(timePoints));
        }
    }

    // ============================================================
    // GROUND DETECTION
    // ============================================================

    private void Grounded()
    {
        Vector3 rayOrigin = transform.position;
        RaycastHit hit;
        bool wasGrounded = isGrounded;

        if (staminaSystem != null)
        {
            staminaSystem.SetGrounded(isGrounded);
        }

        // Ground check
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, raycastDistance, groundLayer))
        {
            isGrounded = true;

            if (!wasGrounded)
            {
                lastGroundedTime = Time.time;
                
                if (rb != null)
                {
                    rb.velocity = new Vector3(rb.velocity.x, rb.velocity.y * 0.5f, rb.velocity.z);
                    Vector3 groundNormal = hit.normal;
                    Quaternion landingRotation = Quaternion.FromToRotation(transform.up, groundNormal) * transform.rotation;
                    transform.rotation = Quaternion.Slerp(transform.rotation, landingRotation, 0.5f);
                }

                // Finalize combo on landing
                bool perfectLanding = DeterminePerfectLanding();
                
                if (scoreSystem != null)
                {
                    scoreSystem.FinalizeCombo(perfectLanding);
                }
                else if (currentCombo > 0)
                {
                    FinalizeCombo();
                }
                
                // Reward perfect landing with stamina bonus
                if (perfectLanding && staminaSystem != null)
                {
                    staminaSystem.OnPerfectLand();
                }
                
                StartCoroutine(RealignVisualOnLand(0.25f));
                if (cam != null) cam.SetIgnoreRotation(false);
            }
        }
        else
        {
            isGrounded = false;
        }
    }

    private bool DeterminePerfectLanding()
    {
        float angle = Vector3.Angle(transform.up, Vector3.up);
        float landingSpeed = Mathf.Abs(rb.velocity.y);
        bool perfect = angle < 15f && landingSpeed < 8f;
        
        if (perfect)
        {
            Debug.Log("✨ PERFECT LANDING!");
        }
        
        return perfect;
    }

    // ============================================================
    // MOVEMENT
    // ============================================================

    private void Move()
    {
        if (!canMove) return;
        
        Vector3 horizontal = Vector3.right * moveSpeed;
        Vector3 newVel = new Vector3(horizontal.x, rb.velocity.y, horizontal.z);
        rb.velocity = newVel;
    }

    private void CorrectBoardOrientation()
    {
        if (rb == null) return;

        Vector3 currentUp = transform.up;
        float upDot = Vector3.Dot(currentUp, Vector3.up);
        
        if (upDot < 0.95f)
        {
            Quaternion targetRotation = Quaternion.FromToRotation(currentUp, Vector3.up) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 5f);
            rb.angularVelocity = Vector3.zero;
            Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            rb.velocity = horizontalVelocity + Vector3.up * rb.velocity.y;
        }
    }

    void Push()
    {
        if (!canPush) return;

        if (pushCount >= maxPushes)
        {
            StartCoroutine(PushCooldown(pushCooldown));
            return;
        }

        if (staminaSystem != null)
        {
            staminaSystem.OnPush();
        }

        pushCount++;
        targetSpeed = Mathf.Min(targetSpeed + pushForce, maxMoveSpeed);

        if (currentPushRoutine != null)
            StopCoroutine(currentPushRoutine);

        currentPushRoutine = StartCoroutine(SmoothPush());
    }

    // ============================================================
    // INPUT HANDLING
    // ============================================================

    void HandleInput()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseInput();
#else
        HandleTouchInput();
#endif
    }

    void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
            startTouchPos = Input.mousePosition;

        if (Input.GetMouseButtonUp(0))
        {
            endTouchPos = Input.mousePosition;
            Vector2 swipeDelta = endTouchPos - startTouchPos;

            if (swipeDelta.magnitude < swipeThreshold)
            {
                if (isGrounded) Push();
                return;
            }

            float x = swipeDelta.x;
            float y = swipeDelta.y;

            if (Mathf.Abs(y) > Mathf.Abs(x))
            {
                if (y > 0)
                {
                    if (isGrounded || isGrinding) Ollie();
                    else Backflip();
                }
                else
                {
                    if (!isGrounded && !isGrinding) Frontflip();
                }
            }
            else
            {
                if (x > 0) Kickflip();
                else Heelflip();
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            if (Random.value > 0.5f) PopShoveIt();
            else TreFlip();
        }
    }

    void HandleTouchInput()
    {
        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);

        switch (touch.phase)
        {
            case TouchPhase.Began:
                startTouchPos = touch.position;
                break;

            case TouchPhase.Ended:
                endTouchPos = touch.position;
                Vector2 swipeDelta = endTouchPos - startTouchPos;

                if (swipeDelta.magnitude < swipeThreshold)
                {
                    Push();
                    return;
                }

                float x = swipeDelta.x;
                float y = swipeDelta.y;

                if (Mathf.Abs(y) > Mathf.Abs(x))
                {
                    if (y > 0) Ollie();
                }
                else
                {
                    if (x > 0) Kickflip();
                    else Heelflip();
                }
                break;
        }
    }

    // ============================================================
    // MANUAL SYSTEM
    // ============================================================

    void HandleManualInput()
    {
        if (!isManual)
        {
            if (Input.GetMouseButtonDown(0))
                initialSwipePos = Input.mousePosition;

            if (Input.GetMouseButton(0))
            {
                Vector2 swipeDelta = (Vector2)Input.mousePosition - initialSwipePos;
                if (swipeDelta.y < -30f && isGrounded)
                {
                    StartManual();
                }
            }

            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began) initialSwipePos = t.position;
                if (!isManual && t.phase == TouchPhase.Moved)
                {
                    Vector2 swipeDelta = t.position - initialSwipePos;
                    if (swipeDelta.y < -30f && isGrounded)
                    {
                        StartManual();
                    }
                }
            }
        }

        if (isManual)
        {
            Vector2 movementDelta = Vector2.zero;
            bool stillHolding = false;

            if (Input.GetMouseButton(0))
            {
                stillHolding = true;
                movementDelta.y = Input.GetAxis("Mouse Y");
            }

            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Moved)
                {
                    stillHolding = true;
                    movementDelta.y = t.deltaPosition.y / 10f;
                }
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    stillHolding = false;
                }
            }

            if (!stillHolding)
            {
                EndManual();
                return;
            }

            manualTargetAngle += -movementDelta.y * manualTiltSpeed * Time.deltaTime;
            manualTargetAngle = Mathf.Clamp(manualTargetAngle, -maxManualAngle, maxManualAngle);
            ApplyManualRotation(manualTargetAngle);

            if (Mathf.Abs(manualTargetAngle) > manualSafeRange)
            {
                manualFailTimer += Time.deltaTime;
                if (manualFailTimer >= manualFailTime)
                    EndManual(true);
            }
            else
            {
                manualFailTimer = 0f;
                if (trickScores.TryGetValue("Manual", out int baseScore))
                {
                    float timePoints = Time.deltaTime * baseScore;
                    AddScoreToSystem(Mathf.RoundToInt(timePoints));
                }
            }
        }
    }

    void StartManual()
    {
        if (cam != null) cam.SetIgnoreRotation(true);
        if (isManual) return;

        isManual = true;
        canOllie = false;
        manualFailTimer = 0f;
        isHoldingManual = true;
        manualTargetAngle = manualStartAngle;

        StopAllCoroutines();
        StartCoroutine(SmoothApplyManualTarget(manualStartAngle, 0.15f));

        Debug.Log("Started Manual");
    }

    void EndManual(bool failed = false)
    {
        if (cam != null) cam.SetIgnoreRotation(false);
        if (!isManual) return;

        isManual = false;
        canOllie = true;
        isHoldingManual = false;
        manualTargetAngle = 0f;
        manualFailTimer = 0f;

        StopAllCoroutines();
        StartCoroutine(SmoothApplyManualTarget(0f, 0.2f));

        Debug.Log(failed ? "Manual Failed - Lost Balance!" : "Manual Ended");
    }

    void ApplyManualRotation(float pitchDegrees)
    {
        if (boardVisual != null)
            boardVisual.localRotation = boardVisualBaseLocalRot * Quaternion.Euler(pitchDegrees, 0f, 0f);
        else
            transform.localRotation = baseLocalRot * Quaternion.Euler(pitchDegrees, 0f, 0f);
    }

    float QuaternionToPitch(Quaternion currentLocal, Quaternion baseLocal)
    {
        Quaternion relative = Quaternion.Inverse(baseLocal) * currentLocal;
        Vector3 euler = relative.eulerAngles;
        float pitch = euler.x;
        if (pitch > 180f) pitch -= 360f;
        return pitch;
    }

    // ============================================================
    // TRICKS
    // ============================================================

    void Ollie()
    {
        if (!canOllie || (!isGrounded && !isGrinding)) return;
        
        if (staminaSystem != null && !staminaSystem.HasEnoughStamina(staminaSystem.jumpCost))
        {
            Debug.Log("Not enough stamina to Ollie!");
            return;
        }
        
        if (staminaSystem != null)
        {
            staminaSystem.OnJump();
        }

        float finalJumpForce = jumpForce;
        if (isGrinding)
        {
            EndGrind();
            finalJumpForce *= grindJumpOffForce;
        }

        rb.AddForce(Vector3.up * finalJumpForce, ForceMode.Impulse);
        Debug.Log("Ollie!");
    }

    void Kickflip()
    {
        if (staminaSystem != null && !staminaSystem.HasEnoughStamina(staminaSystem.basicTrickCost))
        {
            Debug.Log("Not enough stamina for Kickflip!");
            return;
        }
        
        if (isGrounded || isGrinding)
        {
            if (isGrinding) EndGrind();
            rb.AddForce(Vector3.up * jumpForce * 0.6f, ForceMode.Impulse);
        }

        if (Time.time - lastTrickTime < 0.3f && lastTrickName == "Kickflip")
        {
            StartFlip("Kickflip", Vector3.right, false, 2);
        }
        else
        {
            StartFlip("Kickflip", Vector3.right);
        }
    }

    void Heelflip()
    {
        if (staminaSystem != null && !staminaSystem.HasEnoughStamina(staminaSystem.basicTrickCost))
        {
            Debug.Log("Not enough stamina for Heelflip!");
            return;
        }
        
        if (isGrounded || isGrinding)
        {
            if (isGrinding) EndGrind();
            rb.AddForce(Vector3.up * jumpForce * 0.6f, ForceMode.Impulse);
        }

        if (Time.time - lastTrickTime < 0.3f && lastTrickName == "Heelflip")
        {
            StartFlip("Heelflip", Vector3.left, false, 2);
        }
        else
        {
            StartFlip("Heelflip", Vector3.left);
        }
    }

    void Backflip()
    {
        if (staminaSystem != null && !staminaSystem.HasEnoughStamina(staminaSystem.complexTrickCost))
        {
            Debug.Log("Not enough stamina for Backflip!");
            return;
        }
        
        if (Time.time - lastTrickTime < 0.3f && lastTrickName == "Backflip")
        {
            StartFlip("Backflip", Vector3.right, true, 2);
        }
        else
        {
            StartFlip("Backflip", Vector3.right, true);
        }
    }

    void Frontflip()
    {
        if (staminaSystem != null && !staminaSystem.HasEnoughStamina(staminaSystem.complexTrickCost))
        {
            Debug.Log("Not enough stamina for Frontflip!");
            return;
        }
        
        if (Time.time - lastTrickTime < 0.3f && lastTrickName == "Frontflip")
        {
            StartFlip("Frontflip", Vector3.left, true, 2);
        }
        else
        {
            StartFlip("Frontflip", Vector3.left, true);
        }
    }

    void PopShoveIt()
    {
        if (staminaSystem != null && !staminaSystem.HasEnoughStamina(staminaSystem.basicTrickCost))
        {
            Debug.Log("Not enough stamina for Pop Shove-It!");
            return;
        }
        
        if (isGrounded || isGrinding)
        {
            if (isGrinding) EndGrind();
            rb.AddForce(Vector3.up * jumpForce * 0.6f, ForceMode.Impulse);
        }

        Debug.Log("Pop Shove-It!");
        
        if (staminaSystem != null)
        {
            staminaSystem.OnTrickStart(false);
        }
        
        if (cam != null)
        {
            cam.SetIgnoreRotation(true);
            cam.TriggerCameraShake();
        }
        
        AddToCombo("Pop Shove-It", 1);
        StartCoroutine(VisualSpinRoutine(180, "Pop Shove-It"));
    }

    void TreFlip()
    {
        if (staminaSystem != null && !staminaSystem.HasEnoughStamina(staminaSystem.complexTrickCost))
        {
            Debug.Log("Not enough stamina for Tre Flip!");
            return;
        }
        
        if (isGrounded || isGrinding)
        {
            if (isGrinding) EndGrind();
            rb.AddForce(Vector3.up * jumpForce * 0.6f, ForceMode.Impulse);
        }

        Debug.Log("360 Flip (Tre Flip)!");
        
        if (staminaSystem != null)
        {
            staminaSystem.OnTrickStart(true);
        }
        
        if (cam != null)
        {
            cam.SetIgnoreRotation(true);
            cam.TriggerCameraShake();
        }
        
        AddToCombo("Tre Flip", 1);
        StartCoroutine(VisualSpinRoutine(360, "Tre Flip"));
    }

    private void StartFlip(string trickName, Vector3 axis, bool isXAxisRotation = false, int flips = 1)
    {
        if (staminaSystem != null)
        {
            bool isComplexTrick = flips > 1 || trickName.Contains("Tre") || isXAxisRotation;
            staminaSystem.OnTrickStart(isComplexTrick);
        }

        if (isGrounded || isGrinding)
        {
            if (isGrinding) EndGrind();
            rb.AddForce(Vector3.up * jumpForce * (0.8f + (flips * 0.2f)), ForceMode.Impulse);
        }

        string multipleTrickName = flips > 1 ? $"{flips}x {trickName}" : trickName;
        Debug.Log($"{multipleTrickName}!");

        if (cam != null)
        {
            cam.SetIgnoreRotation(true);
            cam.TriggerCameraShake();
        }
        
        StartCoroutine(VisualFlipRoutine(axis, multipleTrickName, isXAxisRotation, flips));
        AddToCombo(trickName, flips);
    }

    // ============================================================
    // COMBO & SCORING
    // ============================================================

    private void AddToCombo(string trickName, int multiplier = 1)
    {
        float currentTime = Time.time;
        
        if (currentTime - lastTrickTime <= comboTimeWindow)
        {
            if (trickName != lastTrickName)
            {
                currentCombo++;
                comboMultiplier = 1f + (currentCombo * 0.5f);
            }
        }
        else
        {
            currentCombo = 1;
            comboMultiplier = 1f;
        }

        if (trickScores.TryGetValue(trickName, out int baseScore))
        {
            int scoreAdd = Mathf.RoundToInt(baseScore * comboMultiplier * multiplier);
            AddScoreToSystem(scoreAdd);
            
            Debug.Log($"{trickName} x{multiplier} (Combo x{comboMultiplier:F1}) = +{scoreAdd} points!");
            
            if (UIManager.Instance != null)
            {
                Vector3 textPosition = transform.position + Vector3.up * 1.5f;
                UIManager.Instance.SpawnTrickText(trickName, textPosition, multiplier, comboMultiplier);
            }
        }
        else
        {
            Debug.LogWarning($"No score defined for trick: {trickName}");
            int defaultScore = 100;
            int scoreAdd = Mathf.RoundToInt(defaultScore * comboMultiplier * multiplier);
            AddScoreToSystem(scoreAdd);
        }

        lastTrickName = trickName;
        lastTrickTime = currentTime;
    }

    private void FinalizeCombo()
    {
        if (currentCombo > 1)
        {
            Debug.Log($"Combo Complete! {currentCombo} tricks - Final Multiplier: x{comboMultiplier:F1}");
        }
        currentCombo = 0;
        comboMultiplier = 1f;
        lastTrickName = "";
    }

    private void AddScoreToSystem(int points)
    {
        if (scoreSystem != null)
        {
            scoreSystem.AddScore(points);
        }
    }

    // ============================================================
    // COROUTINES
    // ============================================================

    IEnumerator SmoothApplyManualTarget(float targetAngle, float duration)
    {
        float startAngle;
        if (boardVisual != null)
            startAngle = QuaternionToPitch(boardVisual.localRotation, boardVisualBaseLocalRot);
        else
            startAngle = QuaternionToPitch(transform.localRotation, baseLocalRot);
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float angle = Mathf.Lerp(startAngle, targetAngle, elapsed / duration);
            ApplyManualRotation(angle);
            yield return null;
        }
        ApplyManualRotation(targetAngle);
    }

    IEnumerator VisualFlipRoutine(Vector3 axis, string trickName, bool isXAxisRotation = false, int flips = 1)
    {
        if (boardVisual == null)
        {
            Debug.LogWarning("BoardVisual not assigned – visual flip will rotate the root.");
        }

        float duration = 0.5f * flips;
        float totalRotation = 0f;
        float targetRotation = 360f * flips;
        float currentRotationSpeed = rotationSpeed * (flips > 1 ? 1.5f : 1f);

        while (totalRotation < targetRotation)
        {
            float delta = currentRotationSpeed * Time.deltaTime;
            totalRotation += delta;

            if (boardVisual != null)
            {
                if (isXAxisRotation)
                {
                    boardVisual.Rotate(Vector3.right * delta * (axis.x > 0 ? 1 : -1), Space.Self);
                }
                else
                {
                    boardVisual.Rotate(Vector3.forward * delta * (axis.x > 0 ? 1 : -1), Space.Self);
                }
            }
            else
            {
                if (isXAxisRotation)
                {
                    transform.Rotate(Vector3.right * delta * (axis.x > 0 ? 1 : -1), Space.Self);
                }
                else
                {
                    transform.Rotate(axis * delta, Space.Self);
                }
            }
            yield return null;
        }

        Debug.Log($"{trickName} completed ({flips}x flip, {totalRotation:F1} degrees)");
    }

    IEnumerator VisualSpinRoutine(float degrees, string trickName)
    {
        if (boardVisual == null)
        {
            Debug.LogWarning("BoardVisual not assigned – visual spin will rotate the root.");
        }

        float spun = 0f;
        while (spun < degrees)
        {
            float delta = rotationSpeed * Time.deltaTime;
            if (boardVisual != null)
                boardVisual.Rotate(Vector3.up * delta, Space.Self);
            else
                transform.Rotate(Vector3.up * delta, Space.Self);
            spun += delta;
            yield return null;
        }

        Debug.Log($"{trickName} visual finished");
    }

    IEnumerator RealignVisualOnLand(float duration)
    {
        if (boardVisual == null) yield break;

        Quaternion start = boardVisual.localRotation;
        Quaternion targetLocal = boardVisualBaseLocalRot;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            boardVisual.localRotation = Quaternion.Slerp(start, targetLocal, t);
            yield return null;
        }

        boardVisual.localRotation = targetLocal;
    }

    IEnumerator PushCooldown(float duration)
    {
        canPush = false;
        yield return new WaitForSeconds(duration);
        pushCount = 0;
        canPush = true;
    }

    IEnumerator SmoothPush()
    {
        float initialSpeed = moveSpeed;
        float elapsed = 0f;

        while (elapsed < pushSmoothTime)
        {
            elapsed += Time.deltaTime;
            moveSpeed = Mathf.Lerp(initialSpeed, targetSpeed, elapsed / pushSmoothTime);
            yield return null;
        }

        moveSpeed = targetSpeed;
    }

    // ============================================================
    // PUBLIC METHODS
    // ============================================================

    public void ResetForNewLevel() 
    {
        ResetState();
    }
}