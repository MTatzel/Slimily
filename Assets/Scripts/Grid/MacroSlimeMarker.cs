using UnityEngine;

public class MacroSlimeMarker : MonoBehaviour
{
    public static MacroSlimeMarker Instance { get; private set; }

    [SerializeField] private GridManager gridManager;

    public int currentX { get; private set; }
    public int currentY { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (gridManager == null)
        {
            gridManager = GetComponentInParent<GridManager>();
        }

        // Startposition auf Makro-Grid setzen
        SetGridPosition(gridManager.width / 2, gridManager.height / 2);
    }

    /// <summary>
    /// Wird aufgerufen, wenn der Slime im Platformer einen neuen Raum betritt.
    /// </summary>
    public void SetGridPosition(int gridX, int gridY)
    {
        currentX = gridX;
        currentY = gridY;
        UpdateMarkerPosition();
    }

    public void SetGridPosition(Vector2Int gridCoord)
    {
        SetGridPosition(gridCoord.x, gridCoord.y);
    }

    private void UpdateMarkerPosition()
    {
        if (gridManager == null) return;

        // Lokale Position relativ zum GridManager berechnen (beachtet den 10.000er Parent-Offset)
        float localX = currentX * gridManager.offset;
        float localY = currentY * gridManager.offset;

        transform.localPosition = new Vector3(localX, localY, -1f);
    }
}