using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [HideInInspector] public int width;
    [HideInInspector] public int height;
    [HideInInspector] public float offset;

    public GridCell cellPrefab;
    public GridCell[,] grid;

    [Header("Zonen-Farben (Leuchtend für gebaute Kacheln)")]
    public Color hubColor = new Color(0.95f, 0.98f, 1f);            // Helles Weiß-Cyan
    public Color forestColor = new Color(0.18f, 0.82f, 0.38f);      // Tier 1: Smaragdgrün (Wald)
    public Color oceanColor = new Color(0.15f, 0.52f, 0.95f);       // Tier 1: Ozeanblau (Meer)
    public Color ruinsColor = new Color(0.92f, 0.68f, 0.20f);       // Tier 1: Gold-Ocker (Ruinen)
    public Color swampColor = new Color(0.45f, 0.85f, 0.65f);       // Tier 2: Gift-Türkis (Sumpf)
    public Color nightmareColor = new Color(0.62f, 0.15f, 0.85f);   // Rand: Kräftiges Lila
    public Color blackVoidColor = new Color(0.15f, 0.05f, 0.20f);   // Außenrand: Dunkles Violett-Schwarz

    private Vector2Int hubCoordinate;
    private float randomSeedAngleOffset;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void Initialize(LevelConfiguration config)
    {
        ClearGrid();

        width = config.gridWidth;
        height = config.gridHeight;
        offset = config.cellOffset;
        hubCoordinate = config.startCoordinate;

        // Zufällige Drehung der Zonen pro Run für Abwechslung
        randomSeedAngleOffset = Random.Range(0f, 360f);

        GenerateGrid();

        // Startkachel (Hub) direkt besetzen
        if (hubCoordinate.x >= 0 && hubCoordinate.x < width &&
            hubCoordinate.y >= 0 && hubCoordinate.y < height)
        {
            grid[hubCoordinate.x, hubCoordinate.y].SetZone(ZoneType.Hub, hubColor);
            grid[hubCoordinate.x, hubCoordinate.y].SetOccupied();
        }
    }

    private void ClearGrid()
    {
        if (grid == null) return;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] != null)
                {
                    Destroy(grid[x, y].gameObject);
                }
            }
        }
        grid = null;
    }

    private void GenerateGrid()
    {
        grid = new GridCell[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GridCell newCell = Instantiate(cellPrefab, transform);
                newCell.transform.localPosition = new Vector3(x * offset, y * offset, 0);
                newCell.Initialize(x, y);

                AssignZoneToCell(newCell, x, y);

                grid[x, y] = newCell;
            }
        }
    }

    /// <summary>
    /// Teilt das Raster um den Hub herum organisch in Tier-1-Zonen, Tier-2-Zonen und den Albtraum-Rand ein.
    /// </summary>
    private void AssignZoneToCell(GridCell cell, int x, int y)
    {
        // 1. Abstand zum äußeren Rand des Grids prüfen (für lila & schwarze Felder)
        int distToEdge = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));
        float edgeNoise = Mathf.PerlinNoise(x * 0.25f, y * 0.25f) * 2.2f;

        if (distToEdge <= 1 + Mathf.FloorToInt(edgeNoise * 0.5f))
        {
            cell.SetZone(ZoneType.BlackVoid, blackVoidColor);
            return;
        }
        if (distToEdge <= 3 + Mathf.FloorToInt(edgeNoise))
        {
            cell.SetZone(ZoneType.PurpleNightmare, nightmareColor);
            return;
        }

        // 2. Hub-Zentrum prüfen
        Vector2 delta = new Vector2(x - hubCoordinate.x, y - hubCoordinate.y);
        float distFromHub = delta.magnitude;

        if (distFromHub < 0.5f)
        {
            cell.SetZone(ZoneType.Hub, hubColor);
            return;
        }

        // 3. Winkel um den Hub berechnen (mit leichter Wellen-Verzerrung für organische Zonengrenzen)
        float wobble = (Mathf.PerlinNoise(x * 0.15f + 10f, y * 0.15f + 10f) - 0.5f) * 35f;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg + 180f + randomSeedAngleOffset + wobble;
        angle = (angle % 360f + 360f) % 360f;

        float maxPlayableRadius = Mathf.Min(width, height) * 0.5f - 4f;

        // 3 Sektoren à 120 Grad (grenzen alle 3 direkt an den Hub!)
        if (angle < 120f)
        {
            // Sektor 1: Wald (innen) -> Sumpf (Tier 2, liegt weiter außen am Wald, nie direkt am Hub)
            if (distFromHub > maxPlayableRadius * 0.55f)
            {
                cell.SetZone(ZoneType.Swamp, swampColor);
            }
            else
            {
                cell.SetZone(ZoneType.Forest, forestColor);
            }
        }
        else if (angle < 240f)
        {
            // Sektor 2: Meer
            cell.SetZone(ZoneType.Ocean, oceanColor);
        }
        else
        {
            // Sektor 3: Ruinen
            cell.SetZone(ZoneType.Ruins, ruinsColor);
        }
    }

    public void SetCellState(int x, int y, CellState newState)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        grid[x, y].SetState(newState);
    }

    public void SetCellsState(List<Vector2Int> cells, CellState newState)
    {
        if (cells == null) return;
        foreach (var pos in cells)
        {
            SetCellState(pos.x, pos.y, newState);
        }
    }

    public void SetCellsWarning(List<Vector2Int> cells, bool warn)
    {
        if (cells == null) return;
        foreach (var pos in cells)
        {
            if (pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height)
            {
                grid[pos.x, pos.y].SetWarning(warn);
            }
        }
    }

    /// <summary>
    /// Schnittstelle für BossBase: Aktiviert das Warnsignal und färbt die Zelle ein
    /// </summary>
    public void HighlightCell(Vector2Int coord, Color highlightColor)
    {
        if (coord.x < 0 || coord.x >= width || coord.y < 0 || coord.y >= height) return;
        if (grid == null || grid[coord.x, coord.y] == null) return;

        grid[coord.x, coord.y].SetWarning(true, highlightColor);

        var renderer = grid[coord.x, coord.y].GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = highlightColor;
        }
    }

    /// <summary>
    /// Schnittstelle für BossBase: Schaltet die Warnung ab und stellt die Zonenfarbe wieder her
    /// </summary>
    public void ResetCellHighlight(Vector2Int coord)
    {
        if (coord.x < 0 || coord.x >= width || coord.y < 0 || coord.y >= height) return;
        if (grid == null || grid[coord.x, coord.y] == null) return;

        grid[coord.x, coord.y].SetWarning(false);
        grid[coord.x, coord.y].UpdateBaseColor();
    }
}