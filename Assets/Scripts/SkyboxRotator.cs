using UnityEngine;

public class SkyboxRotator : MonoBehaviour
{
    [Tooltip("Degrees per second")]
    public float rotationSpeed = 1f;

    void Update()
    {
        // Rotate the skybox horizontally
        RenderSettings.skybox.SetFloat("_Rotation", Time.time * rotationSpeed);
    }
}
