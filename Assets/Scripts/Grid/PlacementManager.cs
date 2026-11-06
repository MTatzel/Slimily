using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlacementManager : MonoBehaviour
{
    public GridManager gridManager;
    public EventManager eventManager;
    public InventoryManager inventoryManager;
    public GameManager gameManager;

    [Header("Tick-Kosten & Bau-Reichweite (Tether-Regel)")]
    public bool useFocusCost = true;
    public int maxPlacementsPerTurn = 99;
    public int placementsThisTurn = 0;

    [Tooltip("Wenn aktiv, muss mindestens 1 Block des neuen Würfels in Reichweite der aktuellen Slime-Position liegen.")]
    public bool enforceSlimeBuildRange = true;
    [Tooltip("Maximale Distanz (in Feldern) zwischen Slime und dem nächsten Block des neuen Würfels.")]
    public int maxBuildDistanceFromSlime = 2;
    [Tooltip("Wie viel % Albtraum ein gebauter Schrein-Block sofort senkt.")]
    public float nightmareReductionPerShrine = 8f;

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
        if (useFocusCost)
        {
            if (gameManager == null) gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                int cost = (currentShape != null) ? currentShape.tickCost : gameManager.PlaceCubeFocusCost;
                return gameManager.CanAffordFocus(cost);
            }
        }

        return placementsThisTurn < maxPlacementsPerTurn;
    }

    public void ResetTurnPlacements()
    {
        placementsThisTurn = 0;
    }

    public void SelectShape(ShapeData shape, int slotIndex)
    {
        if (shape == null) return;

        currentShape = shape.Clone();
        currentSlotIndex = slotIndex;

        if (!CanPlaceMoreThisTurn())
        {
            currentShape = null;
            currentSlotIndex = -1;
            return;
        }

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

    public void ClearSelection()
    {
        currentShape = null;
        currentSlotIndex = -1;
        ClearHoverPreview();
    }

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
            currentShape.RotateClockwise();
            currentShape.RotateClockwise();
            currentShape.RotateClockwise();
            UpdatePreviewAt(cursorCoord.x, cursorCoord.y);
        }

        // 3. CURSOR-STEUERUNG
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

        bool isValid = CanPlaceShape(startX, startY) && CanPlaceMoreThisTurn();

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
            string placedName = currentShape.shapeName;
            int cardTickCost = currentShape.tickCost;
            int shrineCount = currentShape.CountModifier(BlockModifier.Shrine);

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
            CancelSelection();

            if (gameManager == null) gameManager = GameManager.Instance;

            // Schrein-Bonus direkt beim Platzieren gutschreiben
            if (shrineCount > 0 && gameManager != null)
            {
                float heal = shrineCount * nightmareReductionPerShrine;
                gameManager.AddNightmare(-heal, $"Schrein gebaut ({placedName})");
            }

            // Individuelle Tick-Kosten der Karte (0, 1 oder 2 Ticks) auf der Timeline abrechnen
            if (useFocusCost && gameManager != null && cardTickCost > 0)
            {
                gameManager.AdvanceTicks(cardTickCost, $"Karte '{placedName}' ({cardTickCost} Tick)");
            }
            else if (cardTickCost == 0)
            {
                Debug.Log($"[PlacementManager] '{placedName}' für 0 Ticks gebaut – Boss-Timeline bewegt sich nicht!");
            }
        }
        else
        {
            Debug.Log("[PlacementManager] Ungültige Platzierung (Muss an Gebautes angrenzen & max. 2 Felder vom Slime entfernt sein)!");
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
        bool isWithinSlimeRange = !enforceSlimeBuildRange;

        Vector2Int slimeCoord = Vector2Int.zero;
        if (MacroGridManager.Instance != null)
        {
            slimeCoord = MacroGridManager.Instance.CurrentCoordinate;
        }
        else if (MacroSlimeMarker.Instance != null)
        {
            slimeCoord = new Vector2Int(MacroSlimeMarker.Instance.currentX, MacroSlimeMarker.Instance.currentY);
        }

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

            if (enforceSlimeBuildRange)
            {
                int chebyshevDist = Mathf.Max(Mathf.Abs(checkX - slimeCoord.x), Mathf.Abs(checkY - slimeCoord.y));
                if (chebyshevDist <= maxBuildDistanceFromSlime)
                {
                    isWithinSlimeRange = true;
                }
            }
        }

        return hasAdjacent && isWithinSlimeRange;
    }

    private bool IsOccupied(int x, int y)
    {
        if (x < 0 || x >= gridManager.width || y < 0 || y >= gridManager.height)
            return false;

        return gridManager.grid[x, y].currentState == CellState.Built;
    }
}