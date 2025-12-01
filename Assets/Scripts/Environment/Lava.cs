using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Lava : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
        {
            // Check if the colliding object's layer is the "Player" layer
            if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
            {
                HealthSystem healthSystem = other.GetComponent<HealthSystem>();
                if (healthSystem != null)
                {
                    healthSystem.ApplyDamage(healthSystem.maxHealth); // Inflict fatal damage
                }
            }
        }
}
