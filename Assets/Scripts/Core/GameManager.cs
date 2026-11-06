using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum GameState
{
    FreeRoam,    // Mikro-Ebene: Platformer-Steuerung aktiv
    MapView,     // Makro-Ebene: Reine Karten-Übersicht (keine Bau-UI)
    MacroPlan,   // Makro-Ebene: Bau- & Planungsphase (Hotbar aktiv)
    Placement,   // Makro-Ebene: Form hängt am Cursor zur Platzierung
    EnemyTurn    // Optionaler Makro-Zustand
}

[Serializable]
public class ScheduledTimelineEntry
{
    public TimelineEventType eventType;
    public string label;
    public int ticksRemaining;

    public ScheduledTimelineEntry(TimelineEventType type, string eventLabel, int ticks)
    {
        eventType = type;
        label = eventLabel;
        ticksRemaining = ticks;
    }
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Aktueller Spielstatus")]
    public GameState currentState = GameState.MacroPlan;

    [Header("Timeline & Tick-Kosten")]
    [Tooltip("Abstand in Ticks zwischen zwei großen Boss-Zügen auf der Timeline")]
    [SerializeField] private int maxFocusPerTurn = 4;
    [Tooltip("Ticks bis zum nächsten großen Boss-Zug (wird automatisch mit der Timeline synchronisiert)")]
    [SerializeField] private int currentFocus = 4;
    [Tooltip("Standard-Tick-Kosten für das Platzieren eines Würfels")]
    [SerializeField] private int placeCubeFocusCost = 1;
    [Tooltip("Tick-Kosten für das erstmalige Betreten eines neuen Raumes")]
    [SerializeField] private int enterNewRoomFocusCost = 1;
    [Tooltip("Tick-Kosten für das Durchqueren eines bereits gesäuberten Raumes")]
    [SerializeField] private int enterClearedRoomFocusCost = 0;
    [Tooltip("Straf-Ticks auf der Timeline bei einem Treffer im Platformer")]
    [SerializeField] private int damageFocusCost = 1;
    [Tooltip("Tick-Kosten für 'Durchatmen / Hand neu ziehen' (Taste E oder R3)")]
    [SerializeField] private int mulliganTickCost = 2;

    [Header("Albtraumleiste (0% - 100%, Anpassbar)")]
    [SerializeField] private float currentNightmare = 0f;
    [SerializeField] private float maxNightmare = 100f;
    [SerializeField] private float nightmarePerDamage = 12f;
    [SerializeField] private float nightmarePerTurn = 3f;
    [SerializeField] private float nightmareOnPurpleTile = 10f;
    [SerializeField] private float nightmareOnBlackVoidTile = 25f;
    [SerializeField] private float nightmareOnBossRoomDestroy = 25f;

    [Header("Timeline & Albtraum UI")]
    [SerializeField] private TextMeshProUGUI focusText;
    [SerializeField] private Image nightmareFillImage;
    [SerializeField] private bool showFallbackFocusHUD = true;

    [Header("Level Konfiguration")]
    [SerializeField] private LevelConfiguration currentLevelConfig;

    [Header("Encounter & Event-Referenzen")]
    [SerializeField] private EventManager eventManager;
    private BossBase activeBossInstance;

    [Header("Kameras")]
    [SerializeField] private Camera macroCamera;
    [SerializeField] private Camera mainCamera;

    [Header("UI")]
    public GameObject inventoryScreen;
    public InventoryManager inventoryManager;

    [Header("Hold-Feedback UI (Optional)")]
    [SerializeField] private Image holdProgressImage;
    [SerializeField] private GameObject holdProgressContainer;

    [Header("Spieler-Referenz (Micro)")]
    [SerializeField] private PlatformerSlimeController slimeController;
    [SerializeField] private Rigidbody2D slimeRigidbody;

    [Header("Transition Settings")]
    [SerializeField] private float buildModeHoldDuration = 0.85f;
    [SerializeField] private float cameraTransitionDuration = 0.35f;

    // Die mehrstufige Aktions-Timeline (zeigt die nächsten Events im Voraus)
    private readonly List<ScheduledTimelineEntry> upcomingTimelineEvents = new List<ScheduledTimelineEntry>();

    private float buildModeHoldTimer = 0f;
    private Vector2 cachedSlimeVelocity;
    private Coroutine activeCameraTransition;
    private Coroutine activeRumbleCoroutine;
    private bool isResettingRun = false;
    private int totalTicksElapsed = 0;

    private Vector3 macroOverviewPosition;
    private float macroOverviewSize;

