using UnityEngine;

public enum CellState
{
    Empty,      // Im Nebel (bebaubar, Zone schimmert schwach durch)
    Built,      // Gebauter Raum (leuchtet intensiv in der Zonenfarbe)
    Blocked     // Hindernis (unpassierbar & unbaubar)
}

public enum ZoneType
{
    Hub,                // Sicheres Zentrum
    Forest,             // Tier 1: Wald / Verstrickung (Grün)
    Ocean,              // Tier 1: Meer / Bodenlose Tiefe (Blau)
    Ruins,              // Tier 1: Ruinen / Einsturz (Ocker/Gold)
    Swamp,              // Tier 2: Sumpf (Türkis/Giftgrün - grenzt an Wald)
    PurpleNightmare,    // Äußerer Rand: Erschwerte Albtraum-Räume (Lila)
    BlackVoid           // Ganz außen: Extrem harte / tödliche Räume (Schwarz)
}

public class GridCell : MonoBehaviour
{
    public int x, y;
    public CellState currentState = CellState.Empty;
    public ZoneType zoneType = ZoneType.Forest;

    [Header("Renderers")]
    [SerializeField] private SpriteRenderer baseRenderer;
    [SerializeField] private SpriteRenderer overlayRenderer;

    [Header("Nebel & Zonen-Optik")]
    [Tooltip("Wie stark die Zonenfarbe durch den unentdeckten Nebel schimmert (0.15 - 0.30 empfohlen)")]
    [SerializeField] private float fogTintStrength = 0.22f;

    public bool isTargeted = false;
    public bool isHovered = false;
    public float pulseSpeed = 4f;

    // Grundfarben für Nebel und Blockaden
    private static readonly Color FogBaseColor = new Color(0.07f, 0.07f, 0.09f, 1f);
    private static readonly Color ColorBlocked = new Color(0.1f, 0.15f, 0.3f, 1f);

    // Zugewiesene Zonenfarbe dieser Kachel
    private Color zoneColor = Color.white;

    // Aktuelle Warnfarbe für das Overlay (Boss-Telegrafie)
    private Color currentWarningColor = new Color(1f, 0.5f, 0f);

    // Hilfseigenschaften für andere Skripte
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

    /// <summary>
    /// Weist der Kachel eine Zone und ihre Grundfarbe zu.
    /// </summary>
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

    // Telegrafie mit flexibler Warnfarbe
    public void SetWarning(bool warn, Color warningColor = default)
    {
        isTargeted = warn;
        currentWarningColor = (warningColor == default) ? new Color(1f, 0.5f, 0f) : warningColor;
        UpdateOverlay();
    }

    // Vorschau beim Platzieren von Würfelnetzen
    public void SetPreview(bool isValid)
    {
        isHovered = true;
        if (overlayRenderer != null)
        {
            overlayRenderer.gameObject.SetActive(true);
            overlayRenderer.color = isValid ? new Color(0f, 1f, 0f, 0.55f) : new Color(1f, 0f, 0f, 0.55f);
        }
        else if (baseRenderer != null)
        {
            // Fallback, falls im Prefab kein separater OverlayRenderer zugewiesen ist
            baseRenderer.color = isValid ? new Color(0.2f, 0.9f, 0.2f, 1f) : new Color(0.9f, 0.2f, 0.2f, 1f);
        }
    }

    public void RevertPreview()
    {
        isHovered = false;
        UpdateBaseColor();
        UpdateOverlay();
    }

    public Color GetBaseColor()
    {
        switch (currentState)
        {
            case CellState.Built:
                // Belegte Felder leuchten kräftig in der vollen Farbe ihrer Zone
                return zoneColor;

            case CellState.Blocked:
                return ColorBlocked;

            case CellState.Empty:
            default:
                // Hub ist immer klar sichtbar
                if (zoneType == ZoneType.Hub)
                {
                    return zoneColor * 0.8f;
                }
                // Schwarzer Rand ist fast tiefschwarz
                if (zoneType == ZoneType.BlackVoid)
                {
                    return new Color(0.03f, 0.02f, 0.04f, 1f);
                }
                // Lila Albtraum-Rand hebt sich bedrohlich ab
                if (zoneType == ZoneType.PurpleNightmare)
                {
                    return Color.Lerp(FogBaseColor, zoneColor, 0.42f);
                }

                // Normale Zonen im Nebel: Dunkler Grundton mit durchschimmernder Zonenfarbe
                return Color.Lerp(FogBaseColor, zoneColor, fogTintStrength);
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

        if (isHovered) return;

        if (isTargeted)
        {
            overlayRenderer.gameObject.SetActive(true);
        }
        else
        {
            overlayRenderer.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (isHovered || !isTargeted || overlayRenderer == null) return;

        float alpha = Mathf.PingPong(Time.time * pulseSpeed, 0.5f) + 0.2f;
        Color pulsedColor = currentWarningColor;
        pulsedColor.a = alpha;
        overlayRenderer.color = pulsedColor;
    }
}