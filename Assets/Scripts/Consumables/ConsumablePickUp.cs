using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ConsumablePickUp : MonoBehaviour
{
    [Header("Magnet")]
    public float magnetRange = 3f;
    public float magnetSpeed = 12f;

    private ConsumableDef def;
    private ConsumableSpawner spawner;
    private Transform player;
    private bool collected = false;          // guard flag


    /* auto-cleanup if player leaves it far behind */
    private const float DESPAWN_DIST = 15f;

    public void Init(ConsumableDef d, ConsumableSpawner cs)
    {
        def = d;
        spawner = cs;
        player = GameObject.FindGameObjectWithTag("Player").transform;
        BillboardManager.Register(transform);
    }

    void Update()
    {
        if (collected) return;

        float dist = Vector3.Distance(transform.position, player.position);

        /* stop magnet if we are already overlapping */
        if (dist < 0.5f)
        {
            Collect();
            return;
        }

        /* magnet behaviour */
        if (dist < magnetRange)
        {
            Vector3 dir = (player.position - transform.position).normalized;
            transform.position += dir * (magnetSpeed * Time.deltaTime);
        }

        /* fallback cleanup if player leaves it behind */
        if (player.position.x - transform.position.x > DESPAWN_DIST)
        {
            spawner.OnCollected(def);
            Destroy(gameObject);
        }
    }

    /* called by trigger OR by magnet when overlapping */
    private void Collect()
    {
        if (collected) return;
        collected = true;
        ApplyEffect();
        spawner.OnCollected(def);
        Destroy(gameObject);        // kills it next frame
    }


    /* keep the existing OnTriggerEnter, just forward to Collect */
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Collect();
    }

    private void ApplyEffect()
    {
        switch (def.effect)
        {
            case ConsumableDef.Effect.RestoreHealth:
                FindObjectOfType<HealthSystem>().RestoreHealth(def.amount);
                break;
            case ConsumableDef.Effect.GiveShield:
                FindObjectOfType<HealthSystem>().ApplyShield();
                break;
        }
    }
}