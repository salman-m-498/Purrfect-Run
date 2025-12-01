using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
public class ItemUI : MonoBehaviour
{
    [Header("UI")]
    public GameObject    itemSlotPrefab;   // single icon + text
    public Transform     slotParent;       // horizontal group with 3 fixed slots

    private ItemSystem itemSystem;

    void Start()
    {
        itemSystem = FindObjectOfType<ItemSystem>();
        RefreshHUD();
    }

    public void RefreshHUD()
    {
        // destroy old icons (keep the 3 slots if you want, here we just clear)
        foreach (Transform t in slotParent) Destroy(t.gameObject);

        if (itemSystem == null) return;

        foreach (var kvp in itemSystem.stacks)
        {
            var def = itemSystem.items.Find(i => i.id == kvp.Key);
            if (def == null) continue;

            GameObject slot = Instantiate(itemSlotPrefab, slotParent);
            slot.SetActive(true);

            slot.GetComponentInChildren<Image>().sprite   = def.icon;
            slot.GetComponentInChildren<TextMeshProUGUI>().text = kvp.Value.ToString();
        }
    }
}