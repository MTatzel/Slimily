using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryManager : MonoBehaviour
{
    public GameManager gameManager;
    public PlacementManager placementManager;

    // UI-Elemente
    public RectTransform selectionCursor;
    public RectTransform[] uiSlotTransforms;

    // Vorschau-Referenzen
    public ShapePreviewUI centerPreview;
    public GameObject uiBlockPrefab;

    // Daten-Array für 8 Slots
    public ShapeData[] hotbarSlots = new ShapeData[8];

    private ShapePreviewUI[] slotPreviews = new ShapePreviewUI[8];
    private int currentSelectedIndex = 0;

    // Timer gegen zu schnelles Durchrutschen der Slots mit dem Stick
    [SerializeField] private float navRepeatDelay = 0.18f;
    private float navRepeatTimer = 0f;

    void Awake()
    {
        InitializeSlotPreviews();

        hotbarSlots = new ShapeData[8];
        AddRandomShape();
        AddRandomShape();
        AddRandomShape();
        AddRandomShape();
    }

    void Start()
    {
        FindReferencesIfNeeded();
        RefreshAllPreviews();
        UpdateCursorPosition();
    }

    private void FindReferencesIfNeeded()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (placementManager == null) placementManager = FindAnyObjectByType<PlacementManager>();
    }

    public void InitializeSlotPreviews()
    {
        if (uiSlotTransforms == null) return;

        for (int i = 0; i < uiSlotTransforms.Length; i++)
        {
            if (uiSlotTransforms[i] == null) continue;

            ShapePreviewUI preview = uiSlotTransforms[i].GetComponent<ShapePreviewUI>();
            if (preview == null)
            {
                preview = uiSlotTransforms[i].gameObject.AddComponent<ShapePreviewUI>();
            }

            preview.uiBlockPrefab = uiBlockPrefab;
            preview.container = uiSlotTransforms[i];
            slotPreviews[i] = preview;
        }
    }

    void Update()
    {
        if (gameManager == null || gameManager.currentState != GameState.MacroPlan) return;

        var gamepad = Gamepad.current;
        var keyboard = Keyboard.current;

        // 1. DURCH DIE SLOTS BLÄTTERN (D-Pad / Stick oder A/D / Pfeiltasten)
        int moveDir = 0;

        if (gamepad != null)
        {
            Vector2 dpad = gamepad.dpad.ReadValue();
            Vector2 stick = gamepad.leftStick.ReadValue();

            if (dpad.x < -0.5f || stick.x < -0.55f) moveDir = -1;
            else if (dpad.x > 0.5f || stick.x > 0.55f) moveDir = 1;
        }

        if (keyboard != null && moveDir == 0)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveDir = -1;
            else if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveDir = 1;
        }

        if (moveDir != 0)
        {
            if (navRepeatTimer <= 0f)
            {
                if (moveDir < 0 && currentSelectedIndex > 0)
                {
                    currentSelectedIndex--;
                    UpdateSelection();
                }
                else if (moveDir > 0 && currentSelectedIndex < hotbarSlots.Length - 1)
                {
                    currentSelectedIndex++;
                    UpdateSelection();
                }

                navRepeatTimer = navRepeatDelay;
            }
            else
            {
                navRepeatTimer -= Time.deltaTime;
            }
        }
        else
        {
            navRepeatTimer = 0f;
        }

        // 2. FORM AUSWÄHLEN (X auf Controller, Enter oder Leertaste)
        bool selectPressed = (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame) ||
                             (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame));

        if (selectPressed)
        {
            TrySelectCurrentSlot();
        }
    }

    void UpdateSelection()
    {
        UpdateCursorPosition();
        UpdateCenterPreview();
    }

    public void UpdateCursorPosition()
    {
        if (selectionCursor == null || uiSlotTransforms == null || uiSlotTransforms.Length == 0) return;
        if (currentSelectedIndex >= 0 && currentSelectedIndex < uiSlotTransforms.Length)
        {
            selectionCursor.position = uiSlotTransforms[currentSelectedIndex].position;
        }
    }

    public void ResetSelection()
    {
        currentSelectedIndex = 0;
        UpdateSelection();
    }

    public void RefreshAllPreviews()
    {
        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            if (i < slotPreviews.Length && slotPreviews[i] != null)
            {
                // Etwas kleinere Blockgröße (13f), damit 6er-Würfelnetze sauber in die Slots passen
                slotPreviews[i].DisplayShape(hotbarSlots[i], blockSize: 13f, spacing: 2f);
            }
        }

        UpdateCenterPreview();
    }

    void UpdateCenterPreview()
    {
        if (centerPreview == null || !centerPreview.gameObject.activeInHierarchy) return;

        if (currentSelectedIndex >= 0 && currentSelectedIndex < hotbarSlots.Length && hotbarSlots[currentSelectedIndex] != null)
        {
            centerPreview.DisplayShape(hotbarSlots[currentSelectedIndex], blockSize: 40f, spacing: 4f);
        }
        else
        {
            centerPreview.Clear();
        }
    }

    void TrySelectCurrentSlot()
    {
        if (placementManager == null)
        {
            placementManager = FindAnyObjectByType<PlacementManager>();
        }

        if (placementManager != null && !placementManager.CanPlaceMoreThisTurn())
        {
            Debug.Log("Maximale Platzierungen für diesen Zug erreicht!");
            return;
        }

        if (hotbarSlots[currentSelectedIndex] != null)
        {
            placementManager.SelectShape(hotbarSlots[currentSelectedIndex], currentSelectedIndex);
        }
        else
        {
            Debug.Log($"Slot {currentSelectedIndex + 1} ist leer!");
        }
    }

    public void ConsumeShape(int index)
    {
        if (index < 0 || index >= hotbarSlots.Length) return;

        hotbarSlots[index] = null;

        for (int i = index; i < hotbarSlots.Length - 1; i++)
        {
            hotbarSlots[i] = hotbarSlots[i + 1];
        }
        hotbarSlots[hotbarSlots.Length - 1] = null;

        RefreshAllPreviews();
    }

    public void CheckAndRefillIfEmpty()
    {
        bool isEmpty = true;
        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            if (hotbarSlots[i] != null)
            {
                isEmpty = false;
                break;
            }
        }

        if (isEmpty)
        {
            AddRandomShape();
            AddRandomShape();
            AddRandomShape();
            RefreshAllPreviews();
            Debug.Log("Inventar war leer: Neue 6er-Würfel wurden nachgefüllt.");
        }
    }

    public void AddRandomShape()
    {
        int freeIndex = -1;
        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            if (hotbarSlots[i] == null)
            {
                freeIndex = i;
                break;
            }
        }

        if (freeIndex == -1) return;

        hotbarSlots[freeIndex] = GenerateRandomShape();
    }

    /// <summary>
    /// Erzeugt echte 6er-Würfelnetze (Hexominos) mit 6 zusammenhängenden Kacheln.
    /// </summary>
    private ShapeData GenerateRandomShape()
    {
        int roll = Random.Range(0, 6);

        switch (roll)
        {
            case 0:
            {
                // 1. Klassisches Würfel-Kreuz
                ShapeData shape = new ShapeData("Würfel: Kreuz");
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 0)));
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 1)));
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 2)));
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 3)));
                shape.cells.Add(new ShapeCell(new Vector2Int(-1, 2)));
                shape.cells.Add(new ShapeCell(new Vector2Int(1, 2)));
                return shape;
            }
            case 1:
            {
                // 2. T-Würfelnetz
                ShapeData shape = new ShapeData("Würfel: T-Netz");
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 0)));
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 1)));
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 2)));
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 3), BlockModifier.Ice));
                shape.cells.Add(new ShapeCell(new Vector2Int(-1, 3)));
                shape.cells.Add(new ShapeCell(new Vector2Int(1, 3)));
                return shape;
            }
            case 2:
            {
                // 3. Verschobenes Kreuz (Offset Cross)
                ShapeData shape = new ShapeData("Würfel: Versatz-Kreuz");
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 0)));
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 1)));
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 2)));
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 3)));
                shape.cells.Add(new ShapeCell(new Vector2Int(-1, 1), BlockModifier.Fire));
                shape.cells.Add(new ShapeCell(new Vector2Int(1, 2)));
                return shape;
            }
            case 3:
            {
                // 4. Z-Treppe (2-2-2 Stufen)
                ShapeData shape = new ShapeData("Würfel: Z-Treppe");
                shape.cells.Add(new ShapeCell(new Vector2Int(-1, 0)));
                shape.cells.Add(new ShapeCell(new Vector2Int(-1, 1)));
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 1)));
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 2)));
                shape.cells.Add(new ShapeCell(new Vector2Int(1, 2)));
                shape.cells.Add(new ShapeCell(new Vector2Int(1, 3), BlockModifier.Ice));
                return shape;
            }
            case 4:
            {
                // 5. Flügel-L (Winged L)
                ShapeData shape = new ShapeData("Würfel: Flügel-L");
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 0)));
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 1)));
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 2)));
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 3)));
                shape.cells.Add(new ShapeCell(new Vector2Int(-1, 0)));
                shape.cells.Add(new ShapeCell(new Vector2Int(1, 3), BlockModifier.Fire));
                return shape;
            }
            default:
            {
                // 6. Wellen-Netz (Wave Net)
                ShapeData shape = new ShapeData("Würfel: Welle");
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 0)));
                shape.cells.Add(new ShapeCell(new Vector2Int(0, 1)));
                shape.cells.Add(new ShapeCell(new Vector2Int(1, 1)));
                shape.cells.Add(new ShapeCell(new Vector2Int(1, 2)));
                shape.cells.Add(new ShapeCell(new Vector2Int(1, 3)));
                shape.cells.Add(new ShapeCell(new Vector2Int(2, 3)));
                return shape;
            }
        }
    }
}