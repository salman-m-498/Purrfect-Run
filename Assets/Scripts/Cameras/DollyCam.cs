using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Camera))]
public class DollyCam : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform player;
    // World-space offset for the fixed side view
    public Vector3 sideFollowOffset = new Vector3(-4f, 2f, -2f); 
    // Local-space offset for the player-relative view
    // X = side offset (+ = right, - = left)
    // Y = height offset
    // Z = distance BEHIND player (+ = behind, - = in front)
    public Vector3 backFollowOffset = new Vector3(0f, 3f, -10f);
    public bool FollowBehind = false; // Controls which mode is active
    
    [Header("Movement Direction Tracking")]
    public bool useMovementDirection = true; // Track actual movement instead of player rotation
    public float minSpeedForDirection = 0.5f; // Minimum speed to update direction
    private Vector3 lastMovementDirection = Vector3.forward;
    private float currentCameraYaw = 0f;

    [Header("Rotation Clamp")]
    public Vector2 xRotationClamp = new Vector2(-5f, 5f);

    [Header("Follow Settings")]
    public float followSmoothness = 5f;
    public float rotationSmoothness = 5f;
    public float rotationSmoothnessInAir = 2f; // Slower rotation during tricks

    [Header("Look-Ahead Settings")]
    public bool enableLookAhead = true;
    public float lookAheadDistance = 2f;
    public float lookAheadSmoothness = 3f;
    private Vector3 currentLookAhead = Vector3.zero;

    [Header("Dynamic Distance Settings")]
    public bool enableDynamicDistance = true;
    public float minDistanceMultiplier = 0.8f;  // Multiplier when slow (80% of base distance)
    public float maxDistanceMultiplier = 1.5f;  // Multiplier when fast (150% of base distance)
    public float speedThresholdForMaxDistance = 20f;
    public float distanceSmoothness = 3f;
    private float currentDistanceMultiplier = 1f;

    [Header("Dynamic Height Settings")]
    public bool enableDynamicHeight = true;
    public float heightBoostInAir = 1.5f;
    public float heightBoostSpeed = 15f; // Speed threshold for height boost
    public float heightSmoothness = 4f;
    private float currentHeightBoost = 0f;

    [Header("Collision Avoidance")]
    public bool enableCollisionAvoidance = true;
    public LayerMask collisionLayers;
    public float collisionBuffer = 0.3f;
    public float collisionSmoothness = 8f;
    private float currentCollisionOffset = 0f;

    [Header("Tilt Settings")]
    public float maxTiltAngle = 10f;       
    public float tiltSmoothness = 5f;

    [Header("FOV Settings")]
    public float baseFOV = 60f;
    public float maxFOV = 75f;
    public float fovSpeedInfluence = 10f;

    [Header("Shake Settings")]
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 0.2f;
    public float landingShakeMagnitude = 0.4f;
    public float landingShakeDuration = 0.3f;

    [Header("Landing Impact")]
    public bool enableLandingImpact = true;
    public float landingFOVPunch = 10f;
    public float landingFOVPunchDuration = 0.3f;
    private float currentFOVPunch = 0f;

    [Header("Grind Mode")]
    public bool isGrinding = false;
    public float grindRotationLock = 0.9f; // Higher = more locked rotation during grinds

    [Header("Mode Transition")]
    public float modeTransitionTime = 0.5f;
    private float modeTransitionProgress = 1f;
    private bool previousFollowBehind = false;

    private Camera cam;
    private Rigidbody playerRb;
    private bool isShaking = false;
    private bool ignoreRotation = false;
    private Quaternion frozenRotation;
    private float currentTilt = 0f;
    private bool wasGrounded = true;
    private float airTime = 0f;

    void Start()
    {
        cam = GetComponent<Camera>();

        if (player != null)
        {
            playerRb = player.GetComponent<Rigidbody>();
            
            // Initialize movement direction
            if (playerRb != null)
            {
                Vector3 vel = playerRb.velocity;
                vel.y = 0;
                if (vel.magnitude > minSpeedForDirection)
                {
                    lastMovementDirection = vel.normalized;
                }
                else
                {
                    lastMovementDirection = player.forward;
                }
            }
            else
            {
                lastMovementDirection = player.forward;
            }
            
            currentCameraYaw = Mathf.Atan2(lastMovementDirection.x, lastMovementDirection.z) * Mathf.Rad2Deg;
        }

        frozenRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        previousFollowBehind = FollowBehind;
        
        // Initialize the camera rotation
        if (FollowBehind && player != null)
        {
            transform.rotation = Quaternion.Euler(0f, currentCameraYaw + 90f, 0f);
        }
        else
        {
            transform.rotation = frozenRotation;
        }

        // detach from any parent so camera won't inherit rotations/transforms
        if (transform.parent != null)
            transform.SetParent(null);
    }

    void LateUpdate()
    {
        if (player == null) return;

        // Track mode transitions
        if (FollowBehind != previousFollowBehind)
        {
            modeTransitionProgress = 0f;
            previousFollowBehind = FollowBehind;
        }
        modeTransitionProgress = Mathf.Min(1f, modeTransitionProgress + Time.deltaTime / modeTransitionTime);

        // Detect air time for dynamic adjustments
        bool isGrounded = IsPlayerGrounded();
        if (!isGrounded)
        {
            airTime += Time.deltaTime;
        }
        else
        {
            if (!wasGrounded && airTime > 0.3f) // Landed after being in air
            {
                TriggerLandingImpact();
            }
            airTime = 0f;
        }
        wasGrounded = isGrounded;

        // --- DYNAMIC DISTANCE CALCULATION ---
        if (enableDynamicDistance && playerRb != null)
        {
            float speedPercent = Mathf.Clamp01(playerRb.velocity.magnitude / speedThresholdForMaxDistance);
            float targetMultiplier = Mathf.Lerp(minDistanceMultiplier, maxDistanceMultiplier, speedPercent);
            currentDistanceMultiplier = Mathf.Lerp(currentDistanceMultiplier, targetMultiplier, Time.deltaTime * distanceSmoothness);
        }
        else
        {
            currentDistanceMultiplier = 1f;
        }

        // --- DYNAMIC HEIGHT CALCULATION ---
        if (enableDynamicHeight && playerRb != null)
        {
            float targetHeightBoost = 0f;
            
            // Boost height when in air
            if (!isGrounded)
            {
                targetHeightBoost = heightBoostInAir;
            }
            // Also boost slightly at high speeds
            else if (playerRb.velocity.magnitude > heightBoostSpeed)
            {
                targetHeightBoost = heightBoostInAir * 0.4f;
            }

            currentHeightBoost = Mathf.Lerp(currentHeightBoost, targetHeightBoost, Time.deltaTime * heightSmoothness);
        }
        else
        {
            currentHeightBoost = 0f;
        }

        // --- LOOK-AHEAD CALCULATION ---
        if (enableLookAhead && playerRb != null)
        {
            Vector3 velocity = playerRb.velocity;
            velocity.y = 0; // Only horizontal look-ahead
            Vector3 targetLookAhead = velocity.normalized * lookAheadDistance * Mathf.Clamp01(velocity.magnitude / 10f);
            currentLookAhead = Vector3.Lerp(currentLookAhead, targetLookAhead, Time.deltaTime * lookAheadSmoothness);
        }
        else
        {
            currentLookAhead = Vector3.zero;
        }

        // --- POSITION FOLLOW ---
        Vector3 targetPosition;
        
        // Update movement direction tracking
        if (FollowBehind && useMovementDirection && playerRb != null)
        {
            Vector3 horizontalVelocity = playerRb.velocity;
            horizontalVelocity.y = 0;
            
            // Only update direction when moving with sufficient speed
            if (horizontalVelocity.magnitude > minSpeedForDirection)
            {
                lastMovementDirection = horizontalVelocity.normalized;
                
                // Calculate yaw from movement direction
                float movementYaw = Mathf.Atan2(lastMovementDirection.x, lastMovementDirection.z) * Mathf.Rad2Deg;
                
                // Smoothly interpolate camera yaw
                float yawDifference = Mathf.DeltaAngle(currentCameraYaw, movementYaw);
                currentCameraYaw += yawDifference * Time.deltaTime * rotationSmoothness;
            }
        }
        else if (FollowBehind && !useMovementDirection)
        {
            // Fallback to player rotation if not using movement direction
            currentCameraYaw = player.eulerAngles.y;
        }
        
        if (FollowBehind)
        {
            // Use the tracked direction for both position and rotation
            Quaternion directionRotation = Quaternion.Euler(0f, currentCameraYaw, 0f);
            
            // Apply dynamic distance multiplier to distance (Z) only
            // X stays constant for side offset, Z is the main distance control
            Vector3 adjustedOffset = new Vector3(
                backFollowOffset.x,  // Side offset (not affected by distance)
                backFollowOffset.y + currentHeightBoost,
                backFollowOffset.z * currentDistanceMultiplier  // Back distance
            );
            
            // Build position using movement direction:
            // right = X (side), up = Y (height), forward = Z (behind/front)
            targetPosition = player.position + 
                directionRotation * (Vector3.right * adjustedOffset.x + Vector3.forward * adjustedOffset.z) +
                Vector3.up * adjustedOffset.y +
                currentLookAhead; // Add look-ahead offset
        }
        else
        {
            targetPosition = player.position + 
                Vector3.right * sideFollowOffset.x +
                Vector3.up * (sideFollowOffset.y + currentHeightBoost) +
                Vector3.forward * sideFollowOffset.z +
                currentLookAhead;
        }

        // --- COLLISION AVOIDANCE ---
        if (enableCollisionAvoidance)
        {
            Vector3 directionToCamera = targetPosition - player.position;
            float desiredDistance = directionToCamera.magnitude;
            
            RaycastHit hit;
            if (Physics.Raycast(player.position, directionToCamera.normalized, out hit, desiredDistance, collisionLayers))
            {
                float targetOffset = desiredDistance - hit.distance + collisionBuffer;
                currentCollisionOffset = Mathf.Lerp(currentCollisionOffset, targetOffset, Time.deltaTime * collisionSmoothness);
            }
            else
            {
                currentCollisionOffset = Mathf.Lerp(currentCollisionOffset, 0f, Time.deltaTime * collisionSmoothness);
            }

            // Pull camera closer if collision detected
            if (currentCollisionOffset > 0f)
            {
                targetPosition = player.position + directionToCamera.normalized * (desiredDistance - currentCollisionOffset);
            }
        }

        // Apply smooth position follow with mode transition
        float positionSmooth = Mathf.Lerp(followSmoothness * 0.5f, followSmoothness, modeTransitionProgress);
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * positionSmooth);

        // --- CAMERA ROTATION ---
        Quaternion targetRotation;

        // Determine rotation smoothness based on context
        float activeRotationSmoothness = rotationSmoothness;
        if (!isGrounded || isGrinding)
        {
            activeRotationSmoothness = isGrinding ? rotationSmoothness * grindRotationLock : rotationSmoothnessInAir;
        }

        if (FollowBehind)
        {
            if (ignoreRotation)
            {
                targetRotation = frozenRotation;
            }
            else
            {
                // Camera looks perpendicular to movement direction (90° offset)
                float clampedPitch = Mathf.Clamp(0f, xRotationClamp.x, xRotationClamp.y);
                targetRotation = Quaternion.Euler(clampedPitch, currentCameraYaw, 0f);
            }

            float rotationSmooth = Mathf.Lerp(activeRotationSmoothness * 0.5f, activeRotationSmoothness, modeTransitionProgress);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmooth);
        }
        else // Fixed Side-View Camera
        {
            targetRotation = frozenRotation;

            // --- TILT BASED ON VELOCITY (SIDE VIEW ONLY) ---
            if (playerRb != null)
            {
                float targetTilt = Mathf.Clamp(-playerRb.velocity.x * maxTiltAngle / 10f, -maxTiltAngle, maxTiltAngle);
                currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSmoothness);
                targetRotation = targetRotation * Quaternion.Euler(0f, 0f, currentTilt);
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * modeTransitionProgress);
        }

        // --- FOV ZOOM BASED ON SPEED + LANDING PUNCH ---
        if (playerRb != null)
        {
            float speedPercent = Mathf.Clamp01(playerRb.velocity.magnitude / fovSpeedInfluence);
            float baseFOVWithPunch = baseFOV - currentFOVPunch;
            cam.fieldOfView = Mathf.Lerp(baseFOVWithPunch, maxFOV, speedPercent);
            
            // Decay FOV punch
            if (currentFOVPunch > 0f)
            {
                currentFOVPunch = Mathf.Lerp(currentFOVPunch, 0f, Time.deltaTime / landingFOVPunchDuration);
            }
        }
    }

    // --- GROUND CHECK ---
    private bool IsPlayerGrounded()
    {
        if (playerRb == null) return true;
        
        // Simple ground check - you may want to improve this based on your player controller
        RaycastHit hit;
        if (Physics.Raycast(player.position, Vector3.down, out hit, 1.2f))
        {
            return true;
        }
        return false;
    }

    // --- LANDING IMPACT ---
    private void TriggerLandingImpact()
    {
        if (enableLandingImpact)
        {
            currentFOVPunch = landingFOVPunch;
            TriggerCameraShake(landingShakeMagnitude, landingShakeDuration);
        }
    }

    // --- CAMERA SHAKE ---
    public void TriggerCameraShake()
    {
        TriggerCameraShake(shakeMagnitude, shakeDuration);
    }

    public void TriggerCameraShake(float magnitude, float duration)
    {
        if (!isShaking)
            StartCoroutine(CameraShake(magnitude, duration));
    }

    private IEnumerator CameraShake(float magnitude, float duration)
    {
        isShaking = true;
        Vector3 originalPos = transform.localPosition;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            float z = Random.Range(-1f, 1f) * magnitude * 0.3f;

            transform.localPosition = originalPos + new Vector3(x, y, z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalPos;
        isShaking = false;
    }

    // --- IGNORE ROTATION DURING TRICKS ---
    public void SetIgnoreRotation(bool state)
    {
        ignoreRotation = state;
        if (ignoreRotation)
        {
            frozenRotation = transform.rotation;
        }
        else
        {
            if (!FollowBehind)
            {
                frozenRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            }
        }
    }

    // --- GRIND MODE CONTROL ---
    public void SetGrindMode(bool grinding)
    {
        isGrinding = grinding;
    }
}