using UnityEngine;

public enum CellState
{
    Empty,      // Im Nebel (einheitlich dunkel & ruhig)
    Built,      // Gebauter Raum (leuchtet in der Biom-Farbe des gelegten Würfels)
    Blocked     // Hindernis
}

public enum ZoneType
{
    Hub,
    Forest,
    Ocean,
    Ruins,
    Swamp,
    PurpleNightmare,
    BlackVoid
}

public class GridCell : MonoBehaviour
{
    public int x, y;
    public CellState currentState = CellState.Empty;
    public ZoneType zoneType = ZoneType.Forest;

    [Header("Renderers")]
    [SerializeField] private SpriteRenderer baseRenderer;
    [SerializeField] private SpriteRenderer overlayRenderer;

    public bool isTargeted = false;
    public float pulseSpeed = 4f;

    // Ruhiger, einheitlicher Schieferton für unbebaute Nebel-Felder
    private static readonly Color FogBaseColor = new Color(0.08f, 0.08f, 0.11f, 1f);
    private static readonly Color ColorBlocked = new Color(0.1f, 0.15f, 0.3f, 1f);

    private Color zoneColor = Color.white;
    private Color currentWarningColor = new Color(1f, 0.5f, 0f);

    public bool IsWalkableForSlime => currentState == CellState.Built;
    public bool IsBuildable => currentState == CellState.Empty;

    void Awake()
    {
        if (baseRenderer == null)
            baseRenderer = GetComponent<SpriteRenderer>();

        if (overlayRenderer != null)
            overlayRenderer.gameObject.SetActive(false);
    }

    public void Initialize(int gridX, int gridY)
    {
        x = gridX;
        y = gridY;
        gameObject.name = $"Feld_{x}_{y}";
        SetState(CellState.Empty);
    }

    public void SetZone(ZoneType type, Color color)
    {
        zoneType = type;
        zoneColor = color;
        UpdateBaseColor();
    }

    public void SetState(CellState newState)
    {
        currentState = newState;
        UpdateBaseColor();
    }

    public void SetOccupied() => SetState(CellState.Built);
    public void DestroyBlock() => SetState(CellState.Empty);
    public void SetBlocked() => SetState(CellState.Blocked);

    public void SetWarning(bool warn, Color warningColor = default)
    {
        isTargeted = warn;
        currentWarningColor = (warningColor == default) ? new Color(1f, 0.5f, 0f) : warningColor;
        UpdateOverlay();
    }

    public Color GetBaseColor()
    {
        switch (currentState)
        {
            case CellState.Built:
                return zoneColor;

            case CellState.Blocked:
                return ColorBlocked;

            case CellState.Empty:
            default:
                if (zoneType == ZoneType.Hub)
                {
                    return zoneColor * 0.8f;
                }
                if (zoneType == ZoneType.BlackVoid)
                {
                    return new Color(0.03f, 0.02f, 0.04f, 1f);
                }
                if (zoneType == ZoneType.PurpleNightmare)
                {
                    return Color.Lerp(FogBaseColor, zoneColor, 0.25f);
                }

                return FogBaseColor;
        }
    }

    public void UpdateBaseColor()
    {
        if (baseRenderer != null)
        {
            baseRenderer.color = GetBaseColor();
        }
    }

    private void UpdateOverlay()
    {
        if (overlayRenderer == null) return;
        overlayRenderer.gameObject.SetActive(isTargeted);
    }

    void Update()
    {
        if (!isTargeted || overlayRenderer == null) return;

        float alpha = Mathf.PingPong(Time.time * pulseSpeed, 0.5f) + 0.2f;
        Color pulsedColor = currentWarningColor;
        pulsedColor.a = alpha;
        overlayRenderer.color = pulsedColor;
    }
}