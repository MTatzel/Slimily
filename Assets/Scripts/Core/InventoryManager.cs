using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryManager : MonoBehaviour
{
    public GameManager gameManager;
    public PlacementManager placementManager;

    [Header("UI-Elemente (Bestehende Slots)")]
    public RectTransform selectionCursor;
    public RectTransform[] uiSlotTransforms;

    [Header("Vorschau-Referenzen")]
    public ShapePreviewUI centerPreview;
    public GameObject uiBlockPrefab;

    [Header("Inventar-Einstellungen (Kein Kartendeck mehr!)")]
    [Tooltip("Mit wie vielen Würfeln der Spieler im Hub startet (z.B. 2).")]
    [Range(1, 5)] public int startingCubeCount = 2;

    [Tooltip("Wie viele Würfel maximal gleichzeitig im Inventar getragen werden können.")]
    [Range(1, 8)] public int maxInventorySize = 5;

    [Header("Vorlagen-Pool für gefundene Würfel (Inspector)")]
    [Tooltip("Optional: Eigene CardAssets als Formen-Pool für gefundene Würfel.")]
    public List<CardAsset> shapePoolAssets = new List<CardAsset>();

    [Tooltip("Optional: Direkt im Inspector bearbeitbare Formen-Vorlagen.")]
    public List<ShapeData> inlineShapePool = new List<ShapeData>();

    [Tooltip("Wenn AUS, werden Feuer/Eis/Gift/Schrein-Mods auf den Würfeln ignoriert (nur reine Biom-Farben).")]
    public bool includeBlockModifiers = false;

    [HideInInspector]
    public ShapeData[] hotbarSlots = new ShapeData[8];

    // Der Formen-Pool (aus dem beim Finden eines Würfels im Raum eine 6er-Geometrie gewählt wird)
    private readonly List<ShapeData> availableShapeTemplates = new List<ShapeData>();

    // Das tatsächliche Inventar des Spielers
    private readonly List<ShapeData> inventoryCubes = new List<ShapeData>();

    private ShapePreviewUI[] slotPreviews = new ShapePreviewUI[8];
    private int currentSelectedIndex = 0;

    [SerializeField] private float navRepeatDelay = 0.18f;
    private float navRepeatTimer = 0f;

    public int HandCount => inventoryCubes.Count;
    public int CurrentSelectedIndex => currentSelectedIndex;

    public ShapeData CurrentlySelectedCard =>
        (currentSelectedIndex >= 0 && currentSelectedIndex < inventoryCubes.Count) ? inventoryCubes[currentSelectedIndex] : null;

    [ContextMenu("Fülle Formen-Pool mit Standard-6er-Würfeln")]
    private void PopulateInlinePoolWithDefaults()
    {
        inlineShapePool = MemoryCardCatalog.CreateStarterDeck();
    }

    void Awake()
    {
        InitializeSlotPreviews();
        hotbarSlots = new ShapeData[8];
    }

    void Start()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (placementManager == null) placementManager = FindAnyObjectByType<PlacementManager>();

        RebuildShapeTemplatesPool();
        ResetInventoryToStartingCubes();

        RefreshAllPreviews();
        UpdateCursorPosition();
    }

    /// <summary>
    /// Baut die Liste der möglichen 6er-Grundformen auf (ignoriert Skills).
    /// </summary>
    public void RebuildShapeTemplatesPool()
    {
        availableShapeTemplates.Clear();
        List<ShapeData> rawCandidates = new List<ShapeData>();

        if (shapePoolAssets != null)
        {
            foreach (CardAsset asset in shapePoolAssets)
            {
                if (asset != null && asset.cardData != null && asset.cardData.cells.Count > 0)
                {
                    rawCandidates.Add(asset.cardData);
                }
            }
        }

        if (inlineShapePool != null)
        {
            foreach (ShapeData shape in inlineShapePool)
            {
                if (shape != null && shape.cells.Count > 0)
                {
                    rawCandidates.Add(shape);
                }
            }
        }

        if (rawCandidates.Count == 0)
        {
            rawCandidates.AddRange(MemoryCardCatalog.CreateStarterDeck());
        }

        foreach (ShapeData candidate in rawCandidates)
        {
            // Nur echte 6er-Bau-Würfel in den Pool aufnehmen
            if (candidate.category != CardCategory.CubeShape) continue;

            ShapeData copy = candidate.Clone();
            copy.tickCost = 1; // Standardkosten 1 Tick

            if (!includeBlockModifiers)
            {
                foreach (ShapeCell cell in copy.cells)
                {
                    cell.modifiers.Clear();
                }
            }

            availableShapeTemplates.Add(copy);
        }
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

    /// <summary>
    /// Leert das Inventar und gibt dem Spieler genau die Startwürfel (z.B. 2 Stück).
    /// </summary>
    public void ResetInventoryToStartingCubes()
    {
        inventoryCubes.Clear();

        if (availableShapeTemplates.Count == 0)
        {
            RebuildShapeTemplatesPool();
        }

        for (int i = 0; i < startingCubeCount; i++)
        {
            AddFoundCubeToInventory(isSecretBonus: false);
        }

        currentSelectedIndex = 0;
        UpdateSelection();
    }

    // Abwärtskompatible Methode für GameManager Reset
    public void ResetDeckAndDrawStartingHand() => ResetInventoryToStartingCubes();

    public bool CanAddCube()
    {
        int limit = Mathf.Min(maxInventorySize, hotbarSlots.Length);
        return inventoryCubes.Count < limit;
    }

    /// <summary>
    /// Wird aufgerufen, wenn der Slime im Platformer-Raum einen Würfel einsammelt!
    /// Erzeugt einen neuen Würfel und färbt ihn anhand der aktuellen Biom-Resonanz oben links ein.
    /// </summary>
    public bool AddFoundCubeToInventory(bool isSecretBonus = false)
    {
        if (!CanAddCube()) return false;

        if (availableShapeTemplates.Count == 0)
        {
            RebuildShapeTemplatesPool();
        }
        if (availableShapeTemplates.Count == 0) return false;

        ShapeData template = availableShapeTemplates[Random.Range(0, availableShapeTemplates.Count)];
        ShapeData newCube = template.Clone();

        foreach (ShapeCell cell in newCube.cells)
        {
            cell.hasAssignedZone = false;
        }

        if (BiomeResonanceManager.Instance != null)
        {
            BiomeResonanceManager.Instance.AssignBiomesToShape(newCube);
        }

        inventoryCubes.Add(newCube);
        SyncInventoryToHotbarSlots();
        RefreshAllPreviews();
        return true;
    }

    public void AddRandomShape() => AddFoundCubeToInventory(false);

    // Bleibt als leerer Hook für EventManager erhalten, zieht aber nichts mehr gratis nach!
    public void CheckAndRefillIfEmpty() { }

    /// <summary>
    /// Entfernt den gebauten Würfel aus dem Inventar (ohne Ablagestapel).
    /// </summary>
    public void ConsumeShape(int index)
    {
        if (index < 0 || index >= inventoryCubes.Count) return;

        inventoryCubes.RemoveAt(index);

        SyncInventoryToHotbarSlots();
        RefreshAllPreviews();
        UpdateSelection();
    }

    private void SyncInventoryToHotbarSlots()
    {
        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            hotbarSlots[i] = (i < inventoryCubes.Count) ? inventoryCubes[i] : null;
        }

        if (currentSelectedIndex >= inventoryCubes.Count && inventoryCubes.Count > 0)
        {
            currentSelectedIndex = inventoryCubes.Count - 1;
        }
    }

    void Update()
    {
        if (gameManager == null || gameManager.currentState != GameState.MacroPlan) return;

        var gamepad = Gamepad.current;
        var keyboard = Keyboard.current;

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

        int maxSelectableIndex = Mathf.Max(0, inventoryCubes.Count - 1);
        if (moveDir != 0)
        {
            if (navRepeatTimer <= 0f)
            {
                if (moveDir < 0 && currentSelectedIndex > 0)
                {
                    currentSelectedIndex--;
                    UpdateSelection();
                }
                else if (moveDir > 0 && currentSelectedIndex < maxSelectableIndex)
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
        if (currentSelectedIndex < 0 || currentSelectedIndex >= inventoryCubes.Count) return;

        ShapeData selectedCube = inventoryCubes[currentSelectedIndex];
        if (selectedCube == null) return;

        if (placementManager == null) placementManager = FindAnyObjectByType<PlacementManager>();
        if (placementManager != null)
        {
            placementManager.SelectShape(selectedCube, currentSelectedIndex);
        }
    }
}