    // Öffentliche Getter
    public int CurrentFocus => currentFocus;
    public int TicksUntilNextEvent => currentFocus;
    public int MaxFocusPerTurn => maxFocusPerTurn;
    public int BossIntervalTicks => maxFocusPerTurn;
    public int TotalTicksElapsed => totalTicksElapsed;
    public IReadOnlyList<ScheduledTimelineEntry> UpcomingTimelineEvents => upcomingTimelineEvents;

    public int PlaceCubeFocusCost => placeCubeFocusCost;
    public int EnterNewRoomFocusCost => enterNewRoomFocusCost;
    public int EnterClearedRoomFocusCost => enterClearedRoomFocusCost;
    public int DamageFocusCost => damageFocusCost;
    public int MulliganTickCost => mulliganTickCost;

    public float CurrentNightmare => currentNightmare;
    public float MaxNightmare => maxNightmare;
    public float NightmareRatio => Mathf.Clamp01(currentNightmare / Mathf.Max(1f, maxNightmare));
    public float NightmarePerTurn => nightmarePerTurn;
    public float NightmareOnPurpleTile => nightmareOnPurpleTile;
    public float NightmareOnBlackVoidTile => nightmareOnBlackVoidTile;
    public float NightmareOnBossRoomDestroy => nightmareOnBossRoomDestroy;

    public float BuildHoldProgress => Mathf.Clamp01(buildModeHoldTimer / buildModeHoldDuration);
    public BossBase ActiveBoss => activeBossInstance;

    public event Action<int, int> OnFocusChanged;
    public event Action<float, float> OnNightmareChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        FindReferencesIfNeeded();

        currentNightmare = 0f;
        totalTicksElapsed = 0;
        InitializeTimelineQueue();

        if (currentLevelConfig != null)
        {
            LoadLevel(currentLevelConfig);
        }
        else
        {
            Debug.LogError("[GameManager] Keine LevelConfiguration zugewiesen!");
        }

