using UnityEngine;



[RequireComponent(typeof(SimulationEntity))]
public class HealthSpot : MonoBehaviour
{
    [Header("Healing Settings")]
    [Tooltip("How much health is restored per second to a civilian in range.")]
    public float healRate = 20f;
    [Tooltip("Radius within which a civilian is considered to have reached this spot.")]
    public float healRadius = 3f;
    [Tooltip("How many civilians can be healed at once.")]
    public int maxOccupants = 3;

    [Header("Medicine Stock")]
    [Tooltip("Maximum medicine stock available.")]
    public float maxStock = 100f;
    [Tooltip("Current medicine stock.")]
    public float currentStock = 100f;
    [Tooltip("How much stock is consumed per second per occupant being healed.")]
    public float stockConsumeRate = 5f;
    [Tooltip("How much stock recharges per second when not healing.")]
    public float stockRechargeRate = 3f;
    [Tooltip("How long after running out before recharge begins (seconds).")]
    public float rechargeDelay = 10f;

    [Header("UI")]
    [Tooltip("How high above the object the floating label appears.")]
    public float labelHeight = 3f;
    [Tooltip("Size of the floating label text.")]
    public int labelFontSize = 14;

    [Header("Debug")]
    public int currentOccupants = 0;
    public bool isRecharging = false;


    private float rechargeTimer = 0f;
    private Camera mainCamera;

    

    public bool IsFull
    {
        get { return currentOccupants >= maxOccupants; }
    }

    public bool HasStock
    {
        get { return currentStock > 0f; }
    }

    public float HealRate
    {
        get { return HasStock ? healRate : 0f; }
    }

    public float HealRadius
    {
        get { return healRadius; }
    }

    public float StockPercentage
    {
        get { return currentStock / maxStock; }
    }

  

    private void Start()
    {
        currentStock = maxStock;
        mainCamera = Camera.main;
    }

    private void Update()
    {
        UpdateStock();
    }

    private void UpdateStock()
    {
        if (currentOccupants > 0 && HasStock)
        {
            // Deplete stock while healing civilians
            float consume = stockConsumeRate * currentOccupants * Time.deltaTime;
            currentStock = Mathf.Max(0f, currentStock - consume);
            isRecharging = false;
            rechargeTimer = rechargeDelay;
        }
        else
        {
            // Recharge after delay
            if (rechargeTimer > 0f)
            {
                rechargeTimer -= Time.deltaTime;
            }
            else if (currentStock < maxStock)
            {
                currentStock = Mathf.Min(maxStock,
                    currentStock + stockRechargeRate * Time.deltaTime);
                isRecharging = true;
            }
            else
            {
                isRecharging = false;
            }
        }
    }

  

    public void OccupantEntered()
    {
        currentOccupants++;
    }

    public void OccupantLeft()
    {
        currentOccupants = Mathf.Max(0, currentOccupants - 1);
    }

   

    private void OnGUI()
    {
        if (mainCamera == null) return;

        // Convert world position to screen position
        Vector3 worldPos = transform.position + Vector3.up * labelHeight;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        // Don't draw if behind camera
        if (screenPos.z < 0) return;

        // Flip Y for GUI coordinate system
        screenPos.y = Screen.height - screenPos.y;

        float barWidth = 80f;
        float barHeight = 12f;
        float labelWidth = 100f;
        float x = screenPos.x - barWidth * 0.5f;
        float y = screenPos.y - barHeight - 20f;

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x - 2, y - 2, barWidth + 4, barHeight + 4),
            Texture2D.whiteTexture);

        
      
        float pct = StockPercentage;
        Color barColor = pct > 0.5f
            ? Color.Lerp(Color.yellow, Color.green, (pct - 0.5f) * 2f)
            : Color.Lerp(Color.red, Color.yellow, pct * 2f);

        GUI.color = barColor;
        GUI.DrawTexture(new Rect(x, y, barWidth * pct, barHeight),
            Texture2D.whiteTexture);

        // -----------------------
        GUI.color = Color.white;
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = labelFontSize;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;

        string label = isRecharging
            ? $"Restocking... {pct * 100f:F0}%"
            : $"Stock: {pct * 100f:F0}%";

        // Shadow
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(x - labelWidth * 0.5f + barWidth * 0.5f + 1,
            y - 18f + 1, labelWidth, 20f), label, style);

        // Main text
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(x - labelWidth * 0.5f + barWidth * 0.5f,
            y - 18f, labelWidth, 20f), label, style);

        // Occupants indicator
        
        string occupantText = $"{currentOccupants}/{maxOccupants} patients";
        style.fontSize = labelFontSize - 2;

        style.normal.textColor = Color.black;
        GUI.Label(new Rect(x - labelWidth * 0.5f + barWidth * 0.5f + 1,
            y + barHeight + 1, labelWidth, 20f), occupantText, style);

        style.normal.textColor = currentOccupants >= maxOccupants
            ? Color.red : Color.white;
        GUI.Label(new Rect(x - labelWidth * 0.5f + barWidth * 0.5f,
            y + barHeight, labelWidth, 20f), occupantText, style);

        GUI.color = Color.white;
    }

    // Scene view gizmos for testing
  

    private void OnDrawGizmosSelected()
    {
        // Heal radius
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, healRadius);

        // Stock level as filled sphere
        float pct = Application.isPlaying ? StockPercentage : 1f;
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f * pct);
        Gizmos.DrawSphere(transform.position, healRadius);
    }
}