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
    public RoomAttributeType currentAttribute { get; private set; } = RoomAttributeType.None;

    [Header("Renderers")]
    [SerializeField] private SpriteRenderer baseRenderer;
    [SerializeField] private SpriteRenderer overlayRenderer;
    [SerializeField] private SpriteRenderer attributeRenderer;

    public bool isTargeted = false;
    public float pulseSpeed = 4.5f;

    private static readonly Color FogBaseColor = new Color(0.08f, 0.08f, 0.11f, 1f);
    private static readonly Color ColorBlocked = new Color(0.1f, 0.15f, 0.3f, 1f);

    private Color zoneColor = Color.white;
    private Color currentWarningColor = new Color(1f, 0.22f, 0.12f, 1f);

    private static Sprite hazardReticleSprite;

    public bool IsWalkableForSlime => currentState == CellState.Built;
    public bool IsBuildable => currentState == CellState.Empty;

    void Awake()
    {
        if (baseRenderer == null)
        {
            baseRenderer = GetComponent<SpriteRenderer>();
        }

        EnsureAttributeRenderer();
        EnsureHazardOverlayRenderer();
    }

    private void EnsureAttributeRenderer()
    {
        if (attributeRenderer == null || attributeRenderer == baseRenderer)
        {
            Transform existingAttr = transform.Find("CellAttributeIcon");
            if (existingAttr != null)
            {
                attributeRenderer = existingAttr.GetComponent<SpriteRenderer>();
            }

            if (attributeRenderer == null || attributeRenderer == baseRenderer)
            {
                GameObject attrObj = new GameObject("CellAttributeIcon");
                attrObj.transform.SetParent(transform, false);
                attrObj.transform.localPosition = new Vector3(0f, 0f, -0.25f);
                attributeRenderer = attrObj.AddComponent<SpriteRenderer>();
            }
        }

        if (baseRenderer != null)
        {
            attributeRenderer.sortingLayerID = baseRenderer.sortingLayerID;
            attributeRenderer.sortingOrder = baseRenderer.sortingOrder + 3;
        }
        else
        {
            attributeRenderer.sortingOrder = 3;
        }

        attributeRenderer.gameObject.SetActive(false);
    }

    private void EnsureHazardOverlayRenderer()
    {
        EnsureHazardSprite();

        if (overlayRenderer == null || overlayRenderer == baseRenderer)
        {
            Transform existingChild = transform.Find("BossHazardOverlay");
            if (existingChild != null)
            {
                overlayRenderer = existingChild.GetComponent<SpriteRenderer>();
            }

            if (overlayRenderer == null || overlayRenderer == baseRenderer)
            {
                GameObject overlayObj = new GameObject("BossHazardOverlay");
                overlayObj.transform.SetParent(transform, false);
                overlayObj.transform.localPosition = new Vector3(0f, 0f, -0.45f);
                overlayRenderer = overlayObj.AddComponent<SpriteRenderer>();
            }
        }

        overlayRenderer.sprite = hazardReticleSprite;
        if (baseRenderer != null)
        {
            overlayRenderer.sortingLayerID = baseRenderer.sortingLayerID;
            overlayRenderer.sortingOrder = baseRenderer.sortingOrder + 7;
        }
        else
        {
            overlayRenderer.sortingOrder = 7;
        }

        overlayRenderer.gameObject.SetActive(false);
    }

    public void Initialize(int gridX, int gridY)
    {
        x = gridX;
        y = gridY;
        gameObject.name = $"Feld_{x}_{y}";
        SetState(CellState.Empty);
        SetAttribute(RoomAttributeType.None);
        SetWarning(false);
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
        if (currentState == CellState.Empty)
        {
            SetAttribute(RoomAttributeType.None);
        }
        UpdateBaseColor();
    }

    /// <summary>
    /// Weist diesem Feld ein Symbol/Attribut zu, ändert es oder entfernt es (bei RoomAttributeType.None).
    /// </summary>
    public void SetAttribute(RoomAttributeType newAttribute)
    {
        currentAttribute = newAttribute;
        EnsureAttributeRenderer();

        if (currentAttribute == RoomAttributeType.None || RoomAttributeManager.Instance == null)
        {
            attributeRenderer.gameObject.SetActive(false);
            return;
        }

        Sprite icon = RoomAttributeManager.Instance.GetIcon(currentAttribute);
        if (icon != null)
        {
            attributeRenderer.sprite = icon;
            attributeRenderer.color = RoomAttributeManager.Instance.GetIconColor(currentAttribute);
            attributeRenderer.gameObject.SetActive(true);
        }
        else
        {
            attributeRenderer.gameObject.SetActive(false);
        }
    }

    public void ClearAttribute() => SetAttribute(RoomAttributeType.None);

    public void SetOccupied() => SetState(CellState.Built);
    public void DestroyBlock() => SetState(CellState.Empty);
    public void SetBlocked() => SetState(CellState.Blocked);

    public void SetWarning(bool warn, Color warningColor = default)
    {
        isTargeted = warn;
        currentWarningColor = (warningColor == default) ? new Color(1f, 0.22f, 0.12f, 1f) : warningColor;
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
        EnsureHazardOverlayRenderer();
        overlayRenderer.gameObject.SetActive(isTargeted);

        if (isTargeted)
        {
            Color initialCol = currentWarningColor;
            initialCol.a = 0.92f;
            overlayRenderer.color = initialCol;
        }
    }

    void Update()
    {
        if (!isTargeted || overlayRenderer == null) return;

        float pulse = Mathf.PingPong(Time.time * pulseSpeed, 1f);
        Color pulsedColor = currentWarningColor;
        pulsedColor.a = Mathf.Lerp(0.65f, 1.0f, pulse);
        overlayRenderer.color = pulsedColor;

        float scale = Mathf.Lerp(0.94f, 1.02f, pulse);
        overlayRenderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private static void EnsureHazardSprite()
    {
        if (hazardReticleSprite != null) return;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        int center = size / 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inCornerX = (x < 16 || x >= size - 16);
                bool inCornerY = (y < 16 || y >= size - 16);
                bool isOuterRim = (x < 4 || x >= size - 4 || y < 4 || y >= size - 4);
                bool isCornerBracket = isOuterRim && (inCornerX && inCornerY);

                bool isThinBorder = (x == 1 || x == size - 2 || y == 1 || y == size - 2);

                int dx = Mathf.Abs(x - center);
                int dy = Mathf.Abs(y - center);
                bool isCenterReticle = (dx + dy >= 5 && dx + dy <= 8) || (dx <= 1 && dy <= 1);

                bool insideField = (x >= 6 && x < size - 6 && y >= 6 && y < size - 6);
                bool isHazardStripe = insideField && (((x + y) % 14) < 3);

                if (isCornerBracket || isCenterReticle)
                {
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f));
                }
                else if (isThinBorder)
                {
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0.65f));
                }
                else if (isHazardStripe)
                {
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0.32f));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }

        tex.Apply();
        hazardReticleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}