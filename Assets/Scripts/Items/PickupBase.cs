using UnityEngine;
using System.Collections;

/// <summary>
/// PickupBase: Base class for all pickup items in the level.
/// Handles collision detection, visual feedback, and cleanup.
/// Derived classes override ApplyEffect() to define specific behavior.
/// </summary>
public abstract class PickupBase : MonoBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] protected float despawnTime = 30f;
    [SerializeField] protected Collider pickupCollider;
    [SerializeField] protected Rigidbody pickupRigidbody;
    [SerializeField] protected AudioSource audioSource;

    [Header("Animation")]
    [SerializeField] protected float bobSpeed = 2f;
    [SerializeField] protected float bobHeight = 0.5f;
    [SerializeField] protected float rotationSpeed = 90f;

    protected Vector3 startPosition;
    protected float spawnTime;
    protected bool hasBeenCollected = false;

    // Events
    public System.Action<PickupBase> OnPickedUp;

    protected virtual void Start()
    {
        startPosition = transform.position;
        spawnTime = Time.time;
        
        // Auto-setup collider if not assigned
        if (pickupCollider == null)
            pickupCollider = GetComponent<Collider>();
        if (pickupRigidbody == null)
            pickupRigidbody = GetComponent<Rigidbody>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Ensure collider is trigger
        if (pickupCollider != null)
            pickupCollider.isTrigger = true;

        // Schedule despawn
        Destroy(gameObject, despawnTime);
    }

    protected virtual void Update()
    {
        // Bob up and down
        Vector3 bobPosition = startPosition;
        bobPosition.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = bobPosition;

        // Rotate
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }

    protected virtual void OnTriggerEnter(Collider collision)
    {
        if (hasBeenCollected) return;

        // Check if player touched this
        PlayerController player = collision.GetComponent<PlayerController>();
        if (player != null)
        {
            CollectPickup(player);
        }
    }

    protected virtual void CollectPickup(PlayerController player)
    {
        if (hasBeenCollected) return;
        hasBeenCollected = true;

        // Apply the effect
        ApplyEffect(player);

        // Play feedback
        PlayPickupFeedback();

        // Notify subscribers
        OnPickedUp?.Invoke(this);

        // Destroy this pickup
        Destroy(gameObject);
    }

    /// <summary>
    /// Override this in derived classes to apply the pickup's specific effect.
    /// </summary>
    protected abstract void ApplyEffect(PlayerController player);

    protected virtual void PlayPickupFeedback()
    {
        // Play sound
        if (audioSource != null)
        {
            audioSource.Play();
        }

        // Disable visuals and physics so we can see through the pickup
        if (pickupCollider != null)
            pickupCollider.enabled = false;
        
        // Could spawn particle effect here
    }

    /// <summary>
    /// Force collection (for testing or special cases)
    /// </summary>
    public void ForceCollect(PlayerController player)
    {
        if (!hasBeenCollected)
        {
            CollectPickup(player);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Test Despawn")]
    public void TestDespawn()
    {
        Destroy(gameObject);
    }
#endif
}
