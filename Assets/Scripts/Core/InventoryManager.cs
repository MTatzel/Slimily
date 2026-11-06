using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryManager : MonoBehaviour
{
    public GameManager gameManager;
    public PlacementManager placementManager;

    [Header("UI-Elemente (Bestehende Hotbar)")]
    public RectTransform selectionCursor;
    public RectTransform[] uiSlotTransforms;

    [Header("Vorschau-Referenzen")]
    public ShapePreviewUI centerPreview;
    public GameObject uiBlockPrefab;

    [Header("Deckbuilding-Einstellungen (Slay the Spire Prinzip)")]
    [Tooltip("Maximale Anzahl an Handkarten gleichzeitig")]
    public int maxHandSize = 5;
    [Tooltip("Anzahl der gezogenen Startkarten zu Beginn eines Runs")]
    public int startingHandSize = 5;

    // Bestehendes Array bleibt als Spiegelbild der Hand für die UI erhalten
    public ShapeData[] hotbarSlots = new ShapeData[8];

    // Die 3 StS-Stapel + Master-Deck
    private readonly List<ShapeData> masterDeck = new List<ShapeData>();
    private readonly List<ShapeData> drawPile = new List<ShapeData>();
    private readonly List<ShapeData> hand = new List<ShapeData>();
    private readonly List<ShapeData> discardPile = new List<ShapeData>();

    private ShapePreviewUI[] slotPreviews = new ShapePreviewUI[8];
    private int currentSelectedIndex = 0;

    [SerializeField] private float navRepeatDelay = 0.18f;
    private float navRepeatTimer = 0f;

    // Öffentliche Getter für HUD & Relikte
    public int DrawPileCount => drawPile.Count;
    public int HandCount => hand.Count;
    public int DiscardPileCount => discardPile.Count;
    public int MasterDeckCount => masterDeck.Count;
    public int CurrentSelectedIndex => currentSelectedIndex;

    public ShapeData CurrentlySelectedCard =>
        (currentSelectedIndex >= 0 && currentSelectedIndex < hand.Count) ? hand[currentSelectedIndex] : null;

    void Awake()
    {
        InitializeSlotPreviews();
        hotbarSlots = new ShapeData[8];
        BuildStarterMasterDeck();
        ResetDeckAndDrawStartingHand();
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

    // =====================================================================
    // DECK-, DRAW- & DISCARD-LOGIK (SLAY THE SPIRE SYSTEM)
    // =====================================================================

    /// <summary>
    /// Baut das kuratierte 12-Karten-Starterdeck aus Risiko/Ertrag-Würfeln und Taktik-Skills auf.
    /// </summary>
    public void BuildStarterMasterDeck()
    {
        masterDeck.Clear();

        // 1. Verfluchter Korridor (2x): Schnelle gerade Linie, aber mit Feuer-Gefahren (1 Tick)
        masterDeck.Add(CreateCard_CursedCorridor());
        masterDeck.Add(CreateCard_CursedCorridor());

        // 2. Gletscher-Brücke (2x): Langes T-Netz mit rutschigen Eis-Räumen (1 Tick)
        masterDeck.Add(CreateCard_GlacierBridge());
        masterDeck.Add(CreateCard_GlacierBridge());

        // 3. Sicherer Umweg (2x): Verwinkelte Z-Treppe, komplett sicher + 1 Heil-Schrein (-8% Albtraum, 1 Tick)
        masterDeck.Add(CreateCard_SafeDetourShrine());
        masterDeck.Add(CreateCard_SafeDetourShrine());

        // 4. Wellen-Pfad (1x): Solides Standard-Netz ohne Gefahren (1 Tick)
        masterDeck.Add(CreateCard_WaveStandard());

        // 5. Schatten-Sprungbrett (1x): Kostet 0 Ticks beim Bauen, hat aber 3 Gefahren-Räume!
        masterDeck.Add(CreateCard_ShadowSpringboardZeroCost());

        // 6. Architekten-Achse (1x): Perfektes Kreuz + Schrein, kostet aber 2 Ticks!
        masterDeck.Add(CreateCard_ArchitectAxis());

        // 7. Taktik-Skills (3x): Fokus (Karten ziehen), Zeit-Anker (Timeline bremsen), Läuterung (Boss-Cast brechen)
        masterDeck.Add(CreateSkill_MentalFocus());
        masterDeck.Add(CreateSkill_TimeAnchor());
        masterDeck.Add(CreateSkill_PurifyThreats());
    }

    /// <summary>
    /// Mischt zu Run-Beginn alle Karten aus dem Master-Deck in den Nachziehstapel und zieht die Starthand.
    /// </summary>
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

    /// <summary>
    /// Zieht 'amount' Karten vom Nachziehstapel auf die Hand.
    /// Ist der Nachziehstapel leer, wird der Ablagestapel automatisch neu gemischt!
    /// </summary>
    public void DrawCards(int amount)
    {
        int effectiveMaxHand = Mathf.Min(maxHandSize, hotbarSlots.Length);

        for (int i = 0; i < amount; i++)
        {
            if (hand.Count >= effectiveMaxHand)
            {
                Debug.Log($"[Deck] Handlimit ({effectiveMaxHand}) erreicht – keine weitere Karte gezogen.");
                break;
            }

            if (drawPile.Count == 0)
            {
                if (discardPile.Count == 0)
                {
                    Debug.Log("[Deck] Nachzieh- und Ablagestapel sind leer!");
                    break;
                }

                ReshuffleDiscardIntoDrawPile();
            }

            ShapeData drawnCard = drawPile[0];
            drawPile.RemoveAt(0);
            hand.Add(drawnCard);
        }

        SyncHandToHotbarSlots();
        RefreshAllPreviews();
    }

    private void ReshuffleDiscardIntoDrawPile()
    {
        drawPile.AddRange(discardPile);
        discardPile.Clear();
        ShuffleList(drawPile);
        Debug.Log($"[Deck] Ablagestapel wurde gemischt -> {drawPile.Count} Karten zurück im Nachziehstapel!");
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

    /// <summary>
    /// Mulligan: Wirft alle aktuellen Handkarten auf den Ablagestapel und zieht eine frische Hand.
    /// </summary>
    public void MulliganHand()
    {
        while (hand.Count > 0)
        {
            ShapeData card = hand[0];
            hand.RemoveAt(0);
            discardPile.Add(card);
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

    // Abwärtskompatibel für MacroGridManager (Clear-Draw beim Betreten eines neuen Raumes)
    public void AddRandomShape()
    {
        DrawCards(1);
    }

    public void CheckAndRefillIfEmpty()
    {
        if (hand.Count == 0)
        {
            DrawCards(2);
            Debug.Log("[Deck] Hand war komplett leer: Notfall-Nachzug (+2 Karten).");
        }
    }

    /// <summary>
    /// Wird aufgerufen, wenn eine Karte ausgespielt wurde -> wandert auf den Ablagestapel (Discard Pile)!
    /// </summary>
    public void ConsumeShape(int index)
    {
        if (index < 0 || index >= hand.Count) return;

        ShapeData playedCard = hand[index];
        hand.RemoveAt(index);
        discardPile.Add(playedCard);

        SyncHandToHotbarSlots();
        RefreshAllPreviews();
        UpdateSelection();
    }

    // =====================================================================
    // INPUT & KARTEN-AUSWAHL IN DER HOTBAR
    // =====================================================================

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

        // Karte auswählen / aktivieren (X auf Controller, Enter oder Leertaste)
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
        if (currentSelectedIndex < 0 || currentSelectedIndex >= hand.Count)
        {
            Debug.Log("[Deck] Dieser Hand-Slot ist leer!");
            return;
        }

        ShapeData selectedCard = hand[currentSelectedIndex];
        if (selectedCard == null) return;

        // FALL A: TAKTIK- / SKILL-KARTE -> Sofort auslösen!
        if (selectedCard.category == CardCategory.TacticalSkill)
        {
            ExecuteTacticalSkillCard(selectedCard, currentSelectedIndex);
            return;
        }

        // FALL B: 6ER-WÜRFEL-KARTE -> An PlacementManager übergeben
        if (placementManager == null)
        {
            placementManager = FindAnyObjectByType<PlacementManager>();
        }

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

        // Zuerst Karte aus der Hand auf den Ablagestapel legen
        ConsumeShape(slotIndex);

        switch (type)
        {
            case TacticalSkillType.DrawCards:
                DrawCards(power);
                Debug.Log($"[Skill gespielt] '{cardName}' -> +{power} Karten gezogen!");
                break;

            case TacticalSkillType.DelayBossTimeline:
                if (gameManager != null)
                {
                    gameManager.DelayNextBossEvent(power, $"Skill '{cardName}'");
                }
                break;

            case TacticalSkillType.PurifyBossThreats:
                if (gameManager != null && gameManager.ActiveBoss != null)
                {
                    // Entfernt aktive Warnungen auf dem Grid
                    if (GridManager.Instance != null)
                    {
                        foreach (var zone in gameManager.ActiveBoss.ActiveZones)
                        {
                            foreach (var coord in zone.targetCoordinates)
                            {
                                GridManager.Instance.ResetCellHighlight(coord);
                            }
                        }
                    }
                    Debug.Log($"[Skill gespielt] '{cardName}' -> Aktive Boss-Gefahren auf dem Grid geläutert!");
                }
                if (gameManager != null)
                {
                    gameManager.AddNightmare(-5f, $"Läuterung ({cardName})");
                }
                break;

            case TacticalSkillType.ReduceNightmare:
                if (gameManager != null)
                {
                    gameManager.AddNightmare(-power, $"Skill '{cardName}'");
                }
                break;
        }

        if (gameManager != null && cost > 0)
        {
            gameManager.AdvanceTicks(cost, $"Skill '{cardName}' gespielt");
        }
    }

    // =====================================================================
    // KARTEN-FABRIK (RISIKO/ERTRAG-WÜRFEL & TAKTIK-SKILLS)
    // =====================================================================

    private ShapeData CreateCard_CursedCorridor()
    {
        ShapeData card = new ShapeData(
            "Verfluchter Korridor",
            cost: 1,
            desc: "Gerades Kreuz-Netz für schnellen Vorstoß, aber 2 Räume brennen (Feuer-Gefahr).",
            CardCategory.CubeShape
        );
        card.cells.Add(new ShapeCell(new Vector2Int(0, 0)));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 1), BlockModifier.Fire));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 2), BlockModifier.Fire));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 3)));
        card.cells.Add(new ShapeCell(new Vector2Int(-1, 2)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 2)));
        return card;
    }

    private ShapeData CreateCard_GlacierBridge()
    {
        ShapeData card = new ShapeData(
            "Gletscher-Brücke",
            cost: 1,
            desc: "Weites T-Netz mit 2 rutschigen Eis-Sektoren an der Spitze.",
            CardCategory.CubeShape
        );
        card.cells.Add(new ShapeCell(new Vector2Int(0, 0)));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 1)));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 2), BlockModifier.Ice));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 3), BlockModifier.Ice));
        card.cells.Add(new ShapeCell(new Vector2Int(-1, 3)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 3)));
        return card;
    }

    private ShapeData CreateCard_SafeDetourShrine()
    {
        ShapeData card = new ShapeData(
            "Sicherer Schrein-Pfad",
            cost: 1,
            desc: "Verwinkelte Z-Treppe ohne Gefahren. Enthält 1 Schrein (heilt sofort -8% Albtraum).",
            CardCategory.CubeShape
        );
        card.cells.Add(new ShapeCell(new Vector2Int(-1, 0)));
        card.cells.Add(new ShapeCell(new Vector2Int(-1, 1)));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 1), BlockModifier.Shrine));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 2)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 2)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 3)));
        return card;
    }

    private ShapeData CreateCard_WaveStandard()
    {
        ShapeData card = new ShapeData(
            "Ruhige Welle",
            cost: 1,
            desc: "Ausgewogenes 6er-Wellennetz ohne gefährliche Modifikatoren.",
            CardCategory.CubeShape
        );
        card.cells.Add(new ShapeCell(new Vector2Int(0, 0)));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 1)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 1)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 2)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 3)));
        card.cells.Add(new ShapeCell(new Vector2Int(2, 3)));
        return card;
    }

    private ShapeData CreateCard_ShadowSpringboardZeroCost()
    {
        ShapeData card = new ShapeData(
            "Schatten-Sprungbrett",
            cost: 0,
            desc: "KOSTET 0 TICKS! Bringt dich gratis voran, ist aber mit 3 Gefahren-Räumen verseucht.",
            CardCategory.CubeShape
        );
        card.cells.Add(new ShapeCell(new Vector2Int(0, 0)));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 1), BlockModifier.Poison));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 2), BlockModifier.Fire));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 3)));
        card.cells.Add(new ShapeCell(new Vector2Int(-1, 0)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 3), BlockModifier.Poison));
        return card;
    }

    private ShapeData CreateCard_ArchitectAxis()
    {
        ShapeData card = new ShapeData(
            "Architekten-Achse",
            cost: 2,
            desc: "Schweres Bauwerk (2 Ticks): Komplett sichere Gerade + 1 Schrein (-8% Albtraum).",
            CardCategory.CubeShape
        );
        card.cells.Add(new ShapeCell(new Vector2Int(0, 0)));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 1)));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 2), BlockModifier.Shrine));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 3)));
        card.cells.Add(new ShapeCell(new Vector2Int(-1, 1)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 2)));
        return card;
    }

    private ShapeData CreateSkill_MentalFocus()
    {
        ShapeData skill = new ShapeData(
            "SKILL: Mentaler Fokus",
            cost: 0,
            desc: "0 Ticks: Ziehe sofort +2 Karten vom Nachziehstapel.",
            CardCategory.TacticalSkill
        )
        {
            skillType = TacticalSkillType.DrawCards,
            skillPower = 2
        };
        // Kompaktes 2x2-Gold-Symbol für die Slot-Vorschau
        skill.cells.Add(new ShapeCell(new Vector2Int(0, 0), BlockModifier.SkillIcon));
        skill.cells.Add(new ShapeCell(new Vector2Int(1, 0), BlockModifier.SkillIcon));
        skill.cells.Add(new ShapeCell(new Vector2Int(0, 1), BlockModifier.SkillIcon));
        skill.cells.Add(new ShapeCell(new Vector2Int(1, 1), BlockModifier.SkillIcon));
        return skill;
    }

    private ShapeData CreateSkill_TimeAnchor()
    {
        ShapeData skill = new ShapeData(
            "SKILL: Zeit-Anker",
            cost: 1,
            desc: "1 Tick: Verzögert das nächste Boss-Event auf der Timeline um +3 Ticks.",
            CardCategory.TacticalSkill
        )
        {
            skillType = TacticalSkillType.DelayBossTimeline,
            skillPower = 3
        };
        skill.cells.Add(new ShapeCell(new Vector2Int(0, 0), BlockModifier.SkillIcon));
        skill.cells.Add(new ShapeCell(new Vector2Int(-1, 0), BlockModifier.Ice));
        skill.cells.Add(new ShapeCell(new Vector2Int(1, 0), BlockModifier.Ice));
        skill.cells.Add(new ShapeCell(new Vector2Int(0, 1), BlockModifier.SkillIcon));
        skill.cells.Add(new ShapeCell(new Vector2Int(0, -1), BlockModifier.SkillIcon));
        return skill;
    }

    private ShapeData CreateSkill_PurifyThreats()
    {
        ShapeData skill = new ShapeData(
            "SKILL: Läuterung",
            cost: 0,
            desc: "0 Ticks: Entfernt aktive Boss-Markierungen auf dem Grid und senkt Albtraum um -5%.",
            CardCategory.TacticalSkill
        )
        {
            skillType = TacticalSkillType.PurifyBossThreats,
            skillPower = 5
        };
        skill.cells.Add(new ShapeCell(new Vector2Int(0, 0), BlockModifier.Shrine));
        skill.cells.Add(new ShapeCell(new Vector2Int(0, 1), BlockModifier.SkillIcon));
        skill.cells.Add(new ShapeCell(new Vector2Int(0, -1), BlockModifier.SkillIcon));
        return skill;
    }
}