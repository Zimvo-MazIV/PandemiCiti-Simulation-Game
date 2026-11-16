using UnityEngine.AI;
using System.Collections.Generic;
using UnityEngine;



[RequireComponent(typeof(SimulationEntity))]
public class HazmatWorker : MonoBehaviour
{
   

    [Header("Effects")]
    public ParticleSystem sprayEffect;

    [Header("Movement")]
    public float moveSpeed = 25f;
    public float turnSpeed = 6f;
    public float patrolRadius = 80f;

    [Header("Spray")]
    public float sprayRadius = 120f;
    public float sprayDamage = 15f;
    public float sprayInterval = 0.3f;

    [Header("Detection")]
    public float detectRadius = 150f;
    public float coordinateRadius = 100f;

    [Header("Ground Snapping")]
    public float groundRayOriginOffset = 2f;
    public float groundRayLength = 10f;

    [Header("Debug")]
    public string currentState = "Patrol";

 
    private NavMeshAgent agent;
    private Animator animator;
    private Camera mainCamera;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");


    private Vector3 patrolTarget;
    private float patrolTimer;
    private float sprayTimer;
    private List<IQuadTreeEntity> nearbyBuffer = new List<IQuadTreeEntity>();
    private bool isInterceptor = false;
    private bool usingNavMesh = false;
    private SimulationEntity lockedVirusTarget = null;



