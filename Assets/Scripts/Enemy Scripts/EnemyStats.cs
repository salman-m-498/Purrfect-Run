using UnityEngine;

/// <summary>
/// ScriptableObject for reusable enemy stats
/// Create via: Right-click in Project -> Create -> Game/Enemy Stats
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyStats", menuName = "Game/Enemy Stats", order = 1)]
public class EnemyStats : ScriptableObject
{
    [Header("Movement")]
    public float idleFloatSpeed = 1.5f;
    public float idleFloatAmplitude = 0.5f;
    public float moveSpeed = 6f;
    public float swoopSpeed = 12f;
    
    [Header("Combat")]
    public float detectRange = 10f;
    public float swoopCooldown = 3f;
    public float attackDamage = 10f;
    public float attackRange = 1.5f;
    public float attackCooldown = 1f;
    
    [Header("Health")]
    public float maxHealth = 50f;
    
    [Header("Animation")]
    public float flapSpeed = 20f;
    public float flapAmplitude = 0.2f;
    
    [Header("Rewards")]
    public int killScore = 100;
    public int killCoins = 5;
}