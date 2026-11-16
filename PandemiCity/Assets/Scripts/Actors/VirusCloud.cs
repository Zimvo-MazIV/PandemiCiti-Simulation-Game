using System.Collections.Generic;
using UnityEngine;



[RequireComponent(typeof(SimulationEntity))]
public class VirusCloud : MonoBehaviour
{
    [Header("Movement")]
    public float baseSpeed = 1.5f;
    public float seekCivilianRadius = 15f;
    public float fleeHazmatRadius = 10f;

    [Header("Infection")]
    public float infectionRadius = 2f;
    public float infectionDamage = 8f;

    [Header("Growth")]
    public float growthRate = 0.02f;
    public float maxScale = 4f;
    public float splitScale = 3.5f;
    public GameObject virusCloudPrefab;

    [Header("Health")]
    public float health = 100f;
    public float maxHealth = 100f;

    [Header("Debug")]
    public string currentState = "Drift";

    private Vector3 moveDirection;
    private List<IQuadTreeEntity> nearbyBuffer = new List<IQuadTreeEntity>();
    private bool isDead = false;
    private float splitCooldown = 20f;
    private float splitTimer = 20f;

    private void Start()
    {
        moveDirection = new Vector3(
            Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
    }

    private void LateUpdate()
    {
        if (isDead) return;

        QueryNearby();
        RunFSM();
        Grow();
        TryInfectCivilians();
        TrySplit();
        ClampToWorld();
    }

    private void QueryNearby()
    {
        nearbyBuffer.Clear();
        if (SimulationManager.Instance != null)
        {
            float radius = Mathf.Max(seekCivilianRadius, fleeHazmatRadius);
            nearbyBuffer = SimulationManager.Instance.GetNearbyAll(
                transform.position, radius);
        }
    }

    private void RunFSM()
    {
        bool hazmatNearby = HasNearby(EntityType.SanitizerSquad, fleeHazmatRadius);
        bool civilianNearby = HasNearby(EntityType.Civilian, seekCivilianRadius);

        // Priority 1 - Flee from hazmat workers
        if (hazmatNearby)
        {
            currentState = "FleeHazmat";
            StateFlee();
            return;
        }

        // Priority 2 - Seek dense civilian clusters
        if (civilianNearby)
        {
            currentState = "SeekCivilians";
            StateSeekCivilians();
            return;
        }

        // Priority 3 - Drift with wind
        currentState = "Drift";
        StateDrift();
    }

    private void StateDrift()
    {
        Vector3 wind = Vector3.zero;
        if (WeatherSystem.Instance != null)
            wind = WeatherSystem.Instance.WindVector;

        moveDirection = Vector3.Lerp(
            moveDirection,
            (moveDirection + wind * 0.5f).normalized,
            Time.deltaTime).normalized;

        float speed = baseSpeed;
        if (WeatherSystem.Instance != null)
            speed *= WeatherSystem.Instance.HeatMultiplier * 0.5f;

        transform.position += moveDirection * speed * Time.deltaTime;
    }

    private void StateSeekCivilians()
    {
        Vector3 denseCentre = GetDenseCentre(EntityType.Civilian, seekCivilianRadius);
        Vector3 dir = (denseCentre - transform.position);
        dir.y = 0f;

        if (dir.magnitude > 0.1f)
            moveDirection = Vector3.Lerp(moveDirection, dir.normalized, Time.deltaTime * 2f);

        Vector3 wind = Vector3.zero;
        if (WeatherSystem.Instance != null)
            wind = WeatherSystem.Instance.WindVector;

        Vector3 finalDir = (moveDirection + wind * 0.3f).normalized;

        float speed = baseSpeed;
        if (WeatherSystem.Instance != null)
            speed *= Mathf.Lerp(1f, WeatherSystem.Instance.HeatMultiplier, 0.5f);

        transform.position += finalDir * speed * Time.deltaTime;
    }

    private void StateFlee()
    {
        Vector3 fleeFrom = GetNearestPosition(EntityType.SanitizerSquad, fleeHazmatRadius);
        Vector3 fleeDir = (transform.position - fleeFrom);
        fleeDir.y = 0f;

        if (fleeDir.magnitude > 0.01f)
            moveDirection = Vector3.Lerp(
                moveDirection, fleeDir.normalized, Time.deltaTime * 3f).normalized;

        transform.position += moveDirection * baseSpeed * 2f * Time.deltaTime;
    }

    private void TryInfectCivilians()
    {
        if (SimulationManager.Instance == null) return;

        List<IQuadTreeEntity> civilians = SimulationManager.Instance.GetNearby(
            transform.position, infectionRadius, EntityType.Civilian);

        float damage = infectionDamage;
        if (WeatherSystem.Instance != null)
            damage *= WeatherSystem.Instance.HeatMultiplier;

        foreach (IQuadTreeEntity e in civilians)
        {
            SimulationEntity se = e as SimulationEntity;
            if (se == null) continue;
            Civilian civ = se.GetComponent<Civilian>();
            if (civ != null)
                civ.ReceiveInfectionDamage(damage * Time.deltaTime);
        }
    }

    private void Grow()
    {
        if (transform.localScale.x >= maxScale) return;

        float growAmount = growthRate * Time.deltaTime;
        if (WeatherSystem.Instance != null)
            growAmount *= WeatherSystem.Instance.HeatMultiplier;

        transform.localScale += Vector3.one * growAmount;
        transform.localScale = Vector3.Min(transform.localScale, Vector3.one * maxScale);
    }

    private void TrySplit()
    {
        splitTimer -= Time.deltaTime;
        if (splitTimer > 0f) return;
        if (transform.localScale.x < splitScale) return;
        if (virusCloudPrefab == null) return;

        splitTimer = splitCooldown;

        Vector3 offset = new Vector3(
            Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));

        GameObject child = Instantiate(
            virusCloudPrefab,
            transform.position + offset,
            Quaternion.identity);

        VirusCloud childCloud = child.GetComponent<VirusCloud>();
        if (childCloud != null)
        {
            child.transform.localScale = transform.localScale * 0.5f;
            transform.localScale = transform.localScale * 0.6f;
        }
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;
        health -= damage;

        // Shrink when damaged
        float shrink = damage * 0.01f;
        transform.localScale = Vector3.Max(
            transform.localScale - Vector3.one * shrink,
            Vector3.one * 0.3f);

        if (health <= 0f || transform.localScale.x <= 0.3f)
            Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        Destroy(gameObject, 0.2f);
    }

    

