using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    public float swipeThreshold = 50f; // min swipe distance (for touch/mouse)
    public float trickForce = 5f;      // how much lift force a trick adds
    public float rotationSpeed = 360f; // spin speed during flip tricks

    [Header("Manual Settings")]
    public bool isManual = false;
    public float manualTiltSpeed = 80f;       // degrees per second for balancing input
    public float manualStartAngle = 10f;      // initial nose-up when manual starts (positive = nose-up)
    public float maxManualAngle = 25f;        // absolute limit before immediate fail
    public float manualSafeRange = 15f;       // safe balance range before fail timer starts
    public float manualReturnSpeed = 5f;      // how fast it returns to base when not manual
    public float manualFailTime = 0.5f;       // how long outside safe range before fail

    private float manualTargetAngle = 0f;     // desired pitch (degrees)
    private float manualFailTimer = 0f;
    private bool isHoldingManual = false;
    private Vector2 initialSwipePos;

    private DollyCam cam;
    private ComboTextSpawner comboTextSpawner;
    private GameManager gameManager;
    private StaminaSystem staminaSystem;

    [Header("References")]
    // Visual child transform that represents the board model. This will be rotated for tricks
    // while the parent (this GameObject) handles physics and movement.
    public Transform boardVisual;

    // store the base (rest) local rotation so we always apply pitch relative to it
    private Quaternion baseLocalRot;
    // base local rotation for the visual child (if used)
    private Quaternion boardVisualBaseLocalRot;

    [Header("State")]
    public bool isGrounded;
    public bool canMove = true;
    public bool canPush = true;
    public bool canOllie = true;
    public bool isGrinding = false;

    [Header("Combo System")]
    public int currentCombo = 0;
    public float comboMultiplier = 1f;
    public float comboTimeWindow = 1.5f;  // time window to chain tricks
    private float lastTrickTime;
    private string lastTrickName = "";
    public int totalScore = 0;
    
    // Base scores for tricks
    private readonly Dictionary<string, int> trickScores = new Dictionary<string, int>()
    {
        {"Ollie", 100},
        {"Kickflip", 200},
        {"Heelflip", 200},
        {"Pop Shove-It", 250},
        {"Tre Flip", 500},
        {"Backflip", 400},
        {"Frontflip", 400},
        {"Manual", 50}  // per second
    };

    public LayerMask groundLayer;
    public LayerMask grindLayer;
    public float raycastDistance = 0.2f;

    private Rigidbody rb;
    private int pushCount = 0;
    public int maxPushes = 5;

    private float targetSpeed;
    private Coroutine currentPushRoutine;

    private Vector2 startTouchPos;
    private Vector2 endTouchPos;

    public void Initialize(GameManager manager)
    {
        gameManager = manager;
        cam = manager.dollyCam;
        comboTextSpawner = manager.comboTextSpawner;
        boardVisual = manager.boardVisual;
        staminaSystem = FindObjectOfType<StaminaSystem>();
        
        if (staminaSystem == null)
        {
            Debug.LogError("StaminaSystem not found in scene!");
        }

        rb = GetComponent<Rigidbody>();
        targetSpeed = moveSpeed;
        baseLocalRot = transform.localRotation;
        
        // Initialize base rotations
        if (boardVisual != null)
        {
            boardVisualBaseLocalRot = boardVisual.localRotation;
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
        totalScore = 0;
        pushCount = 0;
        lastTrickName = "";
        
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
        // If not initialized by GameManager, try to find one
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
            // Store the initial mesh orientation as our base - this preserves the model's intended facing
            boardVisualBaseLocalRot = boardVisual.localRotation;
            // Make sure the mesh is facing the movement direction (right) at start
            boardVisual.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            // Store this aligned rotation as our new base
            boardVisualBaseLocalRot = boardVisual.localRotation;
            Debug.Log($"Initial visual rotation: {boardVisual.localEulerAngles}");
        }
        else
        {
            boardVisualBaseLocalRot = baseLocalRot;
        }
    }

    void Update()
    {
        HandleInput();
        HandleManualInput();
        Grounded();
    }

    private void FixedUpdate()
    {
        Move();
        if (isGrounded)
        {
            CorrectBoardOrientation();
        }
    }

    private void CorrectBoardOrientation()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) return;

        // Get current up vector in world space
        Vector3 currentUp = transform.up;
        float upDot = Vector3.Dot(currentUp, Vector3.up);
        
        // Check if we're significantly tilted
        if (upDot < 0.95f) // about 18 degrees or more from vertical
        {
            // Calculate rotation to upright position
            Quaternion targetRotation = Quaternion.FromToRotation(currentUp, Vector3.up) * transform.rotation;
            
            // Smoothly rotate back to upright
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 5f);
            
            // Zero out angular velocity to prevent fighting the correction
            rb.angularVelocity = Vector3.zero;
            
            // Ensure we maintain forward momentum
            Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            rb.velocity = horizontalVelocity + Vector3.up * rb.velocity.y;
        }
    }

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
                // small click = push if grounded
                if (isGrounded) Push();
                return;
            }

            // Determine swipe direction
            float x = swipeDelta.x;
            float y = swipeDelta.y;

            if (Mathf.Abs(y) > Mathf.Abs(x))
            {
                if (y > 0)
                {
                    if (isGrounded)
                    {
                        // Up swipe on ground = Ollie
                        Ollie();
                    }
                    else
                    {
                        // Up swipe in air = Backflip
                        Backflip();
                    }
                }
                else
                {
                    if (isGrounded)
                    {
                        //Manual(); REWRITE THIS LATER
                    }
                    else
                    {
                        // Down swipe in air = Frontflip
                        Frontflip();
                    }
                }
            }
            else
            {
                if (x > 0)
                {
                    // Right swipe = Kickflip (ground or air)
                    Kickflip();
                }
                else
                {
                    // Left swipe = Heelflip (ground or air)
                    Heelflip();
                }
            }
        }

        // Right-click for shove-it / tre flip (ground or air)
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
                    //else Manual();
                }
                else
                {
                    if (x > 0) Kickflip();
                    else Heelflip();
                }
                break;
        }
    }

    // ---------- MANUAL START / END ----------
    void StartManual()
    {
        cam.SetIgnoreRotation(true);
        if (isManual) return;

        isManual = true;
        canOllie = false;
        manualFailTimer = 0f;
        isHoldingManual = true;

        // start with a nose-up angle (positive = nose-up)
        manualTargetAngle = manualStartAngle;

        // optionally smooth into start pose by lerping for a short time
        StopAllCoroutines(); // stop previous coroutines that might conflict
        StartCoroutine(SmoothApplyManualTarget(manualStartAngle, 0.15f));

        Debug.Log("Started Manual");
    }

    void EndManual(bool failed = false)
    {
        cam.SetIgnoreRotation(false);
        if (!isManual) return;

        isManual = false;
        canOllie = true;
        isHoldingManual = false;
        manualTargetAngle = 0f;
        manualFailTimer = 0f;

        // smoothly return to base rotation
        StopAllCoroutines();
        StartCoroutine(SmoothApplyManualTarget(0f, 0.2f));

        if (failed)
        {
            Debug.Log("Manual Failed - Lost Balance!");
            // add bail logic here
        }
        else
        {
            Debug.Log("Manual Ended");
        }
    }

    // ---------- SMOOTH APPLY COROUTINE ----------
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

    // helper to apply pitch relative to base rotation
    void ApplyManualRotation(float pitchDegrees)
    {
        if (boardVisual != null)
            boardVisual.localRotation = boardVisualBaseLocalRot * Quaternion.Euler(pitchDegrees, 0f, 0f);
        else
            transform.localRotation = baseLocalRot * Quaternion.Euler(pitchDegrees, 0f, 0f);
    }

    // helper to get current pitch relative to base rotation (reads back the pitch in degrees)
    float QuaternionToPitch(Quaternion currentLocal, Quaternion baseLocal)
    {
        Quaternion relative = Quaternion.Inverse(baseLocal) * currentLocal;
        Vector3 euler = relative.eulerAngles;
        // convert from 0..360 to -180..180 for X
        float pitch = euler.x;
        if (pitch > 180f) pitch -= 360f;
        return pitch;
    }

    // ---------- MANUAL INPUT (call this from Update) ----------
    void HandleManualInput()
    {
        // --- START DETECTION: swipe down + hold (mouse or touch) ---
        if (!isManual)
        {
            // Mouse
            if (Input.GetMouseButtonDown(0))
                initialSwipePos = Input.mousePosition;

            if (Input.GetMouseButton(0))
            {
                Vector2 swipeDelta = (Vector2)Input.mousePosition - initialSwipePos;
                if (swipeDelta.y < -30f && isGrounded)
                {
                    StartManual();
                    // keep isHoldingManual true here because button is still held
                }
            }

            // Touch
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

        // --- WHILE IN MANUAL ---
        if (isManual)
        {
            Vector2 movementDelta = Vector2.zero;
            bool stillHolding = false;

            // Mouse: hold and move mouse Y to balance
            if (Input.GetMouseButton(0))
            {
                stillHolding = true;
                // Use mouse delta for responsiveness
                movementDelta.y = Input.GetAxis("Mouse Y");
            }

            // Touch: use touch delta to balance
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Moved)
                {
                    stillHolding = true;
                    movementDelta.y = t.deltaPosition.y / 10f; // scale down
                }
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    stillHolding = false;
                }
            }

            if (!stillHolding)
            {
                // user released hold -> end manual
                EndManual();
                return;
            }

            // Adjust the target angle by input (note sign)
            // Positive mouse/touch Y should *reduce* nose-up (pulling up), negative increases nose-up, tune as needed.
            manualTargetAngle += -movementDelta.y * manualTiltSpeed * Time.deltaTime;
            manualTargetAngle = Mathf.Clamp(manualTargetAngle, -maxManualAngle, maxManualAngle);

            // Apply rotation immediately (or lerp for smoothness)
            ApplyManualRotation(manualTargetAngle);

            // Check fail condition: if outside safe range for longer than fail time
            if (Mathf.Abs(manualTargetAngle) > manualSafeRange)
            {
                manualFailTimer += Time.deltaTime;
                if (manualFailTimer >= manualFailTime)
                    EndManual(true);
            }
            else
            {
                manualFailTimer = 0f;
                // Add score per second while successfully balancing
                if (trickScores.TryGetValue("Manual", out int baseScore))
                {
                    float timePoints = Time.deltaTime * baseScore;
                    totalScore += Mathf.RoundToInt(timePoints);

                    // Update the UI so the player sees the score go up
                    if (gameManager != null)
                    {
                        gameManager.UpdateUI(totalScore, currentCombo, comboMultiplier);
                    }
                }
            }
        }
    }


    private void Grounded()
    {
        Vector3 rayOrigin = transform.position;
        RaycastHit hit;

        bool wasGrounded = isGrounded;
        
        if (staminaSystem != null)
        {
            staminaSystem.SetGrounded(isGrounded);
        }
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, raycastDistance, groundLayer))
        {
            isGrounded = true;
            
            // If we just landed
            if (!wasGrounded)
            {
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // Reduce bouncing on landing
                    rb.velocity = new Vector3(rb.velocity.x, rb.velocity.y * 0.5f, rb.velocity.z);
                    
                    // Force immediate partial correction on landing
                    Vector3 groundNormal = hit.normal;
                    Quaternion landingRotation = Quaternion.FromToRotation(transform.up, groundNormal) * transform.rotation;
                    transform.rotation = Quaternion.Slerp(transform.rotation, landingRotation, 0.5f);
                }

                // Complete any active combo when landing
                if (currentCombo > 0)
                {
                    FinalizeCombo();
                }
            }

            // stop any visual trick coroutines? We keep rotation, but slerp back to forward
            StartCoroutine(RealignVisualOnLand(0.25f));
            if (cam != null) cam.SetIgnoreRotation(false);
        }
        else
        {
            isGrounded = false;
        }

        // detect grind rail
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, raycastDistance, grindLayer))
        {
            if (!isGrinding) StartCoroutine(StartGrind(hit));
        }
    }

    private void Move()
    {
        if (!canMove) return;

        // Movement is always to the RIGHT in world-space (local X axis pointing right on screen)
        // Keep horizontal movement independent of visual rotation.
        Vector3 horizontal = Vector3.right * moveSpeed; // always world-right
        Vector3 newVel = new Vector3(horizontal.x, rb.velocity.y, horizontal.z);
        rb.velocity = newVel;
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

    void Ollie()
    {
        if (!canOllie || !isGrounded) return;
        
        if (staminaSystem != null)
        {
            staminaSystem.OnJump();
        }

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        Debug.Log("Ollie!");
    }
    /*
    void Manual()
    {
        if (!isGrounded) return;
        Debug.Log("Manual!");
        StartCoroutine(ManualRoutine());
    }

    IEnumerator ManualRoutine()
    {
        float manualTime = 1.5f;
        float timer = 0;
        float lastScore = 0f;
        
        while (timer < manualTime && isManual)  // Stop if manual ends
        {
            timer += Time.deltaTime;
            
            // Score points for maintaining manual
            if (Mathf.Abs(manualTargetAngle) <= manualSafeRange)
            {
                float timePoints = Time.deltaTime * trickScores["Manual"];
                totalScore += Mathf.RoundToInt(timePoints);
                lastScore += timePoints;
                
                if (lastScore >= 50f)  // Log every 50 points
                {
                    Debug.Log($"Manual balance points: +{Mathf.RoundToInt(lastScore)}");
                    lastScore = 0f;
                }
            }
            
            yield return null;
        }
    } DEFUNCT FOR NOW
    */

    void Kickflip()
    {
        if (isGrounded)
        {
            // Add upward force if starting from ground
            rb.AddForce(Vector3.up * jumpForce * 0.6f, ForceMode.Impulse);
        }

        // Double tap within 0.3 seconds for multiple flips
        if (Time.time - lastTrickTime < 0.3f && lastTrickName == "Kickflip")
        {
            StartFlip("Kickflip", Vector3.right, false, 2); // right = clockwise around Z
        }
        else
        {
            StartFlip("Kickflip", Vector3.right); // right = clockwise around Z
        }
    }

    void Heelflip()
    {
        if (isGrounded)
        {
            // Add upward force if starting from ground
            rb.AddForce(Vector3.up * jumpForce * 0.6f, ForceMode.Impulse);
        }

        if (Time.time - lastTrickTime < 0.3f && lastTrickName == "Heelflip")
        {
            StartFlip("Heelflip", Vector3.left, false, 2); // left = counter-clockwise around Z
        }
        else
        {
            StartFlip("Heelflip", Vector3.left); // left = counter-clockwise around Z
        }
    }

    void Backflip()
    {
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
        if (isGrounded)
        {
            // Add upward force if starting from ground
            rb.AddForce(Vector3.up * jumpForce * 0.6f, ForceMode.Impulse);
        }

        Debug.Log("Pop Shove-It!");
        cam.SetIgnoreRotation(true);
        cam.TriggerCameraShake();
        StartCoroutine(VisualSpinRoutine(180, "Pop Shove-It"));
    }

    void TreFlip()
    {
        if (isGrounded)
        {
            // Add upward force if starting from ground
            rb.AddForce(Vector3.up * jumpForce * 0.6f, ForceMode.Impulse);
        }

        Debug.Log("360 Flip (Tre Flip)!");
        cam.SetIgnoreRotation(true);
        cam.TriggerCameraShake();
        StartCoroutine(VisualSpinRoutine(360, "Tre Flip"));
    }

    void FiftyFiftyGrind()
    {
        Debug.Log("50-50 Grind!");
        StartCoroutine(GrindRoutine());
    }

    void Boardslide()
    {
        Debug.Log("Boardslide!");
        StartCoroutine(GrindRoutine());
    }

    void FiveOGrind()
    {
        Debug.Log("5-0 Grind!");
        StartCoroutine(GrindRoutine());
    }

    IEnumerator StartGrind(RaycastHit hit)
    {
        isGrinding = true;
        Debug.Log("Started Grind!");
        
        if (staminaSystem != null)
        {
            staminaSystem.OnGrindStart();
        }
        
        rb.useGravity = false;
        rb.velocity = hit.collider.transform.forward * moveSpeed;

        yield return new WaitForSeconds(2f); // basic grind duration

        rb.useGravity = true;
        isGrinding = false;
        if (staminaSystem != null)
        {
            staminaSystem.OnGrindEnd();
        }
        Debug.Log("End Grind!");
    }

    // Visual-only flip: rotates the board model around specified axis while physics stays
    IEnumerator VisualFlipRoutine(Vector3 axis, string trickName, bool isXAxisRotation = false, int flips = 1)
    {
        if (boardVisual == null)
        {
            Debug.LogWarning("BoardVisual not assigned — visual flip will rotate the root. Assign a child Transform to boardVisual.");
        }

        float duration = 0.5f * flips; // More time for multiple flips
        float elapsed = 0f;
        float totalRotation = 0f;
        float targetRotation = 360f * flips; // Full rotation(s)
        float currentRotationSpeed = rotationSpeed * (flips > 1 ? 1.5f : 1f); // Faster for multiple flips

        while (totalRotation < targetRotation)
        {
            elapsed += Time.deltaTime;
            float delta = currentRotationSpeed * Time.deltaTime;
            totalRotation += delta;

            if (boardVisual != null)
            {
                if (isXAxisRotation)
                {
                    // Front/backflip rotates around X axis (pitch)
                    boardVisual.Rotate(Vector3.right * delta * (axis.x > 0 ? 1 : -1), Space.Self);
                }
                else
                {
                    // Kick/heelflip rotates around Z axis (board's length)
                    boardVisual.Rotate(Vector3.forward * delta * (axis.x > 0 ? 1 : -1), Space.Self);
                }
            }
            else
            {
                // Fallback to root rotation
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

        // keep visual rotation until landing; camera handled separately
        Debug.Log($"{trickName} completed ({flips}x flip, {totalRotation:F1} degrees)");
    }

    // Visual-only spin (around Y axis)
    IEnumerator VisualSpinRoutine(float degrees, string trickName)
    {
        if (boardVisual == null)
        {
            Debug.LogWarning("BoardVisual not assigned — visual spin will rotate the root. Assign a child Transform to boardVisual.");
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

        Debug.Log($"{trickName} visual finished (still spinning until land)");
    }

    IEnumerator GrindRoutine()
    {
        rb.useGravity = false;
        yield return new WaitForSeconds(1.5f);
        rb.useGravity = true;
    }

    // Smoothly realign the visual model to match its initial orientation (facing movement direction)
    IEnumerator RealignVisualOnLand(float duration)
    {
        if (boardVisual == null) yield break;

        Quaternion start = boardVisual.localRotation;
        
        // Just return to our stored base rotation which was already aligned at Start()
        Quaternion targetLocal = boardVisualBaseLocalRot;
        
        Debug.Log($"Landing - Current:{start.eulerAngles}, Target:{targetLocal.eulerAngles}");

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // Use SmoothStep for ease-in/out transition
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            boardVisual.localRotation = Quaternion.Slerp(start, targetLocal, t);
            yield return null;
        }

        // Ensure final rotation is exact
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

    // Combo System Methods
    private void AddToCombo(string trickName, int multiplier = 1)
    {
        float currentTime = Time.time;
        
        // Check if within combo window
        if (currentTime - lastTrickTime <= comboTimeWindow)
        {
            // Don't allow same trick twice in a row
            if (trickName != lastTrickName)
            {
                currentCombo++;
                comboMultiplier = 1f + (currentCombo * 0.5f); // Each combo adds 50% bonus
            }
        }
        else
        {
            // Start new combo
            currentCombo = 1;
            comboMultiplier = 1f;
        }

        // Update UI through GameManager
        if (gameManager != null)
        {
            gameManager.UpdateUI(totalScore, currentCombo, comboMultiplier);
        }

        // Calculate score with error handling
        if (trickScores.TryGetValue(trickName, out int baseScore))
        {
            int scoreAdd = Mathf.RoundToInt(baseScore * comboMultiplier * multiplier);
            totalScore += scoreAdd;
            Debug.Log($"{trickName} x{multiplier} (Combo x{comboMultiplier:F1}) = +{scoreAdd} points! Total: {totalScore}");
            
            // Spawn combo text slightly above the player
            if (comboTextSpawner != null)
            {
                Vector3 textPosition = transform.position + Vector3.up * 1.5f;
                comboTextSpawner.SpawnTrickText(trickName, textPosition, multiplier, comboMultiplier);
            }
        }
        else
        {
            Debug.LogWarning($"No score defined for trick: {trickName}");
            // Use a default score to ensure tricks always count
            int defaultScore = 100;
            int scoreAdd = Mathf.RoundToInt(defaultScore * comboMultiplier * multiplier);
            totalScore += scoreAdd;
            
            // Spawn combo text even for undefined tricks
            if (comboTextSpawner != null)
            {
                Vector3 textPosition = transform.position + Vector3.up * 1.5f;
                comboTextSpawner.SpawnTrickText(trickName, textPosition, multiplier, comboMultiplier);
            }
            
            Debug.Log($"{trickName} x{multiplier} (Default Score) = +{scoreAdd} points! Total: {totalScore}");
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

    // Multiple flip variations
    private void StartFlip(string trickName, Vector3 axis, bool isXAxisRotation = false, int flips = 1)
    {
        if (staminaSystem != null)
        {
            bool isComplexTrick = flips > 1 || trickName.Contains("Tre") || isXAxisRotation;
            staminaSystem.OnTrickStart(isComplexTrick);
        }

        if (isGrounded)
        {
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
        
        // Add to combo with multiplier based on number of flips
        AddToCombo(trickName, flips);
    }
}
