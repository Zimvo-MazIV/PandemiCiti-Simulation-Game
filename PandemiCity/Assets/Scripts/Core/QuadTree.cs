using System.Collections.Generic;
using UnityEngine;



// QRect - Axis-aligned bounding rectangle on the XZ plane

public struct QRect
{
    public float x, z, width, height;

    public QRect(float x, float z, float width, float height)
    {
        this.x = x;
        this.z = z;
        this.width = width;
        this.height = height;
    }

    public Vector3 Centre
    {
        get { return new Vector3(x + width * 0.5f, 0f, z + height * 0.5f); }
    }

    public bool Contains(Vector3 point)
    {
        return point.x >= x && point.x <= x + width &&
               point.z >= z && point.z <= z + height;
    }

    public bool Intersects(QRect other)
    {
        return !(other.x > x + width ||
                 other.x + other.width < x ||
                 other.z > z + height ||
                 other.z + other.height < z);
    }

    public bool IntersectsCircle(Vector3 centre, float radius)
    {
        float closestX = Mathf.Clamp(centre.x, x, x + width);
        float closestZ = Mathf.Clamp(centre.z, z, z + height);
        float dx = centre.x - closestX;
        float dz = centre.z - closestZ;
        return (dx * dx + dz * dz) <= (radius * radius);
    }
}


// EntityType - All entity types in the simulation
public enum EntityType
{
    Civilian,
    SanitizerSquad,
    VirusCloud,
    Hospital
}


public interface IQuadTreeEntity
{
    Vector3 Position { get; }
    EntityType Type { get; }
}


public class QuadTreeNode
{
    private const int MAX_CAPACITY = 4;
    private const int MAX_DEPTH = 4;

    public QRect Bounds;
    private int depth;
    private List<IQuadTreeEntity> entities;
    private QuadTreeNode[] children;

    private bool Divided
    {
        get { return children != null; }
    }

    public QuadTreeNode(QRect bounds, int depth = 0)
    {
        Bounds = bounds;
        this.depth = depth;
        entities = new List<IQuadTreeEntity>();
        children = null;
    }

    // Insert
 
    public bool Insert(IQuadTreeEntity entity)
    {
        if (!Bounds.Contains(entity.Position))
            return false;

        //If we have children, try to insert into them first
        if (Divided)
        {
            foreach (QuadTreeNode child in children)
            {
                if (child.Insert(entity))
                    return true;
            }
            //Doesn't fit in any child, store at this level
            entities.Add(entity);
            return true;
        }

        //No children yet 
        entities.Add(entity);

        //Split if over capacity and not at max depth
        if (entities.Count > MAX_CAPACITY && depth < MAX_DEPTH)
            Subdivide();

        return true;
    }

  
    //
    private void Subdivide()
    {
        float hw = Bounds.width * 0.5f;
        float hh = Bounds.height * 0.5f;
        float bx = Bounds.x;
        float bz = Bounds.z;

        children = new QuadTreeNode[4];
        children[0] = new QuadTreeNode(new QRect(bx, bz, hw, hh), depth + 1);
        children[1] = new QuadTreeNode(new QRect(bx + hw, bz, hw, hh), depth + 1);
        children[2] = new QuadTreeNode(new QRect(bx, bz + hh, hw, hh), depth + 1);
        children[3] = new QuadTreeNode(new QRect(bx + hw, bz + hh, hw, hh), depth + 1);

        //Try to push existing entities into children
        List<IQuadTreeEntity> toReinsert = new List<IQuadTreeEntity>(entities);
        entities.Clear();

        foreach (IQuadTreeEntity e in toReinsert)
        {
            bool inserted = false;
            foreach (QuadTreeNode child in children)
            {
                if (child.Insert(e))
                {
                    inserted = true;
                    break;
                }
            }
            //If it doesn't fit in any child (base case), keep at this level
            if (!inserted)
                entities.Add(e);
        }
    }


    // QueryRect
    public List<IQuadTreeEntity> QueryRect(QRect range, List<IQuadTreeEntity> results)
    {
        if (!Bounds.Intersects(range))
            return results;

        foreach (IQuadTreeEntity e in entities)
        {
            if (range.Contains(e.Position))
                results.Add(e);
        }

        if (Divided)
        {
            foreach (QuadTreeNode child in children)
                child.QueryRect(range, results);
        }

        return results;
    }

    // QueryCircle
    public List<IQuadTreeEntity> QueryCircle(Vector3 centre, float radius, List<IQuadTreeEntity> results)
    {
        if (!Bounds.IntersectsCircle(centre, radius))
            return results;

        float r2 = radius * radius;

        foreach (IQuadTreeEntity e in entities)
        {
            Vector3 p = e.Position;
            float dx = p.x - centre.x;
            float dz = p.z - centre.z;
            if (dx * dx + dz * dz <= r2)
                results.Add(e);
        }

        if (Divided)
        {
            foreach (QuadTreeNode child in children)
                child.QueryCircle(centre, radius, results);
        }

        return results;
    }

   
    // QueryNearest
   
