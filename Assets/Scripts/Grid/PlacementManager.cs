using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlacementManager : MonoBehaviour
{
    public GridManager gridManager;
    public EventManager eventManager;
    public InventoryManager inventoryManager;
    public GameManager gameManager;

    // Runden-Begrenzung
    public int maxPlacementsPerTurn = 2;
    public int placementsThisTurn = 0;

    private ShapeData currentShape = null;
    private int currentSlotIndex = -1;
    
    private List<GridCell> lastHoveredCells = new List<GridCell>();

    public bool CanPlaceMoreThisTurn()
    {
        return placementsThisTurn < maxPlacementsPerTurn;
    }

    public void ResetTurnPlacements()
    {
        placementsThisTurn = 0;
    }

    public void SelectShape(ShapeData shape, int slotIndex)
    {
        if (!CanPlaceMoreThisTurn())
        {
            Debug.Log("Platzierungslimit für diese Runde bereits erreicht!");
            return;
        }

        currentShape = shape.Clone();
        currentSlotIndex = slotIndex;
        
        if (gameManager != null)
        {
            gameManager.ChangeState(GameState.Placement);
        }
    }

    public void CancelSelection()
    {
        currentShape = null;
        currentSlotIndex = -1;
        
        foreach (GridCell cell in lastHoveredCells)
        {
            cell.RevertPreview();
        }
        lastHoveredCells.Clear();

        if (gameManager != null)
        {
            gameManager.ChangeState(GameState.FreeRoam);
        }
    }

    void Update()
    {
        if (gameManager != null && gameManager.currentState != GameState.Placement) return;
        if (Mouse.current == null || Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CancelSelection();
            return;
        }

        if (currentShape == null) return;

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            currentShape.RotateClockwise();
        }

        UpdatePreview();

        if (Mouse.current.leftButton.wasPressedThisFrame) 
        {
            TryPlaceShape();
        }
    }

    void UpdatePreview()
    {
        foreach (GridCell cell in lastHoveredCells)
        {
            cell.RevertPreview();
        }
        lastHoveredCells.Clear();

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
        mousePos.z = 0; 

        int startX = Mathf.RoundToInt(mousePos.x / gridManager.offset);
        int startY = Mathf.RoundToInt(mousePos.y / gridManager.offset);

        if (startX >= 0 && startX < gridManager.width && startY >= 0 && startY < gridManager.height)
        {
            bool isValid = CanPlaceShape(startX, startY);

            foreach (ShapeCell cell in currentShape.cells)
            {
                int checkX = startX + cell.localPosition.x;
                int checkY = startY + cell.localPosition.y;

                if (checkX >= 0 && checkX < gridManager.width && checkY >= 0 && checkY < gridManager.height)
                {
                    GridCell hoverCell = gridManager.grid[checkX, checkY];
                    hoverCell.SetPreview(isValid);
                    lastHoveredCells.Add(hoverCell);
                }
            }
        }
    }

    void TryPlaceShape()
    {
        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
        mousePos.z = 0; 

        int startX = Mathf.RoundToInt(mousePos.x / gridManager.offset);
        int startY = Mathf.RoundToInt(mousePos.y / gridManager.offset);

        if (CanPlaceShape(startX, startY))
        {
            foreach (ShapeCell cell in currentShape.cells)
            {
                int x = startX + cell.localPosition.x;
                int y = startY + cell.localPosition.y;
                gridManager.grid[x, y].SetOccupied();

                // --- NEU: Raum für die Mikro-Ebene registrieren ---
                if (MacroGridManager.Instance != null)
                {
                    Vector2Int coord = new Vector2Int(x, y);
                    MacroGridManager.Instance.RegisterRoom(coord, null);
                }
            }
            
            // Nach dem Platzieren aller Blöcke die Türen der aktiven Räume updaten
            if (MacroGridManager.Instance != null)
            {
                MacroGridManager.Instance.UpdateActiveRooms(MacroGridManager.Instance.CurrentCoordinate);
            }

            inventoryManager.ConsumeShape(currentSlotIndex);
            placementsThisTurn++;
            Debug.Log($"Form platziert ({placementsThisTurn}/{maxPlacementsPerTurn} in dieser Runde).");

            CancelSelection();
        }
    }

    bool CanPlaceShape(int startX, int startY)
    {
        bool hasAdjacent = false;

        foreach (ShapeCell cell in currentShape.cells)
        {
            int checkX = startX + cell.localPosition.x;
            int checkY = startY + cell.localPosition.y;

            if (checkX < 0 || checkX >= gridManager.width || checkY < 0 || checkY >= gridManager.height)
                return false; 

            if (!gridManager.grid[checkX, checkY].IsBuildable)
                return false;

            if (IsOccupied(checkX + 1, checkY) || 
                IsOccupied(checkX - 1, checkY) || 
                IsOccupied(checkX, checkY + 1) || 
                IsOccupied(checkX, checkY - 1))
            {
                hasAdjacent = true;
            }
        }
        
        return hasAdjacent; 
    }

    bool IsOccupied(int x, int y)
    {
        if (x < 0 || x >= gridManager.width || y < 0 || y >= gridManager.height)
            return false; 
            
        // Prüft, ob dort wirklich ein gebauter Weg (grau) liegt
        return gridManager.grid[x, y].currentState == CellState.Built; 
    }
}