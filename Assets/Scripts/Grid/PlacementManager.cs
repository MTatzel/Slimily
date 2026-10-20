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

    [Header("Kameras")]
    [SerializeField] private Camera macroCamera;

    [Header("Gamepad Grid-Cursor")]
    [SerializeField] private Vector2Int cursorCoord;
    [SerializeField] private float moveRepeatDelay = 0.18f;
    private float moveRepeatTimer = 0f;

    private Camera GetActiveRaycastCamera()
    {
        if (macroCamera != null && macroCamera.gameObject.activeInHierarchy) return macroCamera;
        return Camera.main;
    }

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

        // Cursor initial am Slime-Marker ausrichten
        if (MacroSlimeMarker.Instance != null)
        {
            cursorCoord = new Vector2Int(MacroSlimeMarker.Instance.currentX, MacroSlimeMarker.Instance.currentY);
        }
        else if (gridManager != null)
        {
            cursorCoord = new Vector2Int(gridManager.width / 2, gridManager.height / 2);
        }

        if (gameManager != null)
        {
            gameManager.ChangeState(GameState.Placement);
        }

        UpdatePreviewAt(cursorCoord.x, cursorCoord.y);
    }

    /// <summary>
    /// Räumt nur die visuelle Vorschau und Daten auf, ohne einen State-Wechsel zu triggern.
    /// </summary>
    public void ClearSelection()
    {
        currentShape = null;
        currentSlotIndex = -1;
        ClearHoverPreview();
    }

    /// <summary>
    /// Bricht die Platzierung ab und wechselt zurück in den MacroPlan-Zustand.
    /// </summary>
    public void CancelSelection()
    {
        if (currentShape == null && currentSlotIndex == -1) return;

        ClearSelection();

        if (gameManager != null && gameManager.currentState == GameState.Placement)
        {
            gameManager.ChangeState(GameState.MacroPlan);
        }
    }

    void Update()
    {
        if (gameManager != null && gameManager.currentState != GameState.Placement) return;
        if (currentShape == null) return;

        var gamepad = Gamepad.current;
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        // 1. ABBRUCH (Kreis auf PS5 oder Escape)
        bool cancelPressed = (gamepad != null && gamepad.buttonEast.wasPressedThisFrame) ||
                             (keyboard != null && keyboard.escapeKey.wasPressedThisFrame);
        if (cancelPressed)
        {
            CancelSelection();
            return;
        }

        // 2. ROTATION (L1 / R1 oder Q / E / Rechtsklick)
        bool rotateCW = (gamepad != null && gamepad.rightShoulder.wasPressedThisFrame) ||
                        (keyboard != null && keyboard.eKey.wasPressedThisFrame) ||
                        (mouse != null && mouse.rightButton.wasPressedThisFrame);

        bool rotateCCW = (gamepad != null && gamepad.leftShoulder.wasPressedThisFrame) ||
                         (keyboard != null && keyboard.qKey.wasPressedThisFrame);

        if (rotateCW)
        {
            currentShape.RotateClockwise();
            UpdatePreviewAt(cursorCoord.x, cursorCoord.y);
        }
        else if (rotateCCW)
        {
            // 3x im Uhrzeigersinn = 1x gegen den Uhrzeigersinn
            currentShape.RotateClockwise();
            currentShape.RotateClockwise();
            currentShape.RotateClockwise();
            UpdatePreviewAt(cursorCoord.x, cursorCoord.y);
        }

        // 3. CURSOR-STEUERUNG (Gamepad Stick/D-Pad & Pfeiltasten/WASD)
        Vector2Int moveInput = Vector2Int.zero;

        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            Vector2 dpad = gamepad.dpad.ReadValue();

            if (dpad.x > 0.5f || stick.x > 0.55f) moveInput.x = 1;
            else if (dpad.x < -0.5f || stick.x < -0.55f) moveInput.x = -1;

            if (dpad.y > 0.5f || stick.y > 0.55f) moveInput.y = 1;
            else if (dpad.y < -0.5f || stick.y < -0.55f) moveInput.y = -1;
        }

        if (keyboard != null && moveInput == Vector2Int.zero)
        {
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveInput.x = 1;
            else if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveInput.x = -1;

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveInput.y = 1;
            else if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveInput.y = -1;
        }

        if (moveInput != Vector2Int.zero)
        {
            if (moveRepeatTimer <= 0f)
            {
                cursorCoord.x = Mathf.Clamp(cursorCoord.x + moveInput.x, 0, gridManager.width - 1);
                cursorCoord.y = Mathf.Clamp(cursorCoord.y + moveInput.y, 0, gridManager.height - 1);
                UpdatePreviewAt(cursorCoord.x, cursorCoord.y);

                moveRepeatTimer = moveRepeatDelay;
            }
            else
            {
                moveRepeatTimer -= Time.deltaTime;
            }
        }
        else
        {
            moveRepeatTimer = 0f;

            // Optionaler Fallback: Wenn Maus bewegt wird, Mausposition nutzen
            if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 4f)
            {
                Vector2Int mouseCell = GetCellUnderMouse();
                if (mouseCell.x >= 0 && (mouseCell.x != cursorCoord.x || mouseCell.y != cursorCoord.y))
                {
                    cursorCoord = mouseCell;
                    UpdatePreviewAt(cursorCoord.x, cursorCoord.y);
                }
            }
        }

        // 4. PLATZIEREN (Kreuz auf PS5, Enter oder Linksklick)
        bool placePressed = (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame) ||
                            (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) ||
                            (mouse != null && mouse.leftButton.wasPressedThisFrame);

        if (placePressed)
        {
            TryPlaceShapeAt(cursorCoord.x, cursorCoord.y);
        }
    }

    private void UpdatePreviewAt(int startX, int startY)
    {
        ClearHoverPreview();

        if (gridManager == null || currentShape == null) return;

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

    private void ClearHoverPreview()
    {
        foreach (GridCell cell in lastHoveredCells)
        {
            if (cell != null) cell.RevertPreview();
        }
        lastHoveredCells.Clear();
    }

    private void TryPlaceShapeAt(int startX, int startY)
    {
        if (gridManager == null || currentShape == null) return;

        if (CanPlaceShape(startX, startY))
        {
            foreach (ShapeCell cell in currentShape.cells)
            {
                int x = startX + cell.localPosition.x;
                int y = startY + cell.localPosition.y;
                gridManager.grid[x, y].SetOccupied();

                if (MacroGridManager.Instance != null)
                {
                    Vector2Int coord = new Vector2Int(x, y);
                    MacroGridManager.Instance.RegisterRoom(coord, null);
                }
            }

            if (MacroGridManager.Instance != null)
            {
                MacroGridManager.Instance.UpdateActiveRooms(MacroGridManager.Instance.CurrentCoordinate);
            }

            if (inventoryManager != null)
            {
                inventoryManager.ConsumeShape(currentSlotIndex);
            }

            placementsThisTurn++;
            Debug.Log($"Form platziert ({placementsThisTurn}/{maxPlacementsPerTurn} in dieser Runde).");

            CancelSelection();
        }
        else
        {
            Debug.Log("Ungültige Platzierung!");
        }
    }

    private Vector2Int GetCellUnderMouse()
    {
        Camera activeCam = GetActiveRaycastCamera();
        if (activeCam == null || gridManager == null || Mouse.current == null)
            return new Vector2Int(-1, -1);

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 worldPos = activeCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, -activeCam.transform.position.z));
        Vector3 localPos = gridManager.transform.InverseTransformPoint(worldPos);

        int startX = Mathf.RoundToInt(localPos.x / gridManager.offset);
        int startY = Mathf.RoundToInt(localPos.y / gridManager.offset);

        if (startX >= 0 && startX < gridManager.width && startY >= 0 && startY < gridManager.height)
        {
            return new Vector2Int(startX, startY);
        }

        return new Vector2Int(-1, -1);
    }

    private bool CanPlaceShape(int startX, int startY)
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

    private bool IsOccupied(int x, int y)
    {
        if (x < 0 || x >= gridManager.width || y < 0 || y >= gridManager.height)
            return false;

        return gridManager.grid[x, y].currentState == CellState.Built;
    }
}