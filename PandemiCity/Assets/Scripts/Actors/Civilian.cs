using UnityEngine.AI;
using System.Collections.Generic;
using UnityEngine;

// Civilian.cs
// Attach alongside SimulationEntity on the civilian prefab.
// Set entityType to Civilian on the SimulationEntity component.
// Location: Assets/Scripts/Actors/Civilian.cs

[RequireComponent(typeof(SimulationEntity))]
public class Civilian : MonoBehaviour
{
    // ---------------------------------------------------------------
    // Private components
    // ---------------------------------------------------------------
    private Camera mainCamera;
    private NavMeshAgent agent;
    private Rigidbody rb;
    private Animator animator;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    // ---------------------------------------------------------------
    // Inspector fields
    // ---------------------------------------------------------------

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float turnSpeed = 5f;
    public float wanderRadius = 40f;
    public float wanderInterval = 5f;

    [Header("Health")]
    public float maxHealth = 100f;
    public float health = 100f;

    [Header("Infection")]
    public bool isInfected = false;

    [Header("AI Radii")]
    public float fleeRadius = 20f;
    public float flockRadius = 15f;
    public float detectRadius = 50f;

    [Header("Fear")]
    public float fear = 0f;
    public float maxFear = 100f;
    public float fearPerDeath = 15f;
    public float fearDecayRate = 3f;

    [Header("Ground Snapping")]
    [Tooltip("How far above the character to start the ground raycast.")]
    public float groundRayOriginOffset = 2f;
    [Tooltip("Maximum distance to raycast downward to find the ground.")]
    public float groundRayLength = 10f;

    [Header("Debug")]
    public string currentState = "Wander";

    // ---------------------------------------------------------------
    // Private state
    // ---------------------------------------------------------------
    private Vector3 wanderTarget;
    private float wanderTimer;
    private bool isDead = false;
    private HealthSpot currentHealthSpot = null;
    private List<IQuadTreeEntity> nearbyBuffer = new List<IQuadTreeEntity>();
    private bool usingNavMesh = false;

    // ---------------------------------------------------------------
    // Unity messages
    // ---------------------------------------------------------------

    private void Start()
    {
        mainCamera = Camera.main;
        health = maxHealth;
        wanderTimer = Random.Range(0f, wanderInterval);

        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        rb = GetComponent<Rigidbody>();

        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.angularSpeed = 120f;
            agent.stoppingDistance = 0.5f;
            agent.acceleration = 8f;
            usingNavMesh = agent.isOnNavMesh;
        }

        // NavMesh handles positioning — disable Rigidbody physics
        if (usingNavMesh && rb != null)
            rb.isKinematic = true;

