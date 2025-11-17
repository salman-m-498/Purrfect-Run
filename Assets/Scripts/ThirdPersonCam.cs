using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Camera))]
public class ThirdPersonCam : MonoBehaviour
{
    [Header("Target Settings")]
    // The player's transform to follow
    public Transform player;

    // Offset is now LOCAL to the player's FORWARD direction.
    // X = side, Y = height, Z = distance behind (positive Z is forward, negative Z is behind)
    public Vector3 followOffset = new Vector3(0f, 3f, -5f); 

    [Header("Follow Settings")]
    public float followSmoothness = 5f;
    public float rotationSmoothness = 7f; // Increased for a smoother look-at effect

    // --- Third-person specific properties ---
    [Tooltip("If true, the camera will snap the rotation to the player's direction immediately.")]
    public bool snapToPlayerDirection = true;
    
    // Original Tilt/FOV/Shake settings remain
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

    private Camera cam;
    private Rigidbody playerRb;
    private bool isShaking = false;
    private bool ignoreRotation = false;

    // We store the player's yaw (Y-axis rotation) for smooth camera orientation
    private float currentYaw = 0f; 

    void Start()
    {
        cam = GetComponent<Camera>();

        if (player != null)
        {
            playerRb = player.GetComponent<Rigidbody>();
            // Initialize Yaw to the player's starting rotation
            currentYaw = player.eulerAngles.y;
        }

        // detach from any parent so camera won't inherit rotations/transforms
        if (transform.parent != null)
            transform.SetParent(null);
    }

    void LateUpdate()
    {
        if (player == null) return;

        // --- 1. HANDLE CAMERA ROTATION (Orientation) ---
        
        // Only update yaw if we are not ignoring rotation (optional for cutscenes/tricks)
        if (!ignoreRotation)
        {
            // Get the player's current yaw (rotation around the Y-axis)
            float targetYaw = player.eulerAngles.y;

            // Smoothly interpolate the camera's current Yaw towards the player's target Yaw
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * rotationSmoothness);

            // Create the yaw rotation quaternion
            // We only care about rotation around the Y-axis (vertical)
            Quaternion rotation = Quaternion.Euler(0, currentYaw, 0);

            // --- 2. POSITION FOLLOW ---
            
            // Calculate the target position using the smoothed Yaw rotation, 
            // making the offset relative to the player's *current direction* but not tied to the player's full Transform.
            // This ensures the camera position is robust and follows the position smoothly.
            Vector3 targetPosition = player.position + rotation * followOffset;
            
            // Smoothly move the camera to the target position
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSmoothness);

            // --- 3. LOOK AT TARGET (Rotation) ---
            
            // Calculate the required rotation to look *at* the player.
            // This keeps the camera facing the player but avoids inheriting any unwanted roll/pitch from the player's object.
            Vector3 lookDirection = (player.position - transform.position).normalized;
            
            // Calculate the target rotation to look at the player
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);

            // Smoothly apply the rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothness);
        }


        // --- FOV ZOOM BASED ON SPEED (Re-using your existing logic) ---
        if (playerRb != null)
        {
            float speedPercent = Mathf.Clamp01(playerRb.velocity.magnitude / fovSpeedInfluence);
            cam.fieldOfView = Mathf.Lerp(baseFOV, maxFOV, speedPercent);
        }
        
        // NOTE: I removed the original Tilt logic as it was specific to the side-view camera. 
        // If you need camera tilt/lean in 3rd person, you'll calculate it based on velocity along the camera's X-axis or controller input.
    }

    // --- CAMERA SHAKE (Re-using your existing logic) ---
    public void TriggerCameraShake()
    {
        if (!isShaking)
            StartCoroutine(CameraShake());
    }

    private IEnumerator CameraShake()
    {
        isShaking = true;
        // NOTE: Use localPosition here if the camera is parented, but since we detach it in Start(), 
        // using transform.position or saving the position before the shake is safer. 
        Vector3 originalPos = transform.position;

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            // Generate random offset within the magnitude
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;
            float z = Random.Range(-1f, 1f) * shakeMagnitude * 0.3f; // Less movement on Z/depth

            transform.position = originalPos + new Vector3(x, y, z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = originalPos;
        isShaking = false;
    }

    // --- IGNORE ROTATION DURING TRICKS (Re-using your existing logic) ---
    public void SetIgnoreRotation(bool state)
    {
        ignoreRotation = state;
    }
}