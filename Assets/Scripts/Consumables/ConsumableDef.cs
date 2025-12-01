using UnityEngine;

[CreateAssetMenu(menuName = "Pick-ups/Consumable Def")]
public class ConsumableDef : ScriptableObject
{
    [Tooltip("Name for debug logs")]
    public string id;

    [Tooltip("Prefab that carries the pick-up collider / visuals")]
    public GameObject prefab;

    [Tooltip("How far ahead of the player we try to place it (world units)")]
    public float spawnDistance = 12f;

    [Tooltip("Max lateral distance from track centre")]
    public float sideRadius = 3f;

    [Tooltip("How many seconds between spawns for THIS type (min/max)")]
    public Vector2 spawnInterval = new Vector2(8f, 14f);

    [Tooltip("Maximum number of THIS type that can be alive at once")]
    public int maxAlive = 3;

    /* ---- effect ---- */
    public enum Effect { RestoreHealth, GiveShield, GiveStamina /*, …*/ }
    public Effect effect;
    public float amount; // e.g. +30 hp
}