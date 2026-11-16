using UnityEngine;


public class SimulationHUD : MonoBehaviour
{
    [Header("HUD Settings")]
    [Tooltip("How often the counts update in seconds.")]
    public float updateInterval = 0.5f;

    [Header("Style")]
    public int fontSize = 18;
    public Color civilianColor = new Color(0.2f, 0.8f, 0.2f);
    public Color virusColor = new Color(1f, 0.3f, 0.3f);
    public Color hazmatColor = new Color(0.2f, 0.8f, 1f);
    public Color infectedColor = new Color(1f, 0.6f, 0f);
    public Color panelColor = new Color(0f, 0f, 0f, 0.6f);


    private float updateTimer;
    private int civilianCount;
    private int infectedCount;
    private int virusCount;
    private int hazmatCount;
    private int quadTreeNodes;
    private int quadTreeEntities;

    // Weather
    private string windDirection = "N/A";
    private float windSpeed;
    private float heatLevel;
    private string heatDescription;


    private void Update()
    {
        updateTimer -= Time.deltaTime;
        if (updateTimer <= 0f)
        {
            updateTimer = updateInterval;
            RefreshCounts();
        }
    }

    private void OnGUI()
    {
        DrawPanel();
    }


    private void RefreshCounts()
    {
        if (SimulationManager.Instance == null) return;

        // Entity counts from quadtree
        civilianCount = SimulationManager.Instance.CountNearby(
            Vector3.zero, 99999f, EntityType.Civilian);

        virusCount = SimulationManager.Instance.CountNearby(
            Vector3.zero, 99999f, EntityType.VirusCloud);

        hazmatCount = SimulationManager.Instance.CountNearby(
            Vector3.zero, 99999f, EntityType.SanitizerSquad);

        // Quadtree debug info
        quadTreeNodes = SimulationManager.Instance.QuadTree.NodeCount;
        quadTreeEntities = SimulationManager.Instance.QuadTree.EntityCount;

        // Count infected civilians
        infectedCount = 0;
        var all = SimulationManager.Instance.GetNearbyAll(Vector3.zero, 99999f);
        foreach (var e in all)
        {
            if (e.Type != EntityType.Civilian) continue;
            SimulationEntity se = e as SimulationEntity;
            if (se == null) continue;
            Civilian civ = se.GetComponent<Civilian>();
            if (civ != null && civ.isInfected)
                infectedCount++;
        }

        // Weather info
        if (WeatherSystem.Instance != null)
        {
            windSpeed = WeatherSystem.Instance.WindVector.magnitude;
            windDirection = GetWindDirectionName(WeatherSystem.Instance.WindVector);
            heatLevel = WeatherSystem.Instance.HeatMultiplier;
            heatDescription = heatLevel < 1.5f ? "Cold" :
                              heatLevel < 2f ? "Warm" :
                                                 "Hot";
        }
    }

    private void DrawPanel()
    {
        float panelWidth = 260f;
        float panelHeight = 280f;
        float margin = 10f;
        float x = margin;
        float y = margin;

        // Background panel
        GUI.color = panelColor;
        GUI.DrawTexture(new Rect(x, y, panelWidth, panelHeight),
            Texture2D.whiteTexture);
        GUI.color = Color.white;

        float lineHeight = 26f;
        float textX = x + 12f;
        float textY = y + 10f;

        // Title
        DrawLabel(textX, textY, "SIMULATION STATUS",
            Color.white, fontSize + 2, true);
        textY += lineHeight + 4f;

        // Divider
        DrawLabel(textX, textY, "- - - - - - - - - - -", Color.gray, fontSize - 4);
        textY += lineHeight - 6f;

        // Civilians
        DrawLabel(textX, textY,
            $"Civilians:   {civilianCount}", civilianColor, fontSize);
        textY += lineHeight;

        // Infected
        DrawLabel(textX, textY,
            $"Infected:    {infectedCount}", infectedColor, fontSize);
        textY += lineHeight;

        // Hazmat workers
        DrawLabel(textX, textY,
            $"Hazmat:      {hazmatCount}", hazmatColor, fontSize);
        textY += lineHeight;

        // Divider
        DrawLabel(textX, textY, "- - - - - - - - - - -", Color.gray, fontSize - 4);
        textY += lineHeight - 6f;

        // Weather
        DrawLabel(textX, textY,
            $"Wind: {windDirection} {windSpeed:F1} m/s",
            Color.white, fontSize);
        textY += lineHeight;

        DrawLabel(textX, textY,
            $"Heat: {heatDescription} (x{heatLevel:F1})",
            HeatColour(), fontSize);
        textY += lineHeight;

        // Divider
        DrawLabel(textX, textY, "- - - - - - - - - - -", Color.gray, fontSize - 4);
        textY += lineHeight - 6f;

        // Quadtree info
        DrawLabel(textX, textY,
            $"QT Nodes: {quadTreeNodes}  Entities: {quadTreeEntities}",
            new Color(0.7f, 0.7f, 1f), fontSize - 2);
    }

    private void DrawLabel(float x, float y, string text,
        Color color, int size, bool bold = false)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = size;
        style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        style.normal.textColor = color;
        GUI.Label(new Rect(x, y, 280f, 30f), text, style);
    }

    private Color HeatColour()
    {
        if (heatLevel < 1.5f) return Color.cyan;
        if (heatLevel < 2f) return Color.yellow;
        return Color.red;
    }



    private string GetWindDirectionName(Vector3 wind)
    {
        if (wind.magnitude < 0.1f) return "Calm";
        float angle = Mathf.Atan2(wind.x, wind.z) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        if (angle < 22.5f || angle >= 337.5f) return "N";
        if (angle < 67.5f) return "NE";
        if (angle < 112.5f) return "E";
        if (angle < 157.5f) return "SE";
        if (angle < 202.5f) return "S";
        if (angle < 247.5f) return "SW";
        if (angle < 292.5f) return "W";
        return "NW";
    }
}