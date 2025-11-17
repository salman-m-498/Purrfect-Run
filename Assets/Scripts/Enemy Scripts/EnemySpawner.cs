using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject batPrefab;
    public float spawnInterval = 5f;
    public bool autoSpawn = true;
    public int maxEnemies = 10;

    private float timer;

    void Update()
    {
        if (!autoSpawn) return;

        timer -= Time.deltaTime;

        if (timer <= 0)
        {
            TrySpawn();
            timer = spawnInterval;
        }
    }

    public void TrySpawn()
    {
        if (EnemyManager.Count >= maxEnemies)
            return;

        Vector3 pos = transform.position + new Vector3(
            Random.Range(-3f, 3f),
            Random.Range(2f, 5f),
            Random.Range(-3f, 3f)
        );

        Instantiate(batPrefab, pos, Quaternion.identity);
    }

    // Manual spawn call for scripts
    public void SpawnOnce() => TrySpawn();
}