        PickNewWanderTarget();
    }

    private void Update()
    {
        // Update NavMesh status each frame
        if (agent != null)
            usingNavMesh = agent.isOnNavMesh;

        // Snap to ground if not on NavMesh
        if (!usingNavMesh)
            SnapToGround();
    }

    private void LateUpdate()
    {
        if (isDead) return;

        fear = Mathf.Max(0f, fear - fearDecayRate * Time.deltaTime);

        nearbyBuffer.Clear();
        if (SimulationManager.Instance != null)
            nearbyBuffer = SimulationManager.Instance.GetNearbyAll(
                transform.position, detectRadius);

        RunFSM();
    }
    private void OnGUI()
    {
        if (mainCamera == null || isDead) return;

        Vector3 worldPos = transform.position + Vector3.up * 2.5f;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        if (screenPos.z < 0) return;
        screenPos.y = Screen.height - screenPos.y;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 12;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        // Pick colour based on state
        Color stateColor = currentState switch
        {
            "Flee" => Color.red,
            "Panic" => new Color(1f, 0.4f, 0f),
            "SeekHealth" => Color.yellow,
            "Flock" => Color.green,
            "Wander" => Color.white,
            _ => Color.white
        };

        float labelWidth = 90f;
        float labelHeight = 20f;
        float x = screenPos.x - labelWidth * 0.5f;
        float y = screenPos.y - labelHeight;

        // Shadow
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(x + 1, y + 1, labelWidth, labelHeight),
            currentState, style);

        // Main label
        style.normal.textColor = stateColor;
        GUI.Label(new Rect(x, y, labelWidth, labelHeight),
            currentState, style);

        // Show infected indicator
        if (isInfected)
        {
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(x + 1, y - 16f, labelWidth, labelHeight),
                "* INFECTED", style);
            style.normal.textColor = new Color(1f, 0.3f, 0.3f);
            GUI.Label(new Rect(x, y - 16f, labelWidth, labelHeight),
                "* INFECTED", style);
        }
    }

    
    private void SnapToGround()
    {
        RaycastHit hit;
        Vector3 rayOrigin = transform.position + Vector3.up * groundRayOriginOffset;

        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, groundRayLength,
            LayerMask.GetMask("Ground")))
        {
            if (transform.position.y > hit.point.y + 0.05f)
            {
                Vector3 pos = transform.position;
                pos.y = Mathf.Lerp(transform.position.y, hit.point.y,
                    Time.deltaTime * 10f);
                transform.position = pos;

                if (rb != null && !rb.isKinematic)
                {
                    Vector3 vel = rb.linearVelocity;
                    vel.y = Mathf.Min(vel.y, 0f);
                    rb.linearVelocity = vel;
                }
            }
        }
    }

    

    private void RunFSM()
    {
        bool virusNearby = HasNearby(EntityType.VirusCloud, fleeRadius);
        bool healthLow = isInfected;
        bool civiliansNearby = HasNearby(EntityType.Civilian, flockRadius);

        // Priority 1 - Flee from virus
        if (virusNearby)
        {
            currentState = "Flee";
            StateFlee();
            return;
        }

        // Priority 2 - Seek health spot when infected
        if (healthLow)
        {
            currentState = "SeekHealth";
            StateSeekHealth();
            return;
        }

        // Priority 3 - Panic when very afraid
        if (fear >= maxFear * 0.6f)
        {
            currentState = "Panic";
            StatePanic();
            return;
        }

        // Priority 4 - Flock with other civilians
        if (civiliansNearby)
        {
            currentState = "Flock";
            StateFlock();
            return;
        }

        // Priority 5 - Wander
        currentState = "Wander";
        StateWander();
    }

   
  

    private void StateWander()
    {
        wanderTimer -= Time.deltaTime;
        if (wanderTimer <= 0f)
        {
            PickNewWanderTarget();
            wanderTimer = wanderInterval + Random.Range(-0.5f, 0.5f);
        }
        MoveToward(wanderTarget, moveSpeed);
    }

    private void StateFlee()
    {
        Vector3 fleeFrom = GetNearestPosition(EntityType.VirusCloud, fleeRadius);
        Vector3 fleeDir = (transform.position - fleeFrom).normalized;
        Vector3 fleeTarget = ClampToWorld(transform.position + fleeDir * wanderRadius);

        // Sample NavMesh for valid flee point
        UnityEngine.AI.NavMeshHit navHit;
        if (UnityEngine.AI.NavMesh.SamplePosition(fleeTarget, out navHit, 10f,
            UnityEngine.AI.NavMesh.AllAreas))
            fleeTarget = navHit.position;

        float speed = moveSpeed * 1.5f + (fear / maxFear) * moveSpeed;
        MoveToward(fleeTarget, speed);
        fear = Mathf.Min(fear + 15f * Time.deltaTime, maxFear);
    }

    private void StateSeekHealth()
    {
        // Release current spot if it ran out of stock or is full
        if (currentHealthSpot != null &&
            (!currentHealthSpot.HasStock || currentHealthSpot.IsFull))
        {
            currentHealthSpot.OccupantLeft();
            currentHealthSpot = null;
        }

        // Find a new health spot if needed
        if (currentHealthSpot == null)
        {
            IQuadTreeEntity nearest = SimulationManager.Instance != null
                ? SimulationManager.Instance.GetNearest(
                    transform.position, 9999f, EntityType.Hospital)
                : null;

            if (nearest != null)
            {
                SimulationEntity se = nearest as SimulationEntity;
                if (se != null)
                {
                    HealthSpot spot = se.GetComponent<HealthSpot>();

                    // Only commit if spot has stock AND is not full
                    if (spot != null && !spot.IsFull && spot.HasStock)
                    {
                        currentHealthSpot = spot;
                        spot.OccupantEntered();
                    }
                }
            }
        }

        if (currentHealthSpot != null)
        {
            MoveToward(currentHealthSpot.transform.position, moveSpeed * 1.3f);

            float dist = Vector3.Distance(
                transform.position, currentHealthSpot.transform.position);

            if (dist <= currentHealthSpot.HealRadius)
            {
                if (currentHealthSpot.HasStock)
                {
                    // Heal at the spot
                    health += currentHealthSpot.HealRate * Time.deltaTime;
                    if (health >= maxHealth)
                    {
                        health = maxHealth;
                        isInfected = false;
                        currentHealthSpot.OccupantLeft();
                        currentHealthSpot = null;
                    }
                }
                else
                {
                    // Stock ran out while healing — release and wander
                    currentHealthSpot.OccupantLeft();
                    currentHealthSpot = null;
                    StateWander();
                }
            }
        }
        else
        {
            // No available health spot — wander and hope
            StateWander();
        }
    }

    private void StatePanic()
    {
        wanderTimer -= Time.deltaTime;
        if (wanderTimer <= 0f)
        {
            Vector3 randomDir = new Vector3(
                Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
            wanderTarget = ClampToWorld(transform.position + randomDir * 3f);
            wanderTimer = 0.4f;
        }
        MoveToward(wanderTarget, moveSpeed * 1.8f);

        // Spread fear to nearby civilians
        foreach (IQuadTreeEntity e in nearbyBuffer)
        {
            if (e.Type != EntityType.Civilian) continue;
            SimulationEntity se = e as SimulationEntity;
            if (se == null) continue;
            Civilian other = se.GetComponent<Civilian>();
            if (other != null && other != this)
                other.ReceiveFear(fearPerDeath * 0.3f * Time.deltaTime);
        }
    }

    private void StateFlock()
    {
        Vector3 separation = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        int count = 0;

        SimulationEntity self = GetComponent<SimulationEntity>();

        foreach (IQuadTreeEntity e in nearbyBuffer)
        {
            if (e.Type != EntityType.Civilian) continue;
            if (e == self) continue;

            Vector3 toOther = e.Position - transform.position;
            float dist = toOther.magnitude;

            if (dist < 1.5f && dist > 0.01f)
                separation -= toOther.normalized / dist;

            cohesion += e.Position;
            count++;
        }

        if (count == 0) { StateWander(); return; }

        cohesion = (cohesion / count - transform.position).normalized;
        Vector3 flockDir = (separation * 1.5f + cohesion).normalized;
        MoveToward(ClampToWorld(transform.position + flockDir * 15f),
            moveSpeed * 0.9f);
    }

    
    

    public void ReceiveInfectionDamage(float damage)
    {
        if (isDead) return;
        isInfected = true;
        health -= damage;
        if (health <= 0f) Die();
    }

    public void ReceiveFear(float amount)
    {
        fear = Mathf.Min(fear + amount, maxFear);
    }

    public void NotifyDeathNearby()
    {
        fear = Mathf.Min(fear + fearPerDeath, maxFear);
        fleeRadius = Mathf.Min(fleeRadius + 2f, 16f);
    }

 

    private void MoveToward(Vector3 target, float speed)
    {
        if (usingNavMesh && agent != null)
        {
            agent.speed = speed;
            agent.SetDestination(target);

            float currentSpeed = agent.velocity.magnitude;
            if (animator != null)
                animator.SetFloat(SpeedHash, currentSpeed);
        }
        else
        {
            // Fallback direct movement when not on NavMesh
            Vector3 dir = target - transform.position;
            dir.y = 0f;

            if (dir.magnitude < 0.2f)
            {
                if (animator != null) animator.SetFloat(SpeedHash, 0f);
                return;
            }

            Vector3 moveDir = dir.normalized;
            transform.position += moveDir * speed * Time.deltaTime;

            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot, turnSpeed * Time.deltaTime);

            if (animator != null) animator.SetFloat(SpeedHash, speed);
        }
    }

    

    private void PickNewWanderTarget()
    {
        Vector3 offset = new Vector3(
            Random.Range(-wanderRadius, wanderRadius),
            0f,
            Random.Range(-wanderRadius, wanderRadius));
        wanderTarget = ClampToWorld(transform.position + offset);
    }

    private Vector3 ClampToWorld(Vector3 pos)
    {
        if (SimulationManager.Instance == null) return pos;
        QRect b = SimulationManager.Instance.QuadTree.Bounds;
        pos.x = Mathf.Clamp(pos.x, b.x + 0.5f, b.x + b.width - 0.5f);
        pos.z = Mathf.Clamp(pos.z, b.z + 0.5f, b.z + b.height - 0.5f);
        pos.y = transform.position.y;
        return pos;
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

    
 

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        currentState = "Dead";

        if (agent != null) agent.enabled = false;

        if (currentHealthSpot != null)
        {
            currentHealthSpot.OccupantLeft();
            currentHealthSpot = null;
        }

        if (SimulationManager.Instance != null)
        {
            List<IQuadTreeEntity> nearby = SimulationManager.Instance.GetNearbyAll(
                transform.position, flockRadius * 2f);

            foreach (IQuadTreeEntity e in nearby)
            {
                if (e.Type != EntityType.Civilian) continue;
                SimulationEntity se = e as SimulationEntity;
                if (se == null) continue;
                Civilian other = se.GetComponent<Civilian>();
                other?.NotifyDeathNearby();
            }
        }

        Destroy(gameObject, 0.5f);
    }
}