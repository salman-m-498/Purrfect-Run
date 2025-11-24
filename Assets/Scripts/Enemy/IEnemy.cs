using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IEnemy
{
    void Initialize(EnemyStats stats);
    void TakeDamage(float dmg);
    void Die();
    void ResetForPooling();
    GameObject gameObject { get; }
    Transform transform { get; }
    EnemyStats stats { get; }          // runtime copy or ScriptableObject ref
}
