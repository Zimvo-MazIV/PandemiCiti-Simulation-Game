using UnityEngine;

// Spawns civilians at the start of the simulation spread across the world bounds. Also respawns civilians over time up to a certain max.
// L

public class CivilianSpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject civilianPrefab;

    [Header("Spawn Settings")]
    public int initialCount = 30;
    public int maxCount = 25;
    public float respawnInterval = 5f;

    private float respawnTimer;

    private void Start()
    {
        if (civilianPrefab == null)
        {
            Debug.LogError("CivilianSpawner: No civilian prefab assigned.");
            return;
        }

        for (int i = 0; i < initialCount; i++)
            SpawnCivilian();

        respawnTimer = respawnInterval;
    }

    private void Update()
    {
        if (SimulationManager.Instance == null) return;
        if (SimulationManager.Instance.QuadTree.EntityCount >= maxCount) return;

        respawnTimer -= Time.deltaTime;
        if (respawnTimer <= 0f)
        {
            SpawnCivilian();
            respawnTimer = respawnInterval;
        }
    }
    private void SpawnCivilian()
    {
        Vector3 pos = RandomWorldPosition();
        pos.y += 5f;
        // Force upright rotation on spawn
        Instantiate(civilianPrefab, pos, Quaternion.identity);
    }

    private Vector3 RandomWorldPosition()
    {
        if (SimulationManager.Instance == null)
            return transform.position;

        QRect b = SimulationManager.Instance.QuadTree.Bounds;
        int groundLayer = LayerMask.GetMask("Ground");

        // Try up to 10 times to find a valid ground position
        for (int i = 0; i < 10; i++)
        {
            float x = Random.Range(b.x + 2f, b.x + b.width - 2f);
            float z = Random.Range(b.z + 2f, b.z + b.height - 2f);

            Vector3 rayOrigin = new Vector3(x, 500f, z);
            RaycastHit hit;

            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 1000f, groundLayer))
                return new Vector3(x, hit.point.y, z);
        }

        // Fallback
        return new Vector3(
            Random.Range(b.x + 2f, b.x + b.width - 2f),
            0f,
            Random.Range(b.z + 2f, b.z + b.height - 2f));
    }
}