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

        GenerateGrid();

        // Startkachel besetzen
        if (config.startCoordinate.x >= 0 && config.startCoordinate.x < width &&
            config.startCoordinate.y >= 0 && config.startCoordinate.y < height)
        {
            grid[config.startCoordinate.x, config.startCoordinate.y].SetOccupied();
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
                grid[x, y] = newCell;
            }
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
}