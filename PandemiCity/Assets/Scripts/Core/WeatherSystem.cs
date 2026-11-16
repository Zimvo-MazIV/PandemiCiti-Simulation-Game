using UnityEngine;


//manages wind and heat for the simulation (higher heat temps implies stronger virus), increasing infection damage.
//Wind pushes virus clouds. 

public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem Instance { get; private set; }

    [Header("Wind Settings")]
    [Tooltip("How often wind direction changes in seconds.")]
    public float windChangeInterval = 30f;
    [Tooltip("How fast wind direction rotates when changing.")]
    public float windTurnSpeed = 10f;
    [Tooltip("Current wind speed.")]
    public float windSpeed = 2f;
    [Tooltip("Min and max wind speed when randomised.")]
    public float windSpeedMin = 1f;
    public float windSpeedMax = 4f;

    [Header("Heat Settings")]
    [Tooltip("Current heat level between 0 and 1.")]
    [Range(0f, 1f)]
    public float heatLevel = 0.3f;
    [Tooltip("How often heat level changes in seconds.")]
    public float heatChangeInterval = 45f;
    [Tooltip("How fast heat level lerps to its new target.")]
    public float heatChangeSpeed = 0.05f;

    [Header("Debug Info")]
    public Vector3 currentWindVector;
    public float currentHeatMultiplier;

    private Vector3 targetWindDirection;
    private Vector3 currentWindDirection;
    private float windChangeTimer;
    private float heatChangeTimer;
    private float targetHeatLevel;

    //Public accessors used by VirusCloud and Civilian
    public Vector3 WindVector
    {
        get { return currentWindVector; }
    }

    public float HeatMultiplier
    {
        get { return currentHeatMultiplier; }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        currentWindDirection = RandomWindDirection();
        targetWindDirection = currentWindDirection;
        targetHeatLevel = heatLevel;
        windChangeTimer = windChangeInterval;
        heatChangeTimer = heatChangeInterval;
    }
    private void OnDrawGizmos()
    {
        Vector3 origin = transform.position + Vector3.up * 1f;

        // Wind arrow
        Vector3 wind = Application.isPlaying ? currentWindVector : Vector3.right;
        float windStrength = wind.magnitude;

        // Arrow colour goes from blue (calm) to white (strong)
        Gizmos.color = Color.Lerp(Color.blue, Color.white, windStrength / 4f);

        // Arrow shaft
        Vector3 arrowEnd = origin + wind * 5f;
        Gizmos.DrawLine(origin, arrowEnd);

        // Arrow head
        Vector3 windDir = wind.normalized;
        Vector3 right = Vector3.Cross(windDir, Vector3.up).normalized;
        Gizmos.DrawLine(arrowEnd, arrowEnd - windDir * 2f + right * 1f);
        Gizmos.DrawLine(arrowEnd, arrowEnd - windDir * 2f - right * 1f);

        float heat = Application.isPlaying ? heatLevel : 0.3f;

        // Colour goes from blue (cold) to red (hot)
        Gizmos.color = Color.Lerp(Color.blue, Color.red, heat);
        Gizmos.DrawWireSphere(origin, 2f + heat * 3f);
        Gizmos.color = new Color(
            Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.2f);
        Gizmos.DrawSphere(origin, 2f + heat * 3f);
    }

    private void Update()
    {
        UpdateWind();
        UpdateHeat();

        currentWindVector = currentWindDirection * windSpeed;
        currentHeatMultiplier = 1f + heatLevel * 2f;
    }

    private void UpdateWind()
    {
        windChangeTimer -= Time.deltaTime;
        if (windChangeTimer <= 0f)
        {
            targetWindDirection = RandomWindDirection();
            windSpeed = Random.Range(windSpeedMin, windSpeedMax);
            windChangeTimer = windChangeInterval + Random.Range(-5f, 5f);
        }

        currentWindDirection = Vector3.Slerp(
            currentWindDirection,
            targetWindDirection,
            windTurnSpeed * Time.deltaTime * 0.1f).normalized;
    }

    private void UpdateHeat()
    {
        heatChangeTimer -= Time.deltaTime;
        if (heatChangeTimer <= 0f)
        {
            targetHeatLevel = Random.Range(0.1f, 1f);
            heatChangeTimer = heatChangeInterval + Random.Range(-10f, 10f);
        }

        heatLevel = Mathf.Lerp(heatLevel, targetHeatLevel, heatChangeSpeed * Time.deltaTime);
    }

    private Vector3 RandomWindDirection()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;
    }
    private void OnGUI()
    {
        if (!Application.isPlaying) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.white;

        float windStrength = currentWindVector.magnitude;
        string windDir = GetWindDirectionName(currentWindVector);
        string heatDesc = heatLevel < 0.3f ? "Cold (weak virus)" :
                          heatLevel < 0.6f ? "Warm (moderate virus)" :
                                             "Hot (strong virus)";

        GUI.Label(new Rect(10, 10, 300, 30),
            $"Wind: {windDir} ; Speed: {windStrength:F1}", style);
        GUI.Label(new Rect(10, 35, 300, 30),
            $"Heat: {heatLevel:F2} — {heatDesc}", style);
        GUI.Label(new Rect(10, 60, 300, 30),
            $"Infection Multiplier: x{currentHeatMultiplier:F2}", style);
    }

    private string GetWindDirectionName(Vector3 wind)
    {
        if (wind.magnitude < 0.1f) return "Calm";
        float angle = Mathf.Atan2(wind.x, wind.z) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        if (angle < 22.5f || angle >= 337.5f) return "North";
        if (angle < 67.5f) return "North-East";
        if (angle < 112.5f) return "East";
        if (angle < 157.5f) return "South-East";
        if (angle < 202.5f) return "South";
        if (angle < 247.5f) return "South-West";
        if (angle < 292.5f) return "West";
        return "North-West";
    }
}