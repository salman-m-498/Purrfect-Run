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
    public Collider characterCollider; // NEW: Reference to character collider

    [Header("Stabilization Settings")] // NEW SECTION
    public float uprightTorque = 50f;
    public float stabilizationDamping = 5f;
    public float maxCorrectionAngle = 45f; // How far off before aggressive correction
    public KeyCode resetOrientationKey = KeyCode.R; // Key to manually reset orientation
    public float manualResetCooldown = 1f; // Cooldown between manual resets
    
    [Header("High-Speed Terrain Following")]
    public float speedThresholdForAggressiveCorrection = 8f; // Speed at which to enable aggressive terrain following
    public bool useTerrainAdhesion = true; // Enable speed-based ground detection and adhesion
    public float adhesionForce = 15f; // Downward force to keep player on slopes at high speed
    public float slopeFollowSpeed = 12f; // How fast to correct rotation to match slope at high speed
    public bool alignBoardToMovement = true; // Align board visuals to movement direction
    
    [Header("Tumble Recovery")]
    public float tumbleDetectionAngle = 60f; // Angle at which we consider the player tumbling
    public float tumbleAngularVelocityThreshold = 5f; // Angular velocity above which indicates loss of control
    public float emergencyTorqueMultiplier = 3f; // How much stronger correction is during tumble recovery
    public float emergencyAngularDamping = 20f; // Aggressive damping during tumble to stop spin
    
    [Header("Z-Axis Centering")]
    public bool constrainZPosition = true; // Keep player centered on Z axis
    public float targetZPosition = 0f; // Target Z position to maintain
    public float zCenteringSpeed = 5f; // How fast to correct Z position (0-1 lerp per second)
    
    // Advanced correction settings
    [Header("Advanced Correction")]
    public float extremeCorrectionSpeed = 15f; // Speed for extreme angles
    public float normalCorrectionSpeed = 8f; // Speed for normal angles
    public float minCorrectionAngle = 5f; // Start correcting at this angle
    public float instantCorrectionThreshold = 0.1f; // Snap to upright if this close
    public bool usePhysicsCorrection = true; // Use torque-based correction
    public bool useDirectCorrection = true; // Use rotation-based correction
    
    private float lastManualResetTime = -999f;
    private bool hasShownFlipHint = false;
    private bool isCorrectingOrientation = false;
    private Vector3 lastGroundNormal = Vector3.up;
    private float speedAtLastGround = 0f;

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
    public float raycastDistanceAtMaxSpeed = 1.2f; // Longer raycast at high speeds to catch slopes ahead
    public float speedBasedRaycastLerp = 0.8f; // How aggressively to extend raycast with speed

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
    private bool isInTumble = false; // Tracking tumble state for emergency recovery
    private float tumbleRecoveryTimer = 0f;
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
    private HealthSystem healthSystem;
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
        healthSystem = manager.healthSystem;
        scoreSystem = manager.scoreSystem;
        
        if (staminaSystem == null)
        {
            Debug.LogError("StaminaSystem not found in GameManager!");
        }
        
        if (healthSystem == null)
        {
            Debug.LogError("HealthSystem not found in GameManager!");
        }
        
        if (scoreSystem == null)
        {
            Debug.LogError("ScoreSystem not found in GameManager!");
        }

        rb = GetComponent<Rigidbody>();
        targetSpeed = moveSpeed;
        
        // Force rotation to identity at startup - fix 45 degree initial rotation
        // Set both world and local rotation to identity to handle parented objects
        transform.rotation = Quaternion.identity;
        transform.localRotation = Quaternion.identity;
        baseLocalRot = transform.localRotation;
        
        if (boardVisual != null)
        {
            boardVisual.localRotation = Quaternion.identity;
            boardVisualBaseLocalRot = boardVisual.localRotation;
        }
        else
        {
            boardVisualBaseLocalRot = baseLocalRot;
        }

        // NEW: Disable character collider during tricks
        if (characterCollider != null)
        {
            Debug.Log("Character collider found and will be managed during tricks");
        }

        ResetState();
    }

    public void ResetState()
    {
        moveSpeed = 5f;
        targetSpeed = 0f;
        currentCombo = 0;
        comboMultiplier = 1f;
        pushCount = 0;
        lastTrickName = "";
        isGrinding = false;
        currentGrindCollider = null;
        isInTumble = false; // Reset tumble state
        
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Always reset to perfectly upright
        transform.rotation = Quaternion.identity;
        if (boardVisual != null)
        {
            boardVisual.localRotation = Quaternion.identity;
        }

        // NEW: Ensure character collider is enabled when grounded
        if (characterCollider != null)
        {
            characterCollider.enabled = true;
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
            boardVisual.localRotation = Quaternion.identity;
            boardVisualBaseLocalRot = boardVisual.localRotation;
        }
        else
        {
            boardVisualBaseLocalRot = baseLocalRot;
        }
        
        // Force perfect upright rotation on startup - no crooked starts
        // Set both world and local rotation to eliminate any parent rotation effects
        transform.rotation = Quaternion.identity;
        transform.localRotation = Quaternion.identity;
        if (rb != null)
        {
            rb.angularVelocity = Vector3.zero;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Ensure WebGL canvas captures input properly
        Debug.Log("WebGL build detected - Input system initialized");
        
        // Log input test
        StartCoroutine(TestWebGLInput());
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private IEnumerator TestWebGLInput()
    {
        yield return new WaitForSeconds(2f);
        Debug.Log("=== WebGL Input Test ===");
        Debug.Log($"Mouse Position: {Input.mousePosition}");
        Debug.Log($"Touch Supported: {Input.touchSupported}");
        Debug.Log($"Touch Count: {Input.touchCount}");
        Debug.Log("Try clicking/touching the screen...");
    }
#endif

    // ============================================================
    // UPDATE LOOPS
    // ============================================================

    void Update()
    {
        HandleInput();
        HandleManualInput();
        CheckGrindStatus();
        
        // NEW: Manage character collider based on state
        ManageCharacterCollider();
        
        // NEW: Handle manual reset input
        HandleManualReset();
        
        // NEW: Check for extreme angles and show hint
        CheckExtremeAngleHint();
    }

    private void FixedUpdate()
    {
        Grounded(); // Check ground FIRST in physics loop
        Move();
        
        // High-speed terrain adhesion - LIGHTER force to prevent jitter
        if (useTerrainAdhesion && isGrounded && moveSpeed > speedThresholdForAggressiveCorrection)
        {
            ApplyTerrainAdhesion();
        }
        
        // IMPROVED: Always try to correct when grounded (not just when not grinding)
        if (isGrounded && !isManual) // Don't interfere with manual tricks
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
    // NEW: CHARACTER COLLIDER MANAGEMENT
    // ============================================================

    private void ManageCharacterCollider()
    {
        if (characterCollider == null) return;

        // Disable character collider when airborne or doing tricks
        // This prevents the character model from interfering with physics
        if (!isGrounded || isGrinding)
        {
            characterCollider.enabled = false;
        }
        else
        {
            // Re-enable when safely grounded and upright
            float uprightDot = Vector3.Dot(transform.up, Vector3.up);
            if (uprightDot > 0.9f) // Only enable when mostly upright
            {
                characterCollider.enabled = true;
            }
        }
    }

    // ============================================================
    // NEW: MANUAL RESET & HINT SYSTEM
    // ============================================================

    private void HandleManualReset()
    {
        // Check if player pressed reset key (works on keyboard)
        bool resetPressed = Input.GetKeyDown(resetOrientationKey);
        
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL fallback - also allow double-tap to reset (mobile-friendly)
        if (Input.GetMouseButtonDown(1)) // Right click on desktop
        {
            resetPressed = true;
            Debug.Log("WebGL: Right-click reset detected");
        }
#endif
        
        if (resetPressed)
        {
            // Check cooldown
            if (Time.time - lastManualResetTime >= manualResetCooldown)
            {
                ManualResetOrientation();
            }
            else
            {
                float remainingCooldown = manualResetCooldown - (Time.time - lastManualResetTime);
                Debug.Log($"Reset on cooldown! Wait {remainingCooldown:F1}s");
            }
        }
    }

    private void ManualResetOrientation()
    {
        lastManualResetTime = Time.time;
        hasShownFlipHint = false; // Reset hint flag
        
        Debug.Log("🔄 Manual Orientation Reset!");
        
        // Instantly correct to upright
        Vector3 currentForward = transform.forward;
        currentForward.y = 0; // Keep horizontal direction
        if (currentForward.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(currentForward.normalized, Vector3.up);
        }
        else
        {
            // Fallback if we have no horizontal direction
            transform.rotation = Quaternion.identity;
        }
        
        // Kill all angular velocity
        if (rb != null)
        {
            rb.angularVelocity = Vector3.zero;
            
            // Keep horizontal velocity, reduce vertical bounce
            Vector3 horizontalVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            rb.velocity = horizontalVel + Vector3.up * Mathf.Min(rb.velocity.y, 0);
        }
        
        // Reset visual board if exists
        if (boardVisual != null)
        {
            boardVisual.localRotation = boardVisualBaseLocalRot;
        }
        
        // Optional: Show UI feedback
        if (UIManager.Instance != null)
        {
            // DRAFT: Show reset confirmation
            // UIManager.Instance.ShowHintText("Board Reset!", 1f);
        }
        
        Debug.Log("✅ Orientation corrected to upright");
    }

    private void CheckExtremeAngleHint()
    {
        // Only check when grounded
        if (!isGrounded) 
        {
            hasShownFlipHint = false; // Reset when airborne
            return;
        }
        
        // Skip if we're in a manual (intentional tilt)
        if (isManual) return;
        
        // Calculate how upside-down we are
        Vector3 currentUp = transform.up;
        float upDot = Vector3.Dot(currentUp, Vector3.up);
        float angleFromUpright = Mathf.Acos(Mathf.Clamp(upDot, -1f, 1f)) * Mathf.Rad2Deg;
        
        // If we're extremely tilted (>60 degrees) and haven't shown hint yet
        if (angleFromUpright > 60f && !hasShownFlipHint)
        {
            hasShownFlipHint = true;
            ShowFlipBoardHint();
            Debug.Log($"⚠️ Extreme angle detected: {angleFromUpright:F1}° from upright");
        }
    }

    private void ShowFlipBoardHint()
    {
        // DRAFT IMPLEMENTATION - To be connected to your UIManager
        if (UIManager.Instance != null)
        {
            // TODO: Implement these methods in UIManager
            
            // Option 1: Simple text hint
            // UIManager.Instance.ShowHintText($"Press [{resetOrientationKey}] to flip board upright", 3f);
            
            // Option 2: Animated hint with icon
            // UIManager.Instance.ShowKeyPrompt(resetOrientationKey, "Flip Board", 3f);
            
            // Option 3: Tutorial-style popup
            // UIManager.Instance.ShowTutorialHint("Board Flipped!", 
            //     $"Press [{resetOrientationKey}] to reset orientation", 
            //     TutorialHintType.Warning);
            
            Debug.Log($"[HINT WOULD SHOW]: Press {resetOrientationKey} to reset board orientation");
        }
        else
        {
            Debug.LogWarning("UIManager.Instance is null - cannot show flip hint");
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

    // ============================================================
    // HIGH-SPEED TERRAIN ADHESION
    // ============================================================

    private void ApplyTerrainAdhesion()
    {
        // Apply LIGHT downward force to keep player pressed onto slopes at high speed
        // This prevents the player from "launching" off uphill slopes without causing jitter
        if (rb == null) return;
        
        RaycastHit hit;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f; // Start slightly above
        
        Vector3 rayDirection = Vector3.down;
        float rayDistance = raycastDistanceAtMaxSpeed * 2f; // Longer raycast for adhesion
        
        if (Physics.Raycast(rayOrigin, rayDirection, out hit, rayDistance, groundLayer))
        {
            // Calculate adhesion force based on speed - REDUCED for smoothness
            float speedRatio = Mathf.Min(moveSpeed / maxMoveSpeed, 1f);
            // Only apply 30% of adhesion force to avoid jitter and hopping
            float appliedAdhesion = adhesionForce * speedRatio * 0.3f; 
            
            // Only apply on shallow-to-moderate slopes (not steep)
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (slopeAngle < 45f) // Reduced from 60 degrees
            {
                rb.AddForce(Vector3.down * appliedAdhesion, ForceMode.Acceleration);
            }
        }
    }

    // ============================================================
    // IMPROVED BOARD VISUAL ALIGNMENT
    // ============================================================

    private void AlignBoardVisualToMovement()
    {
        if (boardVisual == null || !alignBoardToMovement) return;
        
        // Board visual should always face world right (direction of movement)
        // Rotate to match the player's current up vector for natural appearance
        boardVisual.rotation = Quaternion.LookRotation(Vector3.right, transform.up);
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

        if (healthSystem != null)
        {
            healthSystem.SetGrounded(isGrounded);
        }

        // Speed-based raycast distance - extend detection at high speeds
        float currentRaycastDistance = raycastDistance;
        if (useTerrainAdhesion && moveSpeed > speedThresholdForAggressiveCorrection)
        {
            float speedRatio = Mathf.Min(moveSpeed / maxMoveSpeed, 1f);
            currentRaycastDistance = Mathf.Lerp(
                raycastDistance,
                raycastDistanceAtMaxSpeed,
                speedRatio * speedBasedRaycastLerp
            );
        }

        // Ground check
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, currentRaycastDistance, groundLayer))
        {
            isGrounded = true;
            lastGroundNormal = hit.normal;
            speedAtLastGround = moveSpeed;

            if (!wasGrounded)
            {
                lastGroundedTime = Time.time;
                
                if (rb != null)
                {
                    // On slopes, only dampen significant upward velocity (bounces)
                    // Preserve downward velocity to maintain slope momentum
                    if (rb.velocity.y > 1f) // Only dampen strong bounces
                    {
                        rb.velocity = new Vector3(rb.velocity.x, rb.velocity.y * 0.5f, rb.velocity.z);
                    }
                    else if (rb.velocity.y < 0)
                    {
                        // Don't dampen downward velocity - let gravity work naturally on slopes
                        rb.velocity = new Vector3(rb.velocity.x, rb.velocity.y * 0.98f, rb.velocity.z);
                    }
                    
                    // IMPROVED: Intelligent orientation correction on landing
                    Vector3 groundNormal = hit.normal;
                    Vector3 desiredUp = groundNormal;
                    
                    // Only apply rotation snap on first landing, then let correction handle it
                    if (alignBoardToMovement)
                    {
                        // Primary goal: forward is always world right for skateboard gameplay
                        Vector3 moveDirection = Vector3.right; // World right = skateboard forward
                        
                        // Secondary: align to slope by using surface normal
                        // Blend between pure upright and slope-aligned based on slope steepness
                        float slopeAlignment = Vector3.Dot(groundNormal, Vector3.up);
                        slopeAlignment = Mathf.Clamp01((slopeAlignment - 0.5f) * 2f); // 0-1 range: 0 = steep, 1 = flat
                        
                        Vector3 blendedUp = Vector3.Lerp(groundNormal, Vector3.up, slopeAlignment * 0.7f).normalized;
                        
                        // Build rotation: forward = world right, up = blended normal (GENTLE lerp, not instant)
                        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, blendedUp);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 0.5f); // Smooth blend
                        
                        // Gentle angular velocity damping
                        rb.angularVelocity *= 0.5f;
                        
                        Debug.Log($"✅ Landing: Aligned to {moveDirection}, slope angle = {Vector3.Angle(groundNormal, Vector3.up):F1}°");
                    }
                    else
                    {
                        // Legacy: align to surface normal
                        Quaternion landingRotation = Quaternion.FromToRotation(transform.up, groundNormal) * transform.rotation;
                        transform.rotation = Quaternion.Slerp(transform.rotation, landingRotation, 0.5f);
                        rb.angularVelocity *= 0.3f;
                    }
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
        
        // Constrain Z position to keep player centered
        if (constrainZPosition)
        {
            Vector3 currentPos = transform.position;
            float zDiff = currentPos.z - targetZPosition;
            
            // Smoothly lerp Z position toward target
            float zCorrectionAmount = Mathf.Clamp(zDiff, -1f, 1f) * zCenteringSpeed * Time.deltaTime;
            currentPos.z = Mathf.Lerp(currentPos.z, targetZPosition, zCorrectionAmount);
            
            transform.position = currentPos;
        }
    }

    // ============================================================
    // IMPROVED BOARD ORIENTATION CORRECTION
    // ============================================================

    private void CorrectBoardOrientation()
    {
        if (rb == null) return;

        Vector3 currentUp = transform.up;
        Vector3 targetUp = Vector3.up;
        
        // Calculate how far we are from upright (0 = upside down, 1 = perfect upright)
        float upDot = Vector3.Dot(currentUp, targetUp);
        float angleFromUpright = Mathf.Acos(Mathf.Clamp(upDot, -1f, 1f)) * Mathf.Rad2Deg;
        
        // Calculate angular velocity magnitude to detect if spinning out of control
        float angularVelMagnitude = rb.angularVelocity.magnitude;
        
        // TUMBLE DETECTION: Check if player is in uncontrolled tumble
        if (angleFromUpright > tumbleDetectionAngle && angularVelMagnitude > tumbleAngularVelocityThreshold)
        {
            isInTumble = true;
            tumbleRecoveryTimer = 0.5f; // Give player 0.5 seconds of emergency recovery
            Debug.Log($"🌪️ TUMBLE DETECTED! Angle: {angleFromUpright:F1}°, Angular Vel: {angularVelMagnitude:F2}");
        }
        
        // TUMBLE RECOVERY: Apply emergency correction if in tumble state
        if (isInTumble)
        {
            tumbleRecoveryTimer -= Time.fixedDeltaTime;
            
            // Emergency correction: Use FULL torque-based correction with aggressive damping
            Vector3 torqueAxis = Vector3.Cross(currentUp, targetUp);
            float torqueMagnitude = torqueAxis.magnitude;
            
            if (torqueMagnitude > 0.001f)
            {
                Vector3 torqueDirection = torqueAxis / torqueMagnitude;
                
                // MAX TORQUE during tumble recovery - override slope reduction
                float emergencyTorque = uprightTorque * emergencyTorqueMultiplier * Mathf.Clamp01(angleFromUpright / 90f);
                rb.AddTorque(torqueDirection * emergencyTorque, ForceMode.Acceleration);
                
                // AGGRESSIVE damping to kill the spin fast
                rb.angularVelocity = Vector3.Lerp(
                    rb.angularVelocity,
                    Vector3.zero,
                    Time.fixedDeltaTime * emergencyAngularDamping
                );
                
                Debug.Log($"💪 Emergency recovery active: {tumbleRecoveryTimer:F2}s remaining");
            }
            
            // Exit tumble if recovered or timer expired
            if (tumbleRecoveryTimer <= 0 || angleFromUpright < 30f)
            {
                isInTumble = false;
                Debug.Log("✅ Tumble recovery complete!");
            }
            
            return; // Skip normal correction while in emergency mode
        }
        
        // If we're very close to upright, snap to perfect and stop
        if (angleFromUpright < instantCorrectionThreshold)
        {
            if (isCorrectingOrientation)
            {
                // Snap to perfect upright
                Vector3 currentForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                if (currentForward.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(currentForward, Vector3.up);
                }
                rb.angularVelocity = Vector3.zero;
                isCorrectingOrientation = false;
                Debug.Log("✅ Orientation locked to upright");
            }
            return;
        }
        
        // If angle is too small, don't bother correcting
        if (angleFromUpright < minCorrectionAngle)
        {
            rb.angularVelocity *= 0.95f; // Light damping
            return;
        }
        
        isCorrectingOrientation = true;
        
        // Determine correction intensity based on angle
        bool isExtremeAngle = angleFromUpright > maxCorrectionAngle;
        float correctionSpeed = isExtremeAngle ? extremeCorrectionSpeed : normalCorrectionSpeed;
        float torqueMultiplier = isExtremeAngle ? 2f : 1f;
        
        // Log when we detect extreme angles
        if (isExtremeAngle && Time.frameCount % 30 == 0) // Log every 30 frames
        {
            Debug.Log($"⚠️ Correcting extreme angle: {angleFromUpright:F1}° from upright");
        }
        
        // On slopes: Use MODERATE correction (not weak) to prevent tumbles
        bool isOnSlope = lastGroundNormal.y < 0.95f; // If not perfectly flat
        float correctionIntensity = isOnSlope ? 0.7f : 1f; // 70% correction on slopes (improved from 40%)
        
        // METHOD 1: Physics-based torque correction (smooth and natural)
        if (usePhysicsCorrection)
        {
            Vector3 torqueAxis = Vector3.Cross(currentUp, targetUp);
            float torqueMagnitude = torqueAxis.magnitude;
            
            if (torqueMagnitude > 0.001f)
            {
                Vector3 torqueDirection = torqueAxis / torqueMagnitude;
                
                // Apply corrective torque - STRONGER on slopes to prevent tumbles
                float appliedTorque = uprightTorque * torqueMultiplier * Mathf.Clamp01(angleFromUpright / 90f) * correctionIntensity;
                rb.AddTorque(torqueDirection * appliedTorque, ForceMode.Acceleration);
                
                // Smart damping: More aggressive when high angular velocity
                float angularDampScale = Mathf.Clamp01(angularVelMagnitude / 10f); // Scale up damping if spinning fast
                rb.angularVelocity = Vector3.Lerp(
                    rb.angularVelocity, 
                    Vector3.zero, 
                    Time.fixedDeltaTime * (stabilizationDamping * 0.7f) * (1f + angularDampScale) // Heavier damping when spinning
                );
            }
        }
        
        // METHOD 2: Direct rotation correction (also works on slopes for bad angles)
        if (useDirectCorrection)
        {
            // Always apply direct correction on slopes if angle is extreme (to prevent tumble snowball)
            bool shouldApplyDirectOnSlope = isOnSlope && isExtremeAngle;
            
            if (!isOnSlope || shouldApplyDirectOnSlope)
            {
                // Calculate target rotation that maintains forward direction but corrects up vector
                Vector3 currentForward = transform.forward;
                Vector3 projectedForward = Vector3.ProjectOnPlane(currentForward, targetUp);
                
                Quaternion targetRotation;
                if (projectedForward.sqrMagnitude > 0.01f)
                {
                    targetRotation = Quaternion.LookRotation(projectedForward.normalized, targetUp);
                }
                else
                {
                    // If we're completely vertical, just use right as forward
                    targetRotation = Quaternion.LookRotation(Vector3.right, targetUp);
                }
                
                // Slerp toward target rotation - FASTER for extreme angles to prevent snowballing
                float slerpSpeed = correctionSpeed * Time.fixedDeltaTime;
                if (!isOnSlope)
                {
                    slerpSpeed *= 0.6f; // Gentler on flat ground
                }
                
                // Use faster correction for extreme angles - especially on slopes
                if (isExtremeAngle)
                {
                    slerpSpeed *= 2f; // Double speed for extreme angles to break the tumble cycle
                }
                
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, 
                    targetRotation, 
                    slerpSpeed
                );
                
                // Aggressive dampen for extreme angles
                if (isExtremeAngle)
                {
                    rb.angularVelocity = Vector3.Lerp(
                        rb.angularVelocity, 
                        Vector3.zero, 
                        Time.fixedDeltaTime * 12f // Faster damping for extreme
                    );
                }
            }
        }
        
        // Always preserve horizontal velocity while correcting
        Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        float verticalVelocity = rb.velocity.y;
        
        // Reduce bounce on extreme corrections
        if (isExtremeAngle && Mathf.Abs(verticalVelocity) > 0.5f)
        {
            verticalVelocity *= 0.7f;
        }
        
        rb.velocity = horizontalVelocity + Vector3.up * verticalVelocity;
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
        // WebGL can use mouse on desktop OR touch on mobile
        // Check for actual input type instead of platform
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL build - support both mouse and touch
        if (Input.touchCount > 0)
        {
            HandleTouchInput();
        }
        else if (Input.GetMouseButton(0) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0))
        {
            HandleMouseInput();
        }
#elif UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseInput();
#else
        // Mobile platforms
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