    public IQuadTreeEntity QueryNearest(Vector3 centre, float maxRadius, EntityType typeFilter)
    {
        List<IQuadTreeEntity> candidates = new List<IQuadTreeEntity>();
        QueryCircle(centre, maxRadius, candidates);

        IQuadTreeEntity nearest = null;
        float bestDist2 = float.MaxValue;

        foreach (IQuadTreeEntity e in candidates)
        {
            if (e.Type != typeFilter)
                continue;

            Vector3 p = e.Position;
            float dx = p.x - centre.x;
            float dz = p.z - centre.z;
            float d2 = dx * dx + dz * dz;

            if (d2 < bestDist2)
            {
                bestDist2 = d2;
                nearest = e;
            }
        }

        return nearest;
    }

    public int CountNodes()
    {
        if (!Divided) return 1;
        int total = 1;
        foreach (QuadTreeNode c in children)
            total += c.CountNodes();
        return total;
    }

    public int CountEntities()
    {
        int total = entities.Count;
        if (Divided)
        {
            foreach (QuadTreeNode c in children)
                total += c.CountEntities();
        }
        return total;
    }

    // DrawGizmos - visible
    // Depth-faded purple lines 
   
    public void DrawGizmos(float worldY)
    {
        float alpha = Mathf.Max(0.15f, 0.7f - depth * 0.08f);
        Gizmos.color = new Color(0.55f, 0.35f, 1.0f, alpha);

        float x1 = Bounds.x;
        float z1 = Bounds.z;
        float x2 = Bounds.x + Bounds.width;
        float z2 = Bounds.z + Bounds.height;
        float y = worldY;

        Gizmos.DrawLine(new Vector3(x1, y, z1), new Vector3(x2, y, z1));
        Gizmos.DrawLine(new Vector3(x2, y, z1), new Vector3(x2, y, z2));
        Gizmos.DrawLine(new Vector3(x2, y, z2), new Vector3(x1, y, z2));
        Gizmos.DrawLine(new Vector3(x1, y, z2), new Vector3(x1, y, z1));

        if (Divided)
        {
            foreach (QuadTreeNode child in children)
                child.DrawGizmos(worldY);
        }
    }

    public void DrawDebug(float worldY)
    {
        float alpha = Mathf.Max(0.15f, 0.7f - depth * 0.08f);
        Color col = new Color(0.55f, 0.35f, 1.0f, alpha);

        float x1 = Bounds.x;
        float z1 = Bounds.z;
        float x2 = Bounds.x + Bounds.width;
        float z2 = Bounds.z + Bounds.height;
        float y = worldY;

        // Debug.DrawLine persists for one frame only, so this must be
        // called every frame inside Update() to stay visible.
        Debug.DrawLine(new Vector3(x1, y, z1), new Vector3(x2, y, z1), col);
        Debug.DrawLine(new Vector3(x2, y, z1), new Vector3(x2, y, z2), col);
        Debug.DrawLine(new Vector3(x2, y, z2), new Vector3(x1, y, z2), col);
        Debug.DrawLine(new Vector3(x1, y, z2), new Vector3(x1, y, z1), col);

        if (Divided)
        {
            foreach (QuadTreeNode child in children)
                child.DrawDebug(worldY);
        }
    }
}


// QuadTree - Public facade used by SimulationManager
public class QuadTree
{
    private QuadTreeNode root;

    public QRect Bounds
    {
        get { return root.Bounds; }
    }

    public int NodeCount
    {
        get { return root.CountNodes(); }
    }

    public int EntityCount
    {
        get { return root.CountEntities(); }
    }

    public QuadTree(QRect worldBounds)
    {
        root = new QuadTreeNode(worldBounds, 0);
    }

    // Wipe and rebuild every frame from the live entity list.
    public void Rebuild(List<IQuadTreeEntity> allEntities)
    {
        root = new QuadTreeNode(root.Bounds, 0);
        foreach (IQuadTreeEntity e in allEntities)
            root.Insert(e);
    }

    public List<IQuadTreeEntity> QueryRect(QRect range)
    {
        List<IQuadTreeEntity> results = new List<IQuadTreeEntity>();
        return root.QueryRect(range, results);
    }

    public List<IQuadTreeEntity> QueryCircle(Vector3 centre, float radius)
    {
        List<IQuadTreeEntity> results = new List<IQuadTreeEntity>();
        return root.QueryCircle(centre, radius, results);
    }

    public IQuadTreeEntity QueryNearest(Vector3 centre, float maxRadius, EntityType typeFilter)
    {
        return root.QueryNearest(centre, maxRadius, typeFilter);
    }

    public void DrawGizmos(float worldY = 0.05f)
    {
        root.DrawGizmos(worldY);
    }

    public void DrawDebug(float worldY = 0.05f)
    {
        root.DrawDebug(worldY);
    }
}