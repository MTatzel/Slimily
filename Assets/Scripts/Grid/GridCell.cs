using UnityEngine;

public enum CellState
{
    Empty,      // Weiß: Leeres Feld
    Built,      // Grau: Gebauter Block (Slime kann laufen)
    Blocked     // Dunkelblau: Boss / Hindernis (unpassierbar & unbaubar)
}

public class GridCell : MonoBehaviour
{
    public int x, y;
    public CellState currentState = CellState.Empty;

    [Header("Renderers")]
    [SerializeField] private SpriteRenderer baseRenderer;
    [SerializeField] private SpriteRenderer overlayRenderer;

    public bool isTargeted = false; 
    public bool isHovered = false; 
    public float pulseSpeed = 4f;

    // Grundfarben des Bodens
    private static readonly Color ColorEmpty = Color.white;
    private static readonly Color ColorBuilt = Color.gray;
    private static readonly Color ColorBlocked = new Color(0.1f, 0.2f, 0.6f); // Dunkelblau

    // Aktuelle Warnfarbe für das Overlay
    private Color currentWarningColor = new Color(1f, 0.5f, 0f); // Standard: Orange

    // Hilfseigenschaften für andere Skripte
    public bool IsWalkableForSlime => currentState == CellState.Built;
    public bool IsBuildable => currentState == CellState.Empty;

    void Awake()
    {
        // Falls im Inspector nicht zugewiesen, automatisch zuweisen
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

    // Maus-Vorschau beim Platzieren von Formen
    public void SetPreview(bool isValid)
    {
        isHovered = true;
        if (overlayRenderer != null)
        {
            overlayRenderer.gameObject.SetActive(true);
            // Halbtransparentes Grün oder Rot über die Kachel legen
            overlayRenderer.color = isValid ? new Color(0f, 1f, 0f, 0.45f) : new Color(1f, 0f, 0f, 0.45f);
        }
    }

    public void RevertPreview()
    {
        isHovered = false;
        UpdateOverlay();
    }

    public Color GetBaseColor()
    {
        switch (currentState)
        {
            case CellState.Built: return ColorBuilt;
            case CellState.Blocked: return ColorBlocked;
            default: return ColorEmpty;
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

        if (isHovered) return; // Hover hat Vorrang

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

        // Lässt nur die Deckkraft (Alpha) des Overlays pulsieren
        float alpha = Mathf.PingPong(Time.time * pulseSpeed, 0.5f) + 0.2f; // Pendelt zwischen 0.2 und 0.7
        Color pulsedColor = currentWarningColor;
        pulsedColor.a = alpha;
        overlayRenderer.color = pulsedColor;
    }
}