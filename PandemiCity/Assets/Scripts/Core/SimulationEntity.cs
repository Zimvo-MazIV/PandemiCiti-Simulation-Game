using UnityEngine;



public class SimulationEntity : MonoBehaviour, IQuadTreeEntity
{
    [Header("Entity Settings")]
    [Tooltip("What type of entity this is. Set this in the Inspector on the prefab.")]
    public EntityType entityType = EntityType.Civilian;



    public Vector3 Position
    {
        get
        {
            if (this == null || gameObject == null) return Vector3.zero;
            return transform.position;
        }
    }

    public EntityType Type
    {
        get { return entityType; }
    }

    

    private void Start()
    {
        if (SimulationManager.Instance != null)
            SimulationManager.Instance.Register(this);
        else
            Debug.LogWarning("SimulationEntity: No SimulationManager found in scene. " +
                             "Make sure a SimulationManager GameObject exists.");
    }

    private void OnDestroy()
    {
        if (SimulationManager.Instance != null)
            SimulationManager.Instance.Unregister(this);
    }
}