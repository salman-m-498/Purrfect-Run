using UnityEngine;
using System.Collections.Generic;

public class BillboardManager : MonoBehaviour
{
    // A static list to hold all billboard transforms
    public static List<Transform> Billboards = new List<Transform>();
    private Transform cameraTransform;

    void Start()
    {
        cameraTransform = Camera.main.transform;
    }

    // Called once per frame, AFTER all game logic has run
    void LateUpdate()
    {
        // Cache the camera's rotation to avoid redundant lookups
        Quaternion cameraYRotation = Quaternion.Euler(0f, cameraTransform.rotation.eulerAngles.y, 0f);

        // Iterate through all registered billboards and apply the same rotation
        foreach (Transform billboard in Billboards)
        {
            if (billboard != null)
            {
                // A very simple way to rotate on the Y axis
                billboard.rotation = cameraYRotation;

                // OR, use a LookAt approach for better tracking (slightly more complex):
                // billboard.LookAt(cameraTransform.position);
                // billboard.rotation = Quaternion.Euler(0f, billboard.rotation.eulerAngles.y, 0f);
            }
        }
    }
    
    // Call this from an enemy's Start() to register it
    public static void Register(Transform t)
    {
        if (!Billboards.Contains(t))
        {
            Billboards.Add(t);
        }
    }
}