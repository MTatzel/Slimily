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

    [Header("Erinnerungs-Strom (Deckbuilding)")]
    public int maxHandSize = 5;
    public int startingHandSize = 5;

    public ShapeData[] hotbarSlots = new ShapeData[8];

    private readonly List<ShapeData> masterDeck = new List<ShapeData>();
    private readonly List<ShapeData> drawPile = new List<ShapeData>();
    private readonly List<ShapeData> hand = new List<ShapeData>();
    private readonly List<ShapeData> discardPile = new List<ShapeData>();

    private ShapePreviewUI[] slotPreviews = new ShapePreviewUI[8];
    private int currentSelectedIndex = 0;

    [SerializeField] private float navRepeatDelay = 0.18f;
    private float navRepeatTimer = 0f;

    public int DrawPileCount => drawPile.Count;
    public int HandCount => hand.Count;
    public int DiscardPileCount => discardPile.Count;
    public int CurrentSelectedIndex => currentSelectedIndex;

    public ShapeData CurrentlySelectedCard =>
        (currentSelectedIndex >= 0 && currentSelectedIndex < hand.Count) ? hand[currentSelectedIndex] : null;

    void Awake()
    {
        InitializeSlotPreviews();
        hotbarSlots = new ShapeData[8];
        masterDeck.Clear();
        masterDeck.AddRange(MemoryCardCatalog.CreateStarterDeck());
        ResetDeckAndDrawStartingHand();
    }

    void Start()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (placementManager == null) placementManager = FindAnyObjectByType<PlacementManager>();
        RefreshAllPreviews();
        UpdateCursorPosition();
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

    public void ResetDeckAndDrawStartingHand()
    {
        drawPile.Clear();
        hand.Clear();
        discardPile.Clear();

        foreach (ShapeData card in masterDeck)
        {
            drawPile.Add(card.Clone());
        }

        ShuffleList(drawPile);
        DrawCards(startingHandSize);
    }

    public void DrawCards(int amount)
    {
        int effectiveMaxHand = Mathf.Min(maxHandSize, hotbarSlots.Length);

        for (int i = 0; i < amount; i++)
        {
            if (hand.Count >= effectiveMaxHand) break;

            if (drawPile.Count == 0)
            {
                if (discardPile.Count == 0) break;
                drawPile.AddRange(discardPile);
                discardPile.Clear();
                ShuffleList(drawPile);
            }

            ShapeData drawn = drawPile[0];
            drawPile.RemoveAt(0);
            hand.Add(drawn);
        }

        SyncHandToHotbarSlots();
        RefreshAllPreviews();
    }

    private void ShuffleList(List<ShapeData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            ShapeData temp = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = temp;
        }
    }

    public void MulliganHand()
    {
        while (hand.Count > 0)
        {
            discardPile.Add(hand[0]);
            hand.RemoveAt(0);
        }

        DrawCards(startingHandSize);
        currentSelectedIndex = 0;
        UpdateSelection();
    }

    private void SyncHandToHotbarSlots()
    {
        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            hotbarSlots[i] = (i < hand.Count) ? hand[i] : null;
        }

        if (currentSelectedIndex >= hand.Count && hand.Count > 0)
        {
            currentSelectedIndex = hand.Count - 1;
        }
    }

    public void AddRandomShape() => DrawCards(1);

    public void CheckAndRefillIfEmpty()
    {
        if (hand.Count == 0) DrawCards(2);
    }

    public void ConsumeShape(int index)
    {
        if (index < 0 || index >= hand.Count) return;

        ShapeData played = hand[index];
        hand.RemoveAt(index);
        discardPile.Add(played);

        SyncHandToHotbarSlots();
        RefreshAllPreviews();
        UpdateSelection();
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

        int maxSelectableIndex = Mathf.Max(0, hand.Count - 1);
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
        if (currentSelectedIndex < 0 || currentSelectedIndex >= hand.Count) return;

        ShapeData selectedCard = hand[currentSelectedIndex];
        if (selectedCard == null) return;

        if (selectedCard.category == CardCategory.TacticalSkill)
        {
            ExecuteTacticalSkillCard(selectedCard, currentSelectedIndex);
            return;
        }

        if (placementManager == null) placementManager = FindAnyObjectByType<PlacementManager>();
        if (placementManager != null)
        {
            placementManager.SelectShape(selectedCard, currentSelectedIndex);
        }
    }

    private void ExecuteTacticalSkillCard(ShapeData skillCard, int slotIndex)
    {
        if (gameManager == null) gameManager = GameManager.Instance;

        string cardName = skillCard.shapeName;
        int cost = skillCard.tickCost;
        TacticalSkillType type = skillCard.skillType;
        int power = skillCard.skillPower;

        ConsumeShape(slotIndex);

        switch (type)
        {
            case TacticalSkillType.DrawCards:
                DrawCards(power);
                break;

            case TacticalSkillType.DelayBossTimeline:
                if (gameManager != null) gameManager.DelayNextBossEvent(power, cardName);
                break;

            case TacticalSkillType.PurifyBossThreats:
                if (gameManager != null && gameManager.ActiveBoss != null && GridManager.Instance != null)
                {
                    foreach (var zone in gameManager.ActiveBoss.ActiveZones)
                    {
                        foreach (var coord in zone.targetCoordinates)
                        {
                            GridManager.Instance.ResetCellHighlight(coord);
                        }
                    }
                }
                if (gameManager != null) gameManager.AddNightmare(-power, cardName);
                break;

            case TacticalSkillType.ReduceNightmare:
                if (gameManager != null) gameManager.AddNightmare(-power, cardName);
                break;
        }

        if (gameManager != null && cost > 0)
        {
            gameManager.AdvanceTicks(cost, $"Skill '{cardName}'");
        }
    }
}