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

    [Header("Biom-Farben (Für Hand-Vorschau & gebaute Kacheln)")]
    public Color hubColor = new Color(0.95f, 0.98f, 1f);            // Hub: Helles Weiß-Cyan
    public Color forestColor = new Color(0.18f, 0.82f, 0.38f);      // Wald: Smaragdgrün
    public Color oceanColor = new Color(0.15f, 0.52f, 0.95f);       // Meer: Ozeanblau
    public Color ruinsColor = new Color(0.92f, 0.68f, 0.20f);       // Ruinen: Gold-Ocker
    public Color swampColor = new Color(0.45f, 0.85f, 0.65f);       // Sumpf: Gift-Türkis
    public Color nightmareColor = new Color(0.62f, 0.15f, 0.85f);   // Rand: Lila
    public Color blackVoidColor = new Color(0.15f, 0.05f, 0.20f);   // Außenrand: Schwarz-Violett

    private Vector2Int hubCoordinate;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public Color GetZoneColor(ZoneType type)
    {
        return type switch
        {
            ZoneType.Hub => hubColor,
            ZoneType.Forest => forestColor,
            ZoneType.Ocean => oceanColor,
            ZoneType.Ruins => ruinsColor,
            ZoneType.Swamp => swampColor,
            ZoneType.PurpleNightmare => nightmareColor,
            ZoneType.BlackVoid => blackVoidColor,
            _ => forestColor
        };
    }

    public void Initialize(LevelConfiguration config)
    {
        ClearGrid();

        width = config.gridWidth;
        height = config.gridHeight;
        offset = config.cellOffset;
        hubCoordinate = config.startCoordinate;

        GenerateGrid();

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

                // Keine bunten Sektoren mehr im Nebel: Alle unbebauten Felder starten neutral!
                AssignInitialNeutralOrBorderZone(newCell, x, y);

                grid[x, y] = newCell;
            }
        }
    }

    private void AssignInitialNeutralOrBorderZone(GridCell cell, int x, int y)
    {
        int distToEdge = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));

        if (distToEdge <= 1)
        {
            cell.SetZone(ZoneType.BlackVoid, blackVoidColor);
            return;
        }
        if (distToEdge <= 3)
        {
            cell.SetZone(ZoneType.PurpleNightmare, nightmareColor);
            return;
        }

        if (x == hubCoordinate.x && y == hubCoordinate.y)
        {
            cell.SetZone(ZoneType.Hub, hubColor);
            return;
        }

        // Standardmäßig neutraler Nebel; das echte Biom wird erst beim Platzieren des Würfels übertragen!
        cell.SetZone(ZoneType.Forest, forestColor);
    }

    /// <summary>
    /// Überträgt beim Bauen eines Würfel-Blocks dessen ausgewürfeltes Biom direkt auf die Grid-Kachel.
    /// </summary>
    public void PlaceBiomeBlockAt(int x, int y, ZoneType biome)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;

        GridCell cell = grid[x, y];
        if (cell == null) return;

        // Lila/Schwarzer Außenrand behält seine Gefahr, alle normalen Felder nehmen das Würfel-Biom an
        if (cell.zoneType != ZoneType.PurpleNightmare && cell.zoneType != ZoneType.BlackVoid)
        {
            cell.SetZone(biome, GetZoneColor(biome));
        }

        cell.SetOccupied();
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

    public void ResetCellHighlight(Vector2Int coord)
    {
        if (coord.x < 0 || coord.x >= width || coord.y < 0 || coord.y >= height) return;
        if (grid == null || grid[coord.x, coord.y] == null) return;

        grid[coord.x, coord.y].SetWarning(false);
        grid[coord.x, coord.y].UpdateBaseColor();
    }
}