    private void Start()
    {
        mainCamera = Camera.main;

        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.angularSpeed = 120f;
            agent.stoppingDistance = 0.5f;
            agent.acceleration = 8f;
            usingNavMesh = agent.isOnNavMesh;
        }

        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        PickPatrolTarget();
        patrolTimer = Random.Range(2f, 5f);
    }

    private void Update()
    {
        if (agent != null)
            usingNavMesh = agent.isOnNavMesh;

        if (!usingNavMesh)
            SnapToGround();
    }

    private void LateUpdate()
    {
        // In HazmatWorker LateUpdate temporarily:
        Debug.Log($"Buffer: {nearbyBuffer.Count} | Detect: {detectRadius} | virusNearby: {HasNearbyXZ(EntityType.VirusCloud, detectRadius)}");
        QueryNearby();
        RunFSM();
        sprayTimer -= Time.deltaTime;

        if (animator != null)
        {
            float speed = usingNavMesh && agent != null
                ? agent.velocity.magnitude
                : 0f;
            animator.SetFloat(SpeedHash, speed);
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
            }
        }
    }

   

    private void QueryNearby()
    {
        nearbyBuffer.Clear();
        if (SimulationManager.Instance != null)
            nearbyBuffer = SimulationManager.Instance.GetNearbyAll(
                transform.position, detectRadius);
    }



    private void RunFSM()
    {
        // Safe null check for destroyed Unity objects
        if (lockedVirusTarget != null &&
            (lockedVirusTarget.Equals(null) || lockedVirusTarget.gameObject == null))
            lockedVirusTarget = null;

        bool virusNearby = HasNearbyXZ(EntityType.VirusCloud, detectRadius);
        bool virusInRange = lockedVirusTarget != null
            ? XZDistance(transform.position, lockedVirusTarget.transform.position) <= sprayRadius
            : HasNearbyXZ(EntityType.VirusCloud, sprayRadius);

        // Priority 1 - Spray locked target if in range
        if (virusInRange && lockedVirusTarget != null)
        {
            currentState = "Spray";
            StateSpray();
            return;
        }

        // Priority 2 - Chase or intercept locked target
        if (lockedVirusTarget != null)
        {
            CoordinateWithNearbyWorkers();
            currentState = isInterceptor ? "Intercept" : "Chase";
            if (isInterceptor) StateIntercept();
            else StateChase();
            StopSprayEffect();
            return;
        }

        // Priority 3 - Lock onto nearest virus and chase
        if (virusNearby)
        {
            IQuadTreeEntity nearest = GetNearestEntityXZ(
                EntityType.VirusCloud, detectRadius);

            if (nearest != null)
            {
                lockedVirusTarget = nearest as SimulationEntity;
                Debug.Log($"[Hazmat] Locked onto virus: {lockedVirusTarget}");
            }

            CoordinateWithNearbyWorkers();
            currentState = isInterceptor ? "Intercept" : "Chase";
            if (isInterceptor) StateIntercept();
            else StateChase();
            StopSprayEffect();
            return;
        }

        // Priority 4 - Patrol
        isInterceptor = false;
        lockedVirusTarget = null;
        currentState = "Patrol";
        StatePatrol();
        StopSprayEffect();
    }



    private void StatePatrol()
    {
        patrolTimer -= Time.deltaTime;
        if (patrolTimer <= 0f)
        {
            PickPatrolTarget();
            patrolTimer = Random.Range(3f, 7f);
        }
        MoveTo(patrolTarget, moveSpeed * 0.6f);
    }

    private void StateChase()
    {
        if (lockedVirusTarget == null) return;

        // Move to ground-level position directly below virus
        Vector3 target = lockedVirusTarget.transform.position;
        target.y = transform.position.y;
        MoveTo(target, moveSpeed);
    }

    private void StateIntercept()
    {
        if (lockedVirusTarget == null) { StateChase(); return; }

        // Predict future position on XZ plane
        Vector3 virusPos = lockedVirusTarget.transform.position;
        Vector3 predictedPos = virusPos +
            (virusPos - transform.position).normalized * 3f;
        predictedPos.y = transform.position.y;

        MoveTo(predictedPos, moveSpeed * 1.1f);
    }

    private void StateSpray()
    {
        if (lockedVirusTarget == null) return;

        // Face the locked virus on XZ plane
        Vector3 dir = lockedVirusTarget.transform.position - transform.position;
        dir.y = 0f;
        if (dir.magnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }

        // Stop moving while spraying
        if (agent != null && agent.isOnNavMesh)
            agent.SetDestination(transform.position);

        // Start spray particle effect
        if (sprayEffect != null && !sprayEffect.isPlaying)
            sprayEffect.Play();

        if (sprayTimer > 0f) return;
        sprayTimer = sprayInterval;

        // Damage only the locked target
        VirusCloud cloud = lockedVirusTarget.GetComponent<VirusCloud>();
        if (cloud != null)
        {
            cloud.TakeDamage(sprayDamage);
        }
        else
        {
            // Locked target lost its VirusCloud — release lock
            lockedVirusTarget = null;
        }
    }

   

    private void CoordinateWithNearbyWorkers()
    {
        if (SimulationManager.Instance == null) return;
        if (lockedVirusTarget == null) return;

        List<IQuadTreeEntity> nearbyWorkers = SimulationManager.Instance.GetNearby(
            transform.position, coordinateRadius, EntityType.SanitizerSquad);

        float myDist = XZDistance(
            transform.position, lockedVirusTarget.transform.position);
        bool iAmFurthest = true;

        foreach (IQuadTreeEntity e in nearbyWorkers)
        {
            if (e == GetComponent<SimulationEntity>()) continue;
            float otherDist = XZDistance(
                e.Position, lockedVirusTarget.transform.position);
            if (otherDist > myDist) { iAmFurthest = false; break; }
        }

        isInterceptor = iAmFurthest && nearbyWorkers.Count > 0;
    }

   

    private void MoveTo(Vector3 target, float speed)
    {
        if (usingNavMesh && agent != null)
        {
            agent.speed = speed;
            agent.SetDestination(target);
        }
        else
        {
            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.magnitude < 0.2f) return;

            Vector3 moveDir = dir.normalized;
            transform.position += moveDir * speed * Time.deltaTime;

            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot, turnSpeed * Time.deltaTime);

            if (animator != null)
                animator.SetFloat(SpeedHash, speed);
        }
    }

    
    private void OnGUI()
    {
        if (mainCamera == null) return;

        Vector3 worldPos = transform.position + Vector3.up * 2.5f;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        if (screenPos.z < 0) return;
        screenPos.y = Screen.height - screenPos.y;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 12;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        Color stateColor = currentState switch
        {
            "Spray" => Color.green,
            "Chase" => Color.yellow,
            "Intercept" => new Color(1f, 0.5f, 0f),
            "Patrol" => Color.cyan,
            _ => Color.white
        };

        float labelWidth = 90f;
        float labelHeight = 20f;
        float x = screenPos.x - labelWidth * 0.5f;
        float y = screenPos.y - labelHeight;

        style.normal.textColor = Color.black;
        GUI.Label(new Rect(x + 1, y + 1, labelWidth, labelHeight),
            currentState, style);
        style.normal.textColor = stateColor;
        GUI.Label(new Rect(x, y, labelWidth, labelHeight),
            currentState, style);

        // Role indicator when chasing
        if (currentState == "Chase" || currentState == "Intercept")
        {
            string role = isInterceptor ? "[ INTERCEPT ]" : "[ CHASE ]";
            Color roleColor = isInterceptor
                ? new Color(1f, 0.5f, 0f) : Color.yellow;

            style.normal.textColor = Color.black;
            GUI.Label(new Rect(x + 1, y - 16f, labelWidth, labelHeight),
                role, style);
            style.normal.textColor = roleColor;
            GUI.Label(new Rect(x, y - 16f, labelWidth, labelHeight),
                role, style);
        }
    }

    

    private void StopSprayEffect()
    {
        if (sprayEffect != null && sprayEffect.isPlaying)
            sprayEffect.Stop();
    }

    private void PickPatrolTarget()
    {
        Vector3 offset = new Vector3(
            Random.Range(-patrolRadius, patrolRadius),
            0f,
            Random.Range(-patrolRadius, patrolRadius));
        patrolTarget = ClampToWorld(transform.position + offset);
    }

    private Vector3 ClampToWorld(Vector3 pos)
    {
        if (SimulationManager.Instance == null) return pos;
        QRect b = SimulationManager.Instance.QuadTree.Bounds;
        pos.x = Mathf.Clamp(pos.x, b.x + 1f, b.x + b.width - 1f);
        pos.z = Mathf.Clamp(pos.z, b.z + 1f, b.z + b.height - 1f);
        pos.y = transform.position.y;
        return pos;
    }

    // XZ-only flat distance — ignores Y completely
    private float XZDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    

    private bool HasNearbyXZ(EntityType type, float radius)
    {
        float r2 = radius * radius;
        foreach (IQuadTreeEntity e in nearbyBuffer)
        {
            if (e.Type != type) continue;
            float dx = e.Position.x - transform.position.x;
            float dz = e.Position.z - transform.position.z;
            if (dx * dx + dz * dz <= r2) return true;
        }
        return false;
    }

    private Vector3 GetNearestPositionXZ(EntityType type, float radius)
    {
        float best = float.MaxValue;
        Vector3 pos = transform.position;
        float r2 = radius * radius;

        foreach (IQuadTreeEntity e in nearbyBuffer)
        {
            if (e.Type != type) continue;
            float dx = e.Position.x - transform.position.x;
            float dz = e.Position.z - transform.position.z;
            float d2 = dx * dx + dz * dz;
            if (d2 <= r2 && d2 < best) { best = d2; pos = e.Position; }
        }
        return pos;
    }

    private IQuadTreeEntity GetNearestEntityXZ(EntityType type, float radius)
    {
        float best = float.MaxValue;
        IQuadTreeEntity result = null;
        float r2 = radius * radius;

        foreach (IQuadTreeEntity e in nearbyBuffer)
        {
            if (e.Type != type) continue;
            float dx = e.Position.x - transform.position.x;
            float dz = e.Position.z - transform.position.z;
            float d2 = dx * dx + dz * dz;
            if (d2 <= r2 && d2 < best) { best = d2; result = e; }
        }
        return result;
    }

    

    private void OnDrawGizmosSelected()
    {
        // Spray radius - green
        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, sprayRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, sprayRadius);

        // Detect radius - cyan
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectRadius);

        // Coordinate radius - yellow
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, coordinateRadius);
    }
}