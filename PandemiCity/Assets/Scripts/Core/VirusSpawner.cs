using UnityEngine;

public class VirusSpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject virusCloudPrefab;

    [Header("Spawn Settings")]
    public int initialCount = 3;
    public int maxClouds = 14;
    public float newOutbreakMin = 30f;
    public float newOutbreakMax = 60f;

    [Header("Height")]
    [Tooltip("How high above the ground the virus spawns.")]
    public float spawnHeightAboveGround = 10f;

    private float outbreakTimer;

    private void Start()
    {
        if (virusCloudPrefab == null)
        {
            Debug.LogError("VirusSpawner: No virus cloud prefab assigned.");
            return;
        }

        for (int i = 0; i < initialCount; i++)
            SpawnVirusCloud();

        outbreakTimer = Random.Range(newOutbreakMin, newOutbreakMax);
    }

    private void Update()
    {
        outbreakTimer -= Time.deltaTime;
        if (outbreakTimer <= 0f)
        {
            if (SimulationManager.Instance != null)
            {
                int currentClouds = SimulationManager.Instance.CountNearby(
                    Vector3.zero, 9999f, EntityType.VirusCloud);

                if (currentClouds < maxClouds)
                    SpawnVirusCloud();
            }

            outbreakTimer = Random.Range(newOutbreakMin, newOutbreakMax);
        }
    }

    private void SpawnVirusCloud()
    {
        Vector3 pos = EdgeWorldPosition();
        Instantiate(virusCloudPrefab, pos, Quaternion.identity);
    }

    private Vector3 EdgeWorldPosition()
    {
        if (SimulationManager.Instance == null)
            return transform.position;

        QRect b = SimulationManager.Instance.QuadTree.Bounds;
        int edge = Random.Range(0, 4);
        float x, z;

        switch (edge)
        {
            case 0:
                x = Random.Range(b.x, b.x + b.width);
                z = b.z + 2f;
                break;
            case 1:
                x = Random.Range(b.x, b.x + b.width);
                z = b.z + b.height - 2f;
                break;
            case 2:
                x = b.x + 2f;
                z = Random.Range(b.z, b.z + b.height);
                break;
            default:
                x = b.x + b.width - 2f;
                z = Random.Range(b.z, b.z + b.height);
                break;
        }

        // Raycast down to find actual ground height at this position
        Vector3 rayOrigin = new Vector3(x, 200f, z);
        RaycastHit hit;

        float groundY = 0f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 1000f))
            groundY = hit.point.y;

        // Spawn above wherever the ground actually is
        return new Vector3(x, groundY + spawnHeightAboveGround, z);
    }
}