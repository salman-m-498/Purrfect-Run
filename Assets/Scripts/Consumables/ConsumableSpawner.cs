using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConsumableSpawner : MonoBehaviour
{
    [SerializeField] private List<ConsumableDef> defs;
    [SerializeField] private LayerMask groundMask;

    private Transform player;
    private EndlessLevelGenerator levelGen;
    private readonly Dictionary<ConsumableDef, int> alive = new();

    /* tunable QoL knobs */
    [Header("QoL")]
    [Tooltip("Min / max distance **ahead** of player X")]
    public Vector2 aheadRange = new(6f, 10f);
    [Tooltip("Max height offset above player Y")]
    public float maxHeightAbovePlayer = 2f;
    [Tooltip("How long to keep trying to find a valid spot per frame")]
    public int raycastAttempts = 10;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        levelGen = FindObjectOfType<EndlessLevelGenerator>();
        groundMask = 1 << LayerMask.NameToLayer("Ground");

        foreach (var d in defs) alive[d] = 0;

        foreach (var d in defs) StartCoroutine(SpawnLoop(d));
    }

    private IEnumerator SpawnLoop(ConsumableDef def)
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(def.spawnInterval.x, def.spawnInterval.y));
            if (alive[def] < def.maxAlive) SpawnOne(def);
        }
    }

    private void SpawnOne(ConsumableDef def)
    {
        Vector3 pPos = player.position;

        /* choose random spot ahead */
        float ahead = Random.Range(aheadRange.x, aheadRange.y);
        Vector3 centre = new Vector3(pPos.x + ahead,
                                     pPos.y + Random.Range(0, maxHeightAbovePlayer),
                                     0);

        /* try a handful of lateral offsets */
        for (int i = 0; i < raycastAttempts; i++)
        {
            Vector3 cand = centre +
                           Vector3.forward * Random.Range(-def.sideRadius, def.sideRadius); // Z = left/right
            cand.y += 20f; // start well above

            if (Physics.Raycast(cand, Vector3.down, out RaycastHit hit, 200f, groundMask))
            {
                /* must be on the generated mesh */
                if (!hit.collider.transform.IsChildOf(levelGen.GetGeneratedLevelParent().transform))
                    continue;

                Vector3 spawn = hit.point + Vector3.up * 0.4f; // float a little

                GameObject go = Instantiate(def.prefab, spawn, Quaternion.identity);
                go.GetComponent<ConsumablePickUp>().Init(def, this);
                alive[def]++;
                return;              // success – leave
            }
        }
    }

    public void OnCollected(ConsumableDef def)
    {
        alive[def] = Mathf.Max(0, alive[def] - 1);
    }
}