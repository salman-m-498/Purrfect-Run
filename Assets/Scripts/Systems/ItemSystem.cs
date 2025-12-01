using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ItemSystem: permanent, stackable upgrades (Vampire-Survivors style).
/// Apply effects to HealthSystem, StaminaSystem, AutoAttackSystem, etc.
/// </summary>
public class ItemSystem : MonoBehaviour
{
    [System.Serializable]
    public class ItemDef
    {
        public string id;
        public string displayName;
        public string info;
        public Sprite icon;
        public bool stackable = true;
        public enum EffectType { MaxHealth, MaxStamina, Damage, AttackSpeed }
        public EffectType effect;
        public float value; // +10 max health, +5 damage, etc.
    }

    [Header("Item Definitions")]
    public List<ItemDef> items = new List<ItemDef>();

    [Header("Runtime Data")]
    public Dictionary<string, int> stacks = new Dictionary<string, int>();

    public const int MAX_ITEMS = 3;

    private HealthSystem health;
    private StaminaSystem stamina;
    private AutoAttackSystem attack;
    private ItemUI itemUI;
    void Start()
    {
        health = FindObjectOfType<HealthSystem>();
        stamina = FindObjectOfType<StaminaSystem>();
        attack = FindObjectOfType<AutoAttackSystem>();
        itemUI = FindObjectOfType<ItemUI>();
    }

    /// <summary>
    /// Give the player one random item and apply its effect immediately.
    /// </summary>
    public bool GrantRandomItem(out ItemDef granted)
    {
        granted = null;

        /* ---------- 1.  BAG FULL  ---------- */
        if (stacks.Count >= MAX_ITEMS)
        {
            // pick ONLY from already-owned items
            List<string> ownedIds = new List<string>(stacks.Keys);
            string id = ownedIds[Random.Range(0, ownedIds.Count)];
            granted = items.Find(i => i.id == id);
            stacks[id]++;
            ApplyEffect(granted);
            Debug.Log($"[ItemSystem] Bag full – granted existing {granted.displayName}");
            return true;
        }

        /* ---------- 2.  BAG NOT FULL ---------- */
        // filter out items we already have
        List<ItemDef> available = items.FindAll(i => !stacks.ContainsKey(i.id));

        if (available.Count == 0)          // <- NEW: all items owned, behave like full bag
        {
            // pick from owned ones
            List<string> ownedIds = new List<string>(stacks.Keys);
            string id = ownedIds[Random.Range(0, ownedIds.Count)];
            granted = items.Find(i => i.id == id);
            stacks[id]++;
            ApplyEffect(granted);
            Debug.Log($"[ItemSystem] Bag not full, but all items owned – granted existing {granted.displayName}");
            return true;
        }

        // else we still have new items to choose from
        granted = available[Random.Range(0, available.Count)];
        stacks[granted.id] = 1;
        ApplyEffect(granted);
        Debug.Log($"[ItemSystem] Granted new item {granted.displayName}");
        return true;
    }

    void ApplyEffect(ItemDef def)
    {
        int stack = stacks[def.id];
        switch (def.effect)
        {
            case ItemDef.EffectType.MaxHealth:
                if (health) health.maxHealth += def.value;
                health?.ResetHealth(); // refill on upgrade
                break;

            case ItemDef.EffectType.MaxStamina:
                if (stamina) stamina.maxStamina += def.value;
                stamina?.ResetStamina();
                break;

            case ItemDef.EffectType.Damage:
                if (attack) attack.damage += def.value;
                break;

            case ItemDef.EffectType.AttackSpeed:
                if (attack) attack.attackSpeedPercent += def.value;
                break;
        }
    }

    /// <summary> Clear all stacks (new run). </summary>
    public void ClearActiveItems()
    {
        stacks.Clear();
    }
}