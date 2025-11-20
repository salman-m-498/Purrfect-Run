using UnityEngine;

/// <summary>
/// Simple heal pickup: when the player collides, restore a fixed amount of health
/// by calling `HealthSystem.RestoreHealth(...)`. Supports optional SFX/VFX.
/// </summary>
public class HealPickup : MonoBehaviour
{
    [Tooltip("Amount of health to restore when picked up")]
    public float healAmount = 25f;

    [Tooltip("Optional particle prefab to spawn on pickup (will be instantiated at this object's position)")]
    public GameObject pickupVfx;

    [Tooltip("Optional audio clip to play on pickup")]
    public AudioClip pickupSfx;

    [Tooltip("If true, the pickup will be destroyed after pickup")]
    public bool destroyOnPickup = true;

    private void OnTriggerEnter(Collider other)
    {
        // Try to find HealthSystem on the collider or its parents
        var hs = other.GetComponentInParent<HealthSystem>();
        if (hs != null)
        {
            hs.RestoreHealth(healAmount);

            if (pickupVfx != null)
            {
                Instantiate(pickupVfx, transform.position, Quaternion.identity);
            }

            if (pickupSfx != null)
            {
                AudioSource.PlayClipAtPoint(pickupSfx, transform.position);
            }

            if (destroyOnPickup)
                Destroy(gameObject);
        }
    }

    // Support 2D physics if user uses triggers there
    private void OnTriggerEnter2D(Collider2D other)
    {
        var hs = other.GetComponentInParent<HealthSystem>();
        if (hs != null)
        {
            hs.RestoreHealth(healAmount);

            if (pickupVfx != null)
            {
                Instantiate(pickupVfx, transform.position, Quaternion.identity);
            }

            if (pickupSfx != null)
            {
                AudioSource.PlayClipAtPoint(pickupSfx, transform.position);
            }

            if (destroyOnPickup)
                Destroy(gameObject);
        }
    }
}