    private bool HasNearby(EntityType type, float radius)
    {
        float r2 = radius * radius;
        foreach (IQuadTreeEntity e in nearbyBuffer)
        {
            if (e.Type != type) continue;
            Vector3 d = e.Position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude <= r2) return true;
        }
        return false;
    }

    private Vector3 GetNearestPosition(EntityType type, float radius)
    {
        float best = float.MaxValue;
        Vector3 pos = transform.position;
        float r2 = radius * radius;

        foreach (IQuadTreeEntity e in nearbyBuffer)
        {
            if (e.Type != type) continue;
            Vector3 d = e.Position - transform.position;
            d.y = 0f;
            float d2 = d.sqrMagnitude;
            if (d2 <= r2 && d2 < best) { best = d2; pos = e.Position; }
        }
        return pos;
    }

    private Vector3 GetDenseCentre(EntityType type, float radius)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        float r2 = radius * radius;

        foreach (IQuadTreeEntity e in nearbyBuffer)
        {
            if (e.Type != type) continue;
            Vector3 d = e.Position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude <= r2) { sum += e.Position; count++; }
        }

        return count > 0 ? sum / count : transform.position;
    }

    private void ClampToWorld()
    {
        if (SimulationManager.Instance == null) return;
        QRect b = SimulationManager.Instance.QuadTree.Bounds;
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, b.x + 1f, b.x + b.width - 1f);
        pos.z = Mathf.Clamp(pos.z, b.z + 1f, b.z + b.height - 1f);
        transform.position = pos;
    }
    private void OnDrawGizmosSelected()
    {
        // Infection radius - red
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, infectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, infectionRadius);

        // Seek civilian radius - yellow
        Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
        Gizmos.DrawSphere(transform.position, seekCivilianRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, seekCivilianRadius);

        // Flee hazmat radius - cyan
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, fleeHazmatRadius);
    }
}