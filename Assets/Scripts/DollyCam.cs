using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Camera))]
public class DollyCam : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform player;
    public Vector3 followOffset = new Vector3(-4f, 2f, -2f);

    [Header("Follow Settings")]
    public float followSmoothness = 5f;
    public float rotationSmoothness = 5f;

    [Header("Tilt Settings")]
    public float maxTiltAngle = 10f;       // how much camera leans
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
    // store the rotation to hold while ignoreRotation is true
    private Quaternion frozenRotation;
    private float currentTilt = 0f;

    void Start()
    {
        cam = GetComponent<Camera>();

        if (player != null)
            playerRb = player.GetComponent<Rigidbody>();

        // Set up initial side-view camera rotation (looking perpendicular to player movement)
        frozenRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        transform.rotation = frozenRotation;

        // detach from any parent so camera won't inherit rotations/transforms
        if (transform.parent != null)
            transform.SetParent(null);
    }

    void LateUpdate()
    {
        if (player == null) return;

        // --- POSITION FOLLOW ---
        // Follow offset is in world coordinates:
        // X = right/left of player (-X = camera moves left, showing more space ahead)
        // Y = up/down from player
        // Z = distance from player (-Z = camera moves back)
        Vector3 targetPosition = player.position + 
            Vector3.right * followOffset.x +    // side offset
            Vector3.up * followOffset.y +       // height offset
            Vector3.forward * followOffset.z;   // distance from player
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSmoothness);

        // --- FIXED SIDE-VIEW ROTATION ---
        // Keep camera perpendicular to movement direction (looking along world forward)
        transform.rotation = frozenRotation;

        // --- TILT BASED ON VELOCITY ---
        if (playerRb != null)
        {
            // Compute target tilt based on player's world velocity (movement is always right)
            float targetTilt = Mathf.Clamp(-playerRb.velocity.x * maxTiltAngle / 10f, -maxTiltAngle, maxTiltAngle);
            currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSmoothness);
            // Apply tilt around the forward axis (Z) since we're in side view
            transform.rotation = frozenRotation * Quaternion.Euler(0f, 0f, currentTilt);
        }

        // --- FOV ZOOM BASED ON SPEED ---
        if (playerRb != null)
        {
            float speedPercent = Mathf.Clamp01(playerRb.velocity.magnitude / fovSpeedInfluence);
            cam.fieldOfView = Mathf.Lerp(baseFOV, maxFOV, speedPercent);
        }
    }

    // --- CAMERA SHAKE ---
    public void TriggerCameraShake()
    {
        if (!isShaking)
            StartCoroutine(CameraShake());
    }

    private IEnumerator CameraShake()
    {
        isShaking = true;
        Vector3 originalPos = transform.localPosition;

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;
            float z = Random.Range(-1f, 1f) * shakeMagnitude * 0.3f;

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
    }
}
