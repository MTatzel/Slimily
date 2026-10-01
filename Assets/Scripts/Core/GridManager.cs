using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public int width = 50;
    public int height = 50;
    public GridCell cellPrefab;

    // Das Herzstück deines Prototypen: Ein 2D-Array, das alle Felder speichert
    public GridCell[,] grid; 
    public float offset = 1.1f; // NEU: 1.1 bedeutet, jedes Feld rückt 10% weiter auseinander

    void Start()
    {
        GenerateGrid();
        //CenterCamera();

        int startX = width / 2;
        int startY = height / 2;
        grid[startX, startY].SetOccupied();

        // --- NEU: Mikro-Welt synchronisieren ---
        if (MacroGridManager.Instance != null)
        {
            Vector2Int startCoord = new Vector2Int(startX, startY);
            // Startraum registrieren (null nimmt das defaultRoomPrefab)
            MacroGridManager.Instance.RegisterRoom(startCoord, null);
            // Startraum laden und Kameras aktivieren
            MacroGridManager.Instance.UpdateActiveRooms(startCoord);
        }
    }

    void GenerateGrid()
    {
        grid = new GridCell[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Kachel direkt als Child unter diesem Transform erzeugen
                GridCell newCell = Instantiate(cellPrefab, transform);
                
                // Lokale Position relativ zum GridManager setzen!
                newCell.transform.localPosition = new Vector3(x * offset, y * offset, 0);
                
                newCell.Initialize(x, y);
                grid[x, y] = newCell;
            }
        }
}

    void CenterCamera()
    {
        // GEÄNDERT: Die Berechnung der Mitte muss jetzt auch den Offset berücksichtigen
        float centerX = ((float)width * offset) / 2 - (offset / 2);
        float centerY = ((float)height * offset) / 2 - (offset / 2);
        
        Camera.main.transform.position = new Vector3(centerX, centerY, -10f);
        
        // GEÄNDERT: Die Kamera muss weiter herauszoomen, da das Feld durch den Offset größer ist
        //Camera.main.orthographicSize = (height * offset) / 2 + 2; 
    }

    // 1. Setzt den Zustand einer einzelnen Zelle sicher
    public void SetCellState(int x, int y, CellState newState)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        grid[x, y].SetState(newState);
    }

    // 2. Setzt eine ganze Liste von Koordinaten auf einen Zustand (z.B. Zerstören oder Blockieren)
    public void SetCellsState(List<Vector2Int> cells, CellState newState)
    {
        if (cells == null) return;
        foreach (var pos in cells)
        {
            SetCellState(pos.x, pos.y, newState);
        }
    }

    // 3. Schaltet Telegrafie/Warn-Blinken für eine Liste von Feldern an oder aus
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