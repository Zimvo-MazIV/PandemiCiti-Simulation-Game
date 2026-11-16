using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// CameraController.cs
// Attach to your Camera GameObject.
// Manual mode: WASD to pan, scroll wheel to zoom.
// Auto mode: press Space to toggle. Camera moves between
// points of interest found via the quadtree.
// Location: Assets/Scripts/Core/CameraController.cs

public class CameraController : MonoBehaviour
{
    [Header("Manual Movement")]
    public float panSpeed = 20f;
    public float zoomSpeed = 10f;
    public float minHeight = 5f;
    public float maxHeight = 80f;

    [Header("Auto Mode")]
    public bool autoMode = false;
    public float autoMoveDuration = 5f;
    public float autoMoveSpeed = 8f;
    public float interestRadius = 20f;

    [Header("Debug")]
    public string currentMode = "Manual";

    private Vector3 autoTarget;
    private float autoTimer;

    private void Start()
    {
        autoTimer = autoMoveDuration;
        autoTarget = transform.position;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            autoMode = !autoMode;
            currentMode = autoMode ? "Auto" : "Manual";
        }

        if (autoMode)
            UpdateAutoMode();
        else
            UpdateManualMode();
    }

    // ---------------------------------------------------------------
    // Manual mode - WASD pan, scroll zoom
    // ---------------------------------------------------------------

    private void UpdateManualMode()
    {
        if (Keyboard.current == null) return;

        Vector3 move = Vector3.zero;

        if (Keyboard.current.wKey.isPressed) move += Vector3.forward;
        if (Keyboard.current.sKey.isPressed) move += Vector3.back;
        if (Keyboard.current.aKey.isPressed) move += Vector3.left;
        if (Keyboard.current.dKey.isPressed) move += Vector3.right;

        transform.position += move * panSpeed * Time.deltaTime;

        // Scroll wheel zoom (move camera up and down)
        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                Vector3 pos = transform.position;
                pos.y = Mathf.Clamp(pos.y - scroll * zoomSpeed * Time.deltaTime,
                    minHeight, maxHeight);
                transform.position = pos;
            }
        }
    }

    // ---------------------------------------------------------------
    // Auto mode - finds interesting areas using quadtree
    // ---------------------------------------------------------------

    private void UpdateAutoMode()
    {
        autoTimer -= Time.deltaTime;
        if (autoTimer <= 0f)
        {
            FindNextInterestPoint();
            autoTimer = autoMoveDuration;
        }

        Vector3 target = new Vector3(autoTarget.x, transform.position.y, autoTarget.z);
        transform.position = Vector3.Lerp(
            transform.position, target, autoMoveSpeed * Time.deltaTime * 0.1f);
    }

    private void FindNextInterestPoint()
    {
        if (SimulationManager.Instance == null) return;

        // Look for the highest density of activity using quadtree queries.
        // Priority: active virus clouds first, then panicking civilians.
        List<IQuadTreeEntity> viruses = SimulationManager.Instance.GetNearbyAll(
            Vector3.zero, 9999f);

        Vector3 bestPos = transform.position;
        int bestScore = 0;

        foreach (IQuadTreeEntity e in viruses)
        {
            if (e.Type != EntityType.VirusCloud) continue;

            // Count civilians near this virus cloud - higher count = more interesting
            int score = SimulationManager.Instance.CountNearby(
                e.Position, interestRadius, EntityType.Civilian);

            if (score > bestScore)
            {
                bestScore = score;
                bestPos = e.Position;
            }
        }

        // If no virus found look for any dense civilian cluster
        if (bestScore == 0)
        {
            foreach (IQuadTreeEntity e in viruses)
            {
                if (e.Type != EntityType.Civilian) continue;
                int score = SimulationManager.Instance.CountNearby(
                    e.Position, interestRadius, EntityType.Civilian);
                if (score > bestScore) { bestScore = score; bestPos = e.Position; }
            }
        }

        autoTarget = bestPos;
    }
}