using System;
using System.Collections;
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
    EnemyTurn    // Gegnerphase: Boss führt Aktionen aus
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Aktueller Spielstatus")]
    public GameState currentState = GameState.MacroPlan;

    [Header("Fokus-System & Kosten (Anpassbar)")]
    [Tooltip("Wie viel Fokus der Spieler zu Beginn jeder Runde erhält")]
    [SerializeField] private int maxFocusPerTurn = 4;
    [Tooltip("Aktuell verfügbarer Fokus in dieser Runde")]
    [SerializeField] private int currentFocus = 4;
    [Tooltip("Fokus-Kosten für das Platzieren eines 6er-Würfels auf dem Grid")]
    [SerializeField] private int placeCubeFocusCost = 1;
    [Tooltip("Fokus-Kosten für das erstmalige Betreten eines neuen Raumes")]
    [SerializeField] private int enterNewRoomFocusCost = 1;
    [Tooltip("Fokus-Kosten für das Durchqueren eines bereits besuchten/gesäuberten Raumes")]
    [SerializeField] private int enterClearedRoomFocusCost = 0;
    [Tooltip("Fokus-Verlust bei einem Treffer im Platformer")]
    [SerializeField] private int damageFocusCost = 1;
    [Tooltip("Wenn aktiv, wird der Zug sofort beendet, sobald der Fokus durch einen Treffer auf 0 fällt")]
    [SerializeField] private bool forceEndTurnOnZeroFocusHit = true;

    [Header("Albtraumleiste (0% - 100%, Anpassbar)")]
    [SerializeField] private float currentNightmare = 0f;
    [SerializeField] private float maxNightmare = 100f;
    [Tooltip("Anstieg der Albtraumleiste bei einem Treffer im Raum")]
    [SerializeField] private float nightmarePerDamage = 12f;
    [Tooltip("Passiver Anstieg der Albtraumleiste pro beendeter Runde")]
    [SerializeField] private float nightmarePerTurn = 3f;
    [Tooltip("Zusätzlicher Anstieg am Rundenende, wenn der Slime auf einem lila Feld steht")]
    [SerializeField] private float nightmareOnPurpleTile = 10f;
    [Tooltip("Zusätzlicher Anstieg am Rundenende, wenn der Slime auf einem schwarzen Randfeld steht")]
    [SerializeField] private float nightmareOnBlackVoidTile = 25f;
    [Tooltip("Anstieg, wenn der Raum unter dem Slime vom Boss zerstört wird")]
    [SerializeField] private float nightmareOnBossRoomDestroy = 25f;

    [Header("Fokus & Albtraum UI")]
    [Tooltip("Optional: Eigenes TextMeshPro-Feld im Canvas. Wenn leer, wird automatisch oben links ein HUD gezeichnet.")]
    [SerializeField] private TextMeshProUGUI focusText;
    [Tooltip("Optional: Eigenes Image (Filled) für die Albtraumleiste")]
    [SerializeField] private Image nightmareFillImage;
    [Tooltip("Zeigt oben links automatisch Fokus & Albtraumleiste, solange kein focusText zugewiesen ist")]
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
    [Tooltip("UI-Image mit Image Type 'Filled' für den Fortschrittskreis/Balken")]
    [SerializeField] private Image holdProgressImage;
    [Tooltip("Container-Objekt der Ladeanzeige (wird nur beim Halten eingeblendet)")]
    [SerializeField] private GameObject holdProgressContainer;

    [Header("Spieler-Referenz (Micro)")]
    [SerializeField] private PlatformerSlimeController slimeController;
    [SerializeField] private Rigidbody2D slimeRigidbody;

    [Header("Transition Settings")]
    [Tooltip("Dauer in Sekunden, die Dreieck gehalten werden muss, um in den Baumodus zu wechseln.")]
    [SerializeField] private float buildModeHoldDuration = 0.85f;
    [Tooltip("Dauer des Kamera-Zooms zwischen Slime und Übersicht in Sekunden.")]
    [SerializeField] private float cameraTransitionDuration = 0.35f;

    private float buildModeHoldTimer = 0f;
    private Vector2 cachedSlimeVelocity;
    private Coroutine activeCameraTransition;
    private bool isResettingRun = false;

    // Lokale Zielwerte der Makro-Kamera für die Übersicht
    private Vector3 macroOverviewPosition;
    private float macroOverviewSize;

    // Öffentliche Getter
    public int CurrentFocus => currentFocus;
    public int MaxFocusPerTurn => maxFocusPerTurn;
    public int PlaceCubeFocusCost => placeCubeFocusCost;
    public int EnterNewRoomFocusCost => enterNewRoomFocusCost;
    public int EnterClearedRoomFocusCost => enterClearedRoomFocusCost;
    public int DamageFocusCost => damageFocusCost;

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
        RefillFocus();

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
    // FOKUS- & ALBTRAUM-LOGIK
    // =====================================================================

    public bool CanAffordFocus(int cost)
    {
        if (cost <= 0) return true;
        return currentFocus >= cost;
    }

    public bool TrySpendFocus(int cost, string actionName = "")
    {
        if (cost <= 0) return true;

        if (currentFocus < cost)
        {
            Debug.Log($"[Fokus] Nicht genug Fokus für '{actionName}'! Benötigt: {cost}, Vorhanden: {currentFocus}");
            return false;
        }

        currentFocus = Mathf.Max(0, currentFocus - cost);
        Debug.Log($"[Fokus] -{cost} für '{actionName}' -> Verbleibend: {currentFocus}/{maxFocusPerTurn}");

        NotifyFocusChanged();
        return true;
    }

    public void ForceDrainFocus(int amount, string reason = "")
    {
        if (amount <= 0) return;

        currentFocus = Mathf.Max(0, currentFocus - amount);
        Debug.Log($"[Fokus-Verlust] -{amount} ({reason}) -> Verbleibend: {currentFocus}/{maxFocusPerTurn}");
        NotifyFocusChanged();
    }

    public void RefillFocus()
    {
        currentFocus = maxFocusPerTurn;
        NotifyFocusChanged();
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

    /// <summary>
    /// Wird von PlatformerSlimeController.TakeDamage() aufgerufen.
    /// Zieht Fokus ab, erhöht die Albtraumleiste und löst bei 0 Fokus den Zwangsstopp aus.
    /// </summary>
    public void OnSlimeDamaged()
    {
        if (isResettingRun || currentState == GameState.EnemyTurn) return;

        InterruptMeditation();

        // Falls die Karte offen war, sofort schließen
        if (currentState == GameState.MapView)
        {
            ChangeState(GameState.FreeRoam);
        }

        // 1. Fokus abziehen & Albtraum erhöhen
        ForceDrainFocus(damageFocusCost, "Treffer im Raum");
        AddNightmare(nightmarePerDamage, "Treffer im Raum");

        if (isResettingRun) return;

        // 2. ZWANGSSTOPP prüfen: Fällt Fokus auf 0, bleibt der Slime sofort stehen und der Zug endet!
        if (forceEndTurnOnZeroFocusHit && currentFocus <= 0)
        {
            Debug.LogWarning("[ZWANGSSTOPP] Fokus durch Treffer auf 0 gefallen! Slime friert ein & Zug endet sofort.");

            // Geschwindigkeit nullen, damit der Slime beim nächsten Rundenstart nicht weiterfliegt
            if (slimeController != null)
            {
                slimeController.ZeroOutMomentum();
            }
            cachedSlimeVelocity = Vector2.zero;

            // Slime an Ort und Stelle (auch an Wand/Decke/Luft) einfrieren
            SetSlimeActive(false);

            if (eventManager == null) eventManager = FindAnyObjectByType<EventManager>();
            if (eventManager != null)
            {
                eventManager.TriggerEndTurn(forcedByZeroFocus: true);
            }
        }
    }

    /// <summary>
    /// Wird ausgelöst, wenn die Albtraumleiste 100 % erreicht: Run scheitert, Slime wacht im Hub auf.
    /// </summary>
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

        // Werte zurücksetzen und Level/Grid neu im Hub starten
        currentNightmare = 0f;
        NotifyNightmareChanged();
        RefillFocus();

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

    private string BuildFocusDisplayString()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("FOKUS: ");

        int displayMax = Mathf.Max(maxFocusPerTurn, currentFocus);
        for (int i = 0; i < displayMax; i++)
        {
            sb.Append(i < currentFocus ? "◆ " : "◇ ");
        }

        sb.Append($"({currentFocus}/{maxFocusPerTurn})");
        return sb.ToString();
    }

    private void UpdateHUDTexts()
    {
        if (focusText != null)
        {
            focusText.text = $"{BuildFocusDisplayString()}   |   ALBTRAUM: {currentNightmare:0}%";
        }

        if (nightmareFillImage != null)
        {
            nightmareFillImage.fillAmount = NightmareRatio;
        }
    }

    private void OnGUI()
    {
        if (!showFallbackFocusHUD || focusText != null) return;

        // Haupt-Hintergrundbox oben links
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.fontSize = 16;
        boxStyle.fontStyle = FontStyle.Bold;
        boxStyle.alignment = TextAnchor.UpperLeft;
        boxStyle.normal.textColor = currentFocus > 0 ? new Color(0.3f, 0.95f, 1f) : new Color(1f, 0.35f, 0.35f);

        string roomStatus = "";
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
            roomStatus = $"\nRaum: [{coord.x}, {coord.y}] ({currentZone}) | Gesäubert: {visitedCount}";
        }

        string hint = "\n[Taste K: Test-Treffer]   [Taste E / R3: Zug beenden]";
        GUI.Box(new Rect(16f, 16f, 450f, 118f), BuildFocusDisplayString() + roomStatus + hint, boxStyle);

        // Albtraumleisten-Hintergrund
        Rect barBgRect = new Rect(28f, 98f, 426f, 24f);
        Color prevColor = GUI.color;
        GUI.color = new Color(0.12f, 0.05f, 0.18f, 0.95f);
        GUI.Box(barBgRect, GUIContent.none);

        // Albtraumleisten-Füllung
        float fillWidth = 422f * NightmareRatio;
        if (fillWidth > 1f)
        {
            GUI.color = Color.Lerp(new Color(0.55f, 0.15f, 0.85f, 1f), new Color(0.95f, 0.1f, 0.25f, 1f), NightmareRatio);
            GUI.Box(new Rect(30f, 100f, fillWidth, 20f), GUIContent.none);
        }

        // Albtraum-Beschriftung
        GUI.color = Color.white;
        GUIStyle barLabelStyle = new GUIStyle(GUI.skin.label);
        barLabelStyle.alignment = TextAnchor.MiddleCenter;
        barLabelStyle.fontStyle = FontStyle.Bold;
        barLabelStyle.fontSize = 13;
        GUI.Label(barBgRect, $"ALBTRAUM: {currentNightmare:0}% / {maxNightmare:0}%", barLabelStyle);

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

        // 1. ZUG BEENDEN (R3 auf Gamepad oder Taste E)
        bool endTurnTriggered = (gamepad != null && gamepad.rightStickButton.wasPressedThisFrame) ||
                                (keyboard != null && keyboard.eKey.wasPressedThisFrame);

        if (endTurnTriggered && (currentState == GameState.FreeRoam || currentState == GameState.MacroPlan))
        {
            if (eventManager == null)
            {
                eventManager = FindAnyObjectByType<EventManager>();
            }

            if (eventManager != null)
            {
                eventManager.TriggerEndTurn(forcedByZeroFocus: false);
            }
            return;
        }

        // Touchpad-Erkennung (DualSense) oder Fallback auf Select
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

        // 2. MIKRO-EBENE (FreeRoam) -> MAKRO
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
        // 3. MAKRO-EBENE -> ZURÜCK ZU MIKRO
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