        UpdateHoldUI(0f);
        ChangeState(GameState.MacroPlan);
    }

    // =====================================================================
    // MEHRSTUFIGE AKTIONS-TIMELINE & RUMBLE
    // =====================================================================

    /// <summary>
    /// Baut die initiale Vorschau-Leiste mit abwechselnden Umwelt- und Boss-Events auf.
    /// </summary>
    public void InitializeTimelineQueue()
    {
        upcomingTimelineEvents.Clear();

        int interval = Mathf.Max(2, maxFocusPerTurn);
        int halfInterval = Mathf.Max(1, interval / 2);

        // Zyklus 1 & 2 im Voraus planen, damit der Spieler immer 3-4 Events weit blicken kann
        upcomingTimelineEvents.Add(new ScheduledTimelineEntry(TimelineEventType.AmbientNightmareRipple, "Schatten-Beben", halfInterval));
        upcomingTimelineEvents.Add(new ScheduledTimelineEntry(TimelineEventType.BossMajorPulse, "⚡ BOSS-ZUG", interval));
        upcomingTimelineEvents.Add(new ScheduledTimelineEntry(TimelineEventType.AmbientNightmareRipple, "Schatten-Beben", interval + halfInterval));
        upcomingTimelineEvents.Add(new ScheduledTimelineEntry(TimelineEventType.BossMajorPulse, "⚡ BOSS-ZUG", interval * 2));

        SyncCurrentFocusToNextBossEvent();
        NotifyFocusChanged();
    }

    private void SyncCurrentFocusToNextBossEvent()
    {
        foreach (var ev in upcomingTimelineEvents)
        {
            if (ev.eventType == TimelineEventType.BossMajorPulse)
            {
                currentFocus = ev.ticksRemaining;
                return;
            }
        }

        currentFocus = maxFocusPerTurn;
    }

    private void AppendNextTimelineEvent(TimelineEventType finishedType)
    {
        int furthestTick = 0;
        foreach (var ev in upcomingTimelineEvents)
        {
            if (ev.ticksRemaining > furthestTick)
            {
                furthestTick = ev.ticksRemaining;
            }
        }

        int step = Mathf.Max(2, maxFocusPerTurn / 2);
        int nextTick = furthestTick + step;

        string label = finishedType == TimelineEventType.BossMajorPulse ? "⚡ BOSS-ZUG" : "Schatten-Beben";
        upcomingTimelineEvents.Add(new ScheduledTimelineEntry(finishedType, label, nextTick));
    }

    public bool CanAffordFocus(int cost)
    {
        return !isResettingRun;
    }

    public bool TrySpendFocus(int cost, string actionName = "")
    {
        if (isResettingRun) return false;
        AdvanceTicks(cost, actionName);
        return true;
    }

    public void ForceDrainFocus(int amount, string reason = "")
    {
        AdvanceTicks(amount, reason);
    }

    /// <summary>
    /// Schiebt alle Events auf der Timeline um 'ticks' Schritte nach links.
    /// Jedes Event, das 0 erreicht, wird sofort im Hintergrund ausgelöst.
    /// </summary>
    public void AdvanceTicks(int ticks, string reason = "")
    {
        if (ticks <= 0 || isResettingRun) return;

        if (upcomingTimelineEvents.Count == 0)
        {
            InitializeTimelineQueue();
        }

        if (eventManager == null) eventManager = FindAnyObjectByType<EventManager>();

        for (int step = 0; step < ticks; step++)
        {
            totalTicksElapsed++;

            for (int i = 0; i < upcomingTimelineEvents.Count; i++)
            {
                upcomingTimelineEvents[i].ticksRemaining--;
            }

            // Alle fälligen Events (ticksRemaining <= 0) auslösen und hinten neu einreihen
            for (int i = 0; i < upcomingTimelineEvents.Count; i++)
            {
                if (upcomingTimelineEvents[i].ticksRemaining <= 0)
                {
                    TimelineEventType triggeredType = upcomingTimelineEvents[i].eventType;
                    upcomingTimelineEvents.RemoveAt(i);
                    i--;

                    if (eventManager != null)
                    {
                        eventManager.EnqueueTimelineEvent(triggeredType);
                    }

                    AppendNextTimelineEvent(triggeredType);
                }
            }
        }

        upcomingTimelineEvents.Sort((a, b) => a.ticksRemaining.CompareTo(b.ticksRemaining));
        SyncCurrentFocusToNextBossEvent();

        Debug.Log($"[Timeline] +{ticks} Tick(s) ({reason}) -> Nächster Boss-Zug in {currentFocus} Tick(s)");
        NotifyFocusChanged();
    }

    /// <summary>
    /// Schiebt alle anstehenden Events auf der Timeline nach hinten (z. B. durch 'Zeit-Anker').
    /// </summary>
    public void DelayNextBossEvent(int extraTicks, string reason = "")
    {
        if (extraTicks <= 0 || isResettingRun) return;

        foreach (var ev in upcomingTimelineEvents)
        {
            ev.ticksRemaining += extraTicks;
        }

        SyncCurrentFocusToNextBossEvent();
        Debug.Log($"[Timeline] Alle Timeline-Events um +{extraTicks} Ticks verzögert ({reason})!");
        NotifyFocusChanged();
    }

    /// <summary>
    /// Lässt den Slime/Raum im Platformer kurz erbeben, wenn der Boss im Hintergrund agiert.
    /// </summary>
    public void TriggerMicroCameraRumble(float intensity = 0.25f, float duration = 0.3f)
    {
        if (currentState != GameState.FreeRoam || slimeController == null) return;

        if (activeRumbleCoroutine != null)
        {
            StopCoroutine(activeRumbleCoroutine);
        }
        activeRumbleCoroutine = StartCoroutine(MicroRumbleRoutine(intensity, duration));
    }

    private IEnumerator MicroRumbleRoutine(float intensity, float duration)
    {
        // Kurzes Squash-Zucken am Slime + optisches Kamera-Rütteln
        if (slimeController != null)
        {
            slimeController.ApplySquashAndStretch(1f + intensity * 0.45f, 1f - intensity * 0.35f);
        }

        Transform camTransform = mainCamera != null ? mainCamera.transform : null;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float damper = 1f - (elapsed / duration);
            if (camTransform != null)
            {
                Vector2 shake = UnityEngine.Random.insideUnitCircle * (intensity * damper * 0.15f);
                camTransform.position += new Vector3(shake.x, shake.y, 0f);
            }
            yield return null;
        }

        activeRumbleCoroutine = null;
    }

    public void TriggerMulliganRedraw()
    {
        if (isResettingRun) return;

        if (inventoryManager == null) inventoryManager = FindAnyObjectByType<InventoryManager>();
        if (inventoryManager != null)
        {
            inventoryManager.MulliganHand();
        }

        AdvanceTicks(mulliganTickCost, "Durchatmen / Neue Hand gezogen");
    }

    public void RefillFocus()
    {
        InitializeTimelineQueue();
    }

    public void AddNightmare(float amount, string reason = "")
    {
        if (Mathf.Approximately(amount, 0f) || isResettingRun) return;

        currentNightmare = Mathf.Clamp(currentNightmare + amount, 0f, maxNightmare);
        Debug.Log($"[Albtraum] {(amount >= 0 ? "+" : "")}{amount:0.#}% ({reason}) -> Stand: {currentNightmare:0.#}%");

        NotifyNightmareChanged();

        if (currentNightmare >= maxNightmare && !isResettingRun)
        {
            StartCoroutine(WakeUpInHubRoutine());
        }
    }

    public void OnSlimeDamaged()
    {
        if (isResettingRun) return;

        InterruptMeditation();

        if (currentState == GameState.MapView)
        {
            ChangeState(GameState.FreeRoam);
        }

        AddNightmare(nightmarePerDamage, "Treffer im Raum");

        if (!isResettingRun)
        {
            AdvanceTicks(damageFocusCost, "Straf-Tick durch Treffer");
        }
    }

    private IEnumerator WakeUpInHubRoutine()
    {
        isResettingRun = true;
        Debug.LogWarning("=== ALBTRAUM HAT 100% ERREICHT! RUN VORBEI -> ERWACHEN IM HUB ===");

        SetSlimeActive(false);

        if (TurnBannerUI.Instance != null)
        {
            yield return StartCoroutine(TurnBannerUI.Instance.ShowBannerRoutine("ALBTRAUM: 100% - ERWACHEN IM HUB", isEnemyTurn: true, duration: 1.6f));
        }
        else
        {
            yield return new WaitForSeconds(1.2f);
        }

        currentNightmare = 0f;
        totalTicksElapsed = 0;
        NotifyNightmareChanged();
        InitializeTimelineQueue();

        if (inventoryManager == null) inventoryManager = FindAnyObjectByType<InventoryManager>();
        if (inventoryManager != null)
        {
            inventoryManager.ResetDeckAndDrawStartingHand();
        }

        if (currentLevelConfig != null)
        {
            LoadLevel(currentLevelConfig);
        }

        ChangeState(GameState.MacroPlan);
        isResettingRun = false;
    }

    private void NotifyFocusChanged()
    {
        UpdateHUDTexts();
        OnFocusChanged?.Invoke(currentFocus, maxFocusPerTurn);

        if (MacroGridManager.Instance != null)
        {
            MacroGridManager.Instance.RefreshActiveRoomDoors();
        }
    }

    private void NotifyNightmareChanged()
    {
        UpdateHUDTexts();
        OnNightmareChanged?.Invoke(currentNightmare, maxNightmare);
    }

    private string BuildTimelineDisplayString()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("TIMELINE: [JETZT]");

        int count = Mathf.Min(3, upcomingTimelineEvents.Count);
        for (int i = 0; i < count; i++)
        {
            var ev = upcomingTimelineEvents[i];
            sb.Append($" ──► [+{ev.ticksRemaining} {ev.label}]");
        }

        return sb.ToString();
    }

    private string BuildRoomThreatWarningString(out bool currentRoomInDanger)
    {
        currentRoomInDanger = false;
        if (MacroGridManager.Instance == null) return "";

        Vector2Int current = MacroGridManager.Instance.CurrentCoordinate;
        currentRoomInDanger = MacroGridManager.Instance.IsCellTargetedByBoss(current);

        List<string> threatenedDoors = new List<string>();
        if (MacroGridManager.Instance.IsCellTargetedByBoss(current + Vector2Int.up)) threatenedDoors.Add("NORD");
        if (MacroGridManager.Instance.IsCellTargetedByBoss(current + Vector2Int.right)) threatenedDoors.Add("OST");
        if (MacroGridManager.Instance.IsCellTargetedByBoss(current + Vector2Int.down)) threatenedDoors.Add("SÜD");
        if (MacroGridManager.Instance.IsCellTargetedByBoss(current + Vector2Int.left)) threatenedDoors.Add("WEST");

        if (currentRoomInDanger)
        {
            return $"⚠️ AKUTE GEFAHR: DIESER RAUM WIRD IN {currentFocus} TICKS GETROFFEN! (Schnell verlassen oder Läuterung nutzen!)";
        }

        if (threatenedDoors.Count > 0)
        {
            return $"🔥 TÜR-WARNUNG: Roter Rauch an Tür [{string.Join(", ", threatenedDoors)}] -> Nachbarraum ist vom Boss anvisiert!";
        }

        return "";
    }

    private void UpdateHUDTexts()
    {
        if (focusText != null)
        {
            focusText.text = $"{BuildTimelineDisplayString()}   |   ALBTRAUM: {currentNightmare:0}%";
        }

        if (nightmareFillImage != null)
        {
            nightmareFillImage.fillAmount = NightmareRatio;
        }
    }

    private void OnGUI()
    {
        if (!showFallbackFocusHUD || focusText != null) return;

        bool currentRoomInDanger;
        string threatWarning = BuildRoomThreatWarningString(out currentRoomInDanger);

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.fontSize = 14;
        boxStyle.fontStyle = FontStyle.Bold;
        boxStyle.alignment = TextAnchor.UpperLeft;
        boxStyle.normal.textColor = currentRoomInDanger
            ? new Color(1f, 0.25f, 0.25f)
            : (currentFocus > 1 ? new Color(0.3f, 0.95f, 1f) : new Color(1f, 0.65f, 0.25f));

        string roomAndDeckStatus = "";
        if (MacroGridManager.Instance != null)
        {
            Vector2Int coord = MacroGridManager.Instance.CurrentCoordinate;
            int visitedCount = MacroGridManager.Instance.VisitedRoomCount;
            ZoneType currentZone = ZoneType.Hub;
            if (GridManager.Instance != null && GridManager.Instance.grid != null &&
                coord.x >= 0 && coord.x < GridManager.Instance.width &&
                coord.y >= 0 && coord.y < GridManager.Instance.height &&
                GridManager.Instance.grid[coord.x, coord.y] != null)
            {
                currentZone = GridManager.Instance.grid[coord.x, coord.y].zoneType;
            }
            roomAndDeckStatus = $"\nRaum: [{coord.x}, {coord.y}] ({currentZone}) | Erschlossen: {visitedCount}";
        }

        if (inventoryManager == null) inventoryManager = FindAnyObjectByType<InventoryManager>();
        if (inventoryManager != null)
        {
            roomAndDeckStatus += $"   ||   DECK: Nachziehen [{inventoryManager.DrawPileCount}]  |  Hand [{inventoryManager.HandCount}/{inventoryManager.maxHandSize}]  |  Ablage [{inventoryManager.DiscardPileCount}]";
        }

        string hint = $"\n[Taste K: Test-Treffer (-1 Tick)]   [Taste E / R3: Hand neu ziehen (-{mulliganTickCost} Ticks)]   [Taste M / Touchpad: Schnellkarte]";
        GUI.Box(new Rect(16f, 16f, 760f, 122f), BuildTimelineDisplayString() + roomAndDeckStatus + hint, boxStyle);

        // Albtraumleiste
        Rect barBgRect = new Rect(28f, 100f, 736f, 24f);
        Color prevColor = GUI.color;
        GUI.color = new Color(0.12f, 0.05f, 0.18f, 0.95f);
        GUI.Box(barBgRect, GUIContent.none);

        float fillWidth = 732f * NightmareRatio;
        if (fillWidth > 1f)
        {
            GUI.color = Color.Lerp(new Color(0.55f, 0.15f, 0.85f, 1f), new Color(0.95f, 0.1f, 0.25f, 1f), NightmareRatio);
            GUI.Box(new Rect(30f, 102f, fillWidth, 20f), GUIContent.none);
        }

        GUI.color = Color.white;
        GUIStyle barLabelStyle = new GUIStyle(GUI.skin.label);
        barLabelStyle.alignment = TextAnchor.MiddleCenter;
        barLabelStyle.fontStyle = FontStyle.Bold;
        barLabelStyle.fontSize = 13;
        GUI.Label(barBgRect, $"ALBTRAUM: {currentNightmare:0}% / {maxNightmare:0}%", barLabelStyle);

        float nextBoxY = 146f;

        // Rote Umwelt-Warnbox einblenden, wenn aktueller Raum oder eine Nachbartür bedroht ist
        if (!string.IsNullOrEmpty(threatWarning))
        {
            GUI.color = currentRoomInDanger ? new Color(0.65f, 0.05f, 0.05f, 0.95f) : new Color(0.45f, 0.15f, 0.05f, 0.92f);
            Rect warnRect = new Rect(16f, nextBoxY, 760f, 34f);
            GUI.Box(warnRect, GUIContent.none);

            GUI.color = Color.white;
            GUIStyle warnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(warnRect, threatWarning, warnStyle);
            nextBoxY += 40f;
        }

        // Karten-Details im Baumodus (MacroPlan)
        if (currentState == GameState.MacroPlan && inventoryManager != null)
        {
            ShapeData selected = inventoryManager.CurrentlySelectedCard;
            if (selected != null)
            {
                GUI.color = new Color(0.08f, 0.1f, 0.14f, 0.95f);
                Rect cardInfoRect = new Rect(16f, nextBoxY, 760f, 62f);
                GUI.Box(cardInfoRect, GUIContent.none);

                GUI.color = selected.category == CardCategory.TacticalSkill
                    ? new Color(1f, 0.85f, 0.3f)
                    : new Color(0.5f, 1f, 0.75f);

                GUIStyle cardStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true
                };

                string actionHint = selected.category == CardCategory.TacticalSkill
                    ? "[X / Enter: Skill sofort auslösen]"
                    : "[X / Enter: Würfel auf Grid platzieren]";

                string cardText = $"  KARTE: {selected.shapeName}   (Kosten: {selected.tickCost} Tick{(selected.tickCost == 1 ? "" : "s")})   {actionHint}\n  Effekt: {selected.description}";
                GUI.Label(cardInfoRect, cardText, cardStyle);
            }
        }

        GUI.color = prevColor;
    }

    // =====================================================================
    // LEVEL & STATE MANAGEMENT
    // =====================================================================

    public void LoadLevel(LevelConfiguration config)
    {
        currentLevelConfig = config;

        if (GridManager.Instance != null)
        {
            GridManager.Instance.Initialize(config);
        }

        if (MacroGridManager.Instance != null)
        {
            MacroGridManager.Instance.Initialize(config);

            if (slimeController != null)
            {
                MacroGridManager.Instance.PlaceSlimeInRoom(config.startCoordinate, slimeController.transform);
            }
        }

        SetupLevelEncounter(config);
        CalculateMacroOverviewParameters(config);
        UpdateHUDTexts();
    }

    private void SetupLevelEncounter(LevelConfiguration config)
    {
        if (activeBossInstance != null)
        {
            Destroy(activeBossInstance.gameObject);
            activeBossInstance = null;
        }

        if (eventManager == null)
        {
            eventManager = FindAnyObjectByType<EventManager>();
        }

        if (config != null && config.bossPrefab != null)
        {
            Transform spawnParent = transform.parent != null ? transform.parent : transform;
            activeBossInstance = Instantiate(config.bossPrefab, spawnParent);

            float posX = config.bossSpawnCoordinate.x * config.cellOffset;
            float posY = config.bossSpawnCoordinate.y * config.cellOffset;
            activeBossInstance.transform.localPosition = new Vector3(posX, posY, 0f);

            if (eventManager != null)
            {
                eventManager.boss = activeBossInstance;
            }
        }
        else
        {
            if (eventManager != null)
            {
                eventManager.boss = null;
            }
        }
    }

    private void CalculateMacroOverviewParameters(LevelConfiguration config)
    {
        if (macroCamera == null)
        {
            macroCamera = GameObject.Find("MacroCamera")?.GetComponent<Camera>();
        }

        float centerX = (config.gridWidth * config.cellOffset) / 2f - (config.cellOffset / 2f);
        float centerY = (config.gridHeight * config.cellOffset) / 2f - (config.cellOffset / 2f);

        macroOverviewPosition = new Vector3(centerX, centerY, -10f);
        macroOverviewSize = (config.gridHeight * config.cellOffset) / 2f + 2f;

        if (macroCamera != null)
        {
            macroCamera.transform.localPosition = macroOverviewPosition;
            macroCamera.orthographicSize = macroOverviewSize;
        }
    }

    private void Update()
    {
        if (isResettingRun) return;
        HandleTransitionInputs();
    }

    private void HandleTransitionInputs()
    {
        if (activeCameraTransition != null) return;

        var gamepad = Gamepad.current;
        var keyboard = Keyboard.current;

        bool mulliganTriggered = (gamepad != null && gamepad.rightStickButton.wasPressedThisFrame) ||
                                 (keyboard != null && keyboard.eKey.wasPressedThisFrame);

        if (mulliganTriggered && (currentState == GameState.FreeRoam || currentState == GameState.MacroPlan))
        {
            TriggerMulliganRedraw();
            return;
        }

        bool touchpadPressed = false;
        if (gamepad != null)
        {
            if (gamepad is UnityEngine.InputSystem.DualShock.DualShockGamepad dualShock)
            {
                touchpadPressed = dualShock.touchpadButton.wasPressedThisFrame;
            }
            else
            {
                touchpadPressed = gamepad.selectButton.wasPressedThisFrame;
            }
        }

        if (currentState == GameState.FreeRoam)
        {
            bool isGrounded = slimeController != null && slimeController.IsGrounded;
            bool isStandingStill = slimeController != null && slimeController.Velocity.sqrMagnitude < 0.1f;

            bool mapToggle = touchpadPressed || (keyboard != null && keyboard.mKey.wasPressedThisFrame);
            if (mapToggle)
            {
                if (isGrounded)
                {
                    InterruptMeditation();
                    ChangeState(GameState.MapView);
                    return;
                }
                else
                {
                    Debug.Log("[GameManager] Karte kann im Flug nicht geöffnet werden!");
                }
            }

            bool isHoldingBuild = (gamepad != null && gamepad.buttonNorth.isPressed) ||
                                  (keyboard != null && keyboard.bKey.isPressed);

            bool canMeditate = isGrounded && isStandingStill;

            if (isHoldingBuild && canMeditate)
            {
                if (slimeController != null)
                {
                    slimeController.IsMeditating = true;
                }

                buildModeHoldTimer += Time.deltaTime;
                UpdateHoldUI(BuildHoldProgress);

                if (buildModeHoldTimer >= buildModeHoldDuration)
                {
                    if (slimeController != null)
                    {
                        slimeController.IsMeditating = false;
                    }

                    buildModeHoldTimer = 0f;
                    UpdateHoldUI(0f);
                    ChangeState(GameState.MacroPlan);
                }
            }
            else
            {
                if (buildModeHoldTimer > 0f || (slimeController != null && slimeController.IsMeditating))
                {
                    InterruptMeditation();
                }
            }
        }
        else if (currentState == GameState.MapView || currentState == GameState.MacroPlan)
        {
            bool exitMacro = (gamepad != null && (gamepad.buttonEast.wasPressedThisFrame || gamepad.buttonNorth.wasPressedThisFrame)) ||
                             (keyboard != null && keyboard.escapeKey.wasPressedThisFrame);

            bool mapToggle = touchpadPressed || (keyboard != null && keyboard.mKey.wasPressedThisFrame);

            if (exitMacro || (currentState == GameState.MapView && mapToggle))
            {
                ChangeState(GameState.FreeRoam);
            }
        }
    }

    public void InterruptMeditation()
    {
        buildModeHoldTimer = 0f;
        UpdateHoldUI(0f);

        if (slimeController != null)
        {
            slimeController.IsMeditating = false;
        }
    }

    private void UpdateHoldUI(float progress)
    {
        if (holdProgressContainer != null)
        {
            holdProgressContainer.SetActive(progress > 0.01f);
        }

        if (holdProgressImage != null)
        {
            holdProgressImage.fillAmount = progress;
        }
    }

    public void ChangeState(GameState newState)
    {
        if (currentState == newState) return;

        GameState previous = currentState;
        currentState = newState;

        ExitState(previous);
        EnterState(newState, previous);
    }

    private void EnterState(GameState state, GameState fromState)
    {
        FindReferencesIfNeeded();
        InterruptMeditation();

        bool isGoingToMacro = (state == GameState.MacroPlan || state == GameState.MapView || state == GameState.EnemyTurn);
        bool comingFromMicro = (fromState == GameState.FreeRoam);

        if (state == GameState.FreeRoam)
        {
            SetUIActive(inventory: false);
            if (slimeController != null) slimeController.IsViewingMap = false;

            if (activeCameraTransition != null) StopCoroutine(activeCameraTransition);
            activeCameraTransition = StartCoroutine(TransitionToMicroRoutine());
        }
        else if (isGoingToMacro && comingFromMicro)
        {
            if (state == GameState.MapView)
            {
                if (slimeController != null)
                {
                    slimeController.enabled = true;
                    slimeController.IsViewingMap = true;
                }
                if (slimeRigidbody != null)
                {
                    slimeRigidbody.simulated = true;
                }
            }
            else
            {
                SetSlimeActive(false);
            }

            bool showInventory = (state == GameState.MacroPlan || state == GameState.Placement);

            if (activeCameraTransition != null) StopCoroutine(activeCameraTransition);
            activeCameraTransition = StartCoroutine(TransitionToMacroRoutine(showInventory));
        }
        else
        {
            SetCameraActive(macro: true, micro: false);

            if (state == GameState.MapView)
            {
                if (slimeController != null)
                {
                    slimeController.enabled = true;
                    slimeController.IsViewingMap = true;
                }
                if (slimeRigidbody != null)
                {
                    slimeRigidbody.simulated = true;
                }
            }
            else
            {
                SetSlimeActive(false);
            }

            SetUIActive(inventory: (state == GameState.MacroPlan || state == GameState.Placement));

            if (state == GameState.MacroPlan && inventoryManager != null)
            {
                inventoryManager.ResetSelection();
            }
        }
    }

    private void ExitState(GameState oldState)
    {
        if (oldState == GameState.MapView)
        {
            if (slimeController != null)
            {
                slimeController.IsViewingMap = false;
            }
        }
        else if (oldState == GameState.Placement)
        {
            PlacementManager placementMgr = FindAnyObjectByType<PlacementManager>();
            if (placementMgr != null)
            {
                placementMgr.ClearSelection();
            }
        }
    }

    private IEnumerator TransitionToMacroRoutine(bool showInventory)
    {
        Vector3 startLocalPos = macroOverviewPosition;
        if (MacroSlimeMarker.Instance != null)
        {
            startLocalPos = MacroSlimeMarker.Instance.transform.localPosition;
            startLocalPos.z = -10f;
        }

        float closeUpSize = 6f;

        if (macroCamera != null)
        {
            macroCamera.transform.localPosition = startLocalPos;
            macroCamera.orthographicSize = closeUpSize;
        }

        SetCameraActive(macro: true, micro: false);
        SetUIActive(inventory: false);

        float elapsed = 0f;
        while (elapsed < cameraTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / cameraTransitionDuration);

            if (macroCamera != null)
            {
                macroCamera.transform.localPosition = Vector3.Lerp(startLocalPos, macroOverviewPosition, t);
                macroCamera.orthographicSize = Mathf.Lerp(closeUpSize, macroOverviewSize, t);
            }
            yield return null;
        }

        if (macroCamera != null)
        {
            macroCamera.transform.localPosition = macroOverviewPosition;
            macroCamera.orthographicSize = macroOverviewSize;
        }

        SetUIActive(inventory: showInventory);
        if (showInventory && inventoryManager != null)
        {
            inventoryManager.ResetSelection();
        }

        activeCameraTransition = null;
    }

    private IEnumerator TransitionToMicroRoutine()
    {
        Vector3 targetLocalPos = macroOverviewPosition;
        if (MacroSlimeMarker.Instance != null)
        {
            targetLocalPos = MacroSlimeMarker.Instance.transform.localPosition;
            targetLocalPos.z = -10f;
        }

        float closeUpSize = 6f;
        Vector3 startLocalPos = (macroCamera != null) ? macroCamera.transform.localPosition : macroOverviewPosition;
        float startSize = (macroCamera != null) ? macroCamera.orthographicSize : macroOverviewSize;

        float elapsed = 0f;
        while (elapsed < cameraTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / cameraTransitionDuration);

            if (macroCamera != null)
            {
                macroCamera.transform.localPosition = Vector3.Lerp(startLocalPos, targetLocalPos, t);
                macroCamera.orthographicSize = Mathf.Lerp(startSize, closeUpSize, t);
            }
            yield return null;
        }

        SetCameraActive(macro: false, micro: true);
        SetSlimeActive(true);

        if (MacroGridManager.Instance != null)
        {
            MacroGridManager.Instance.RefreshActiveRoomDoors();
        }

        activeCameraTransition = null;
    }

    private void SetCameraActive(bool macro, bool micro)
    {
        if (macroCamera != null) macroCamera.gameObject.SetActive(macro);
        if (mainCamera != null) mainCamera.gameObject.SetActive(micro);
    }

    private void SetUIActive(bool inventory)
    {
        if (inventoryScreen != null)
        {
            inventoryScreen.SetActive(inventory);
            if (inventory) Canvas.ForceUpdateCanvases();
        }
    }

    private void SetSlimeActive(bool active)
    {
        if (slimeController != null)
        {
            slimeController.enabled = active;
        }

        if (slimeRigidbody != null)
        {
            if (!active)
            {
                cachedSlimeVelocity = slimeRigidbody.linearVelocity;
                slimeRigidbody.linearVelocity = Vector2.zero;
                slimeRigidbody.simulated = false;
            }
            else
            {
                slimeRigidbody.simulated = true;
                slimeRigidbody.linearVelocity = cachedSlimeVelocity;
            }
        }
    }

    private void FindReferencesIfNeeded()
    {
        if (eventManager == null)
        {
            eventManager = FindAnyObjectByType<EventManager>();
        }

        if (macroCamera == null)
        {
            macroCamera = GameObject.Find("MacroCamera")?.GetComponent<Camera>();
        }

        if (mainCamera == null)
        {
            var camObj = GameObject.Find("Main Camera");
            if (camObj != null) mainCamera = camObj.GetComponent<Camera>();
            else mainCamera = Camera.main;
        }

        if (slimeController == null)
        {
            slimeController = FindAnyObjectByType<PlatformerSlimeController>();
        }

        if (slimeRigidbody == null && slimeController != null)
        {
            slimeRigidbody = slimeController.GetComponent<Rigidbody2D>();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            NotifyFocusChanged();
            NotifyNightmareChanged();
        }
    }
#endif
}