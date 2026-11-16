using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;



public class SimulationManager : MonoBehaviour
{

    [Header("World Bounds")]
    [Tooltip("X position of the bottom-left corner of the simulation world.")]
    public float worldX = -10f;
    [Tooltip("Z position of the bottom-left corner of the simulation world.")]
    public float worldZ = -10f;
    [Tooltip("Total width of the simulation world on the X axis.")]
    public float worldWidth = 20f;
    [Tooltip("Total height of the simulation world on the Z axis.")]
    public float worldHeight = 20f;

    [Header("Quadtree Debug")]
    [Tooltip("Press Q at runtime to toggle both Gizmos and Debug.DrawLine drawing.")]
    public bool showQuadTree = true;
    [Tooltip("Y height at which the debug lines are drawn above the floor.")]
    public float gizmoY = 0.5f;

    [Header("Debug Info")]
    [Tooltip("Read-only. Updated every frame.")]
    public int debugNodeCount = 0;
    public int debugEntityCount = 0;

    
    public static SimulationManager Instance { get; private set; }

    public QuadTree QuadTree { get; private set; }


    private List<IQuadTreeEntity> allEntities = new List<IQuadTreeEntity>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        QRect worldBounds = new QRect(worldX, worldZ, worldWidth, worldHeight);
        QuadTree = new QuadTree(worldBounds);
    }

    private void Update()
    {
        // Toggle quadtree visualisation with Q key (new Input System)
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            showQuadTree = !showQuadTree;
        QuadTree.Rebuild(allEntities);

        // Update inspector debug counters
        debugNodeCount = QuadTree.NodeCount;
        debugEntityCount = QuadTree.EntityCount;

        
        // top of the Game view window.
        if (showQuadTree)
            QuadTree.DrawDebug(gizmoY);


    }

    // OnDrawGizmos draws in the Scene view both in and out of Play mode.
    private void OnDrawGizmos()
    {
        if (!showQuadTree)
            return;

        if (QuadTree == null)
        {
            // Draw world border in edit mode before Play is pressed.
            Gizmos.color = new Color(0.55f, 0.35f, 1.0f, 0.4f);
            DrawWorldBorder();
            return;
        }

        QuadTree.DrawGizmos(gizmoY);
    }


    public void Register(IQuadTreeEntity entity)
    {
        if (!allEntities.Contains(entity))
            allEntities.Add(entity);
    }

    public void Unregister(IQuadTreeEntity entity)
    {
        allEntities.Remove(entity);
    }

    

    public List<IQuadTreeEntity> GetNearby(Vector3 centre, float radius, EntityType type)
    {
        List<IQuadTreeEntity> raw = QuadTree.QueryCircle(centre, radius);
        List<IQuadTreeEntity> filtered = new List<IQuadTreeEntity>();

        foreach (IQuadTreeEntity e in raw)
        {
            if (e.Type == type)
                filtered.Add(e);
        }

        return filtered;
    }

    public List<IQuadTreeEntity> GetNearbyAll(Vector3 centre, float radius)
    {
        return QuadTree.QueryCircle(centre, radius);
    }

    public IQuadTreeEntity GetNearest(Vector3 centre, float maxRadius, EntityType type)
    {
        return QuadTree.QueryNearest(centre, maxRadius, type);
    }

    public int CountNearby(Vector3 centre, float radius, EntityType type)
    {
        List<IQuadTreeEntity> raw = QuadTree.QueryCircle(centre, radius);
        int count = 0;
        foreach (IQuadTreeEntity e in raw)
        {
            if (e.Type == type)
                count++;
        }
        return count;
    }


    private void DrawWorldBorder()
    {
        float x1 = worldX;
        float z1 = worldZ;
        float x2 = worldX + worldWidth;
        float z2 = worldZ + worldHeight;
        float y = gizmoY;

        Gizmos.DrawLine(new Vector3(x1, y, z1), new Vector3(x2, y, z1));
        Gizmos.DrawLine(new Vector3(x2, y, z1), new Vector3(x2, y, z2));
        Gizmos.DrawLine(new Vector3(x2, y, z2), new Vector3(x1, y, z2));
        Gizmos.DrawLine(new Vector3(x1, y, z2), new Vector3(x1, y, z1));
    }
}