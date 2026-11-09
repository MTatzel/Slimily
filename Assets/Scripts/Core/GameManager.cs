using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum GameState
{
    FreeRoam,
    MapView,
    MacroPlan,
    Placement,
    EnemyTurn
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Aktueller Spielstatus")]
    public GameState currentState = GameState.MacroPlan;

    [Header("Countdown-Kosten (Ticks)")]
    [SerializeField] private int maxFocusPerTurn = 4;
    [SerializeField] private int currentFocus = 4;
    [SerializeField] private int placeCubeFocusCost = 1;
    [SerializeField] private int enterNewRoomFocusCost = 1;
    [SerializeField] private int enterClearedRoomFocusCost = 0;
    [SerializeField] private int damageFocusCost = 1;
    [SerializeField] private int mulliganTickCost = 2;

    [Header("Albtraumleiste (0% - 100%)")]
    [SerializeField] private float currentNightmare = 0f;
    [SerializeField] private float maxNightmare = 100f;
    [SerializeField] private float nightmarePerDamage = 12f;
    [SerializeField] private float nightmarePerTurn = 3f;
    [SerializeField] private float nightmareOnPurpleTile = 10f;
    [SerializeField] private float nightmareOnBlackVoidTile = 25f;
    [SerializeField] private float nightmareOnBossRoomDestroy = 25f;

    [Header("HUD & Referenzen")]
    [SerializeField] private TextMeshProUGUI focusText;
    [SerializeField] private Image nightmareFillImage;
    [SerializeField] private bool showFallbackFocusHUD = true;
    [SerializeField] private LevelConfiguration currentLevelConfig;
    [SerializeField] private EventManager eventManager;
    private ThreatTrackerManager threatTracker;
    private BossBase activeBossInstance;

    [Header("Kameras & UI")]
    [SerializeField] private Camera macroCamera;
    [SerializeField] private Camera mainCamera;
    public GameObject inventoryScreen;
    public InventoryManager inventoryManager;
    [SerializeField] private Image holdProgressImage;
    [SerializeField] private GameObject holdProgressContainer;

    [Header("Spieler-Referenz (Micro)")]
    [SerializeField] private PlatformerSlimeController slimeController;
    [SerializeField] private Rigidbody2D slimeRigidbody;

    [Header("Transition Settings")]
    [SerializeField] private float buildModeHoldDuration = 0.85f;
    [SerializeField] private float cameraTransitionDuration = 0.35f;

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
    public bool ShowFallbackHUD => showFallbackFocusHUD && focusText == null;

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
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // Neue ausgelagerte Komponenten automatisch anhängen
        threatTracker = GetComponent<ThreatTrackerManager>();
        if (GetComponent<BiomeResonanceManager>() == null)
        {
           gameObject.AddComponent<BiomeResonanceManager>();
        }
        
        if (threatTracker == null) threatTracker = gameObject.AddComponent<ThreatTrackerManager>();

        if (GetComponent<ThreatOrbsAndPiPHUD>() == null)
        {
            gameObject.AddComponent<ThreatOrbsAndPiPHUD>();
        }
    }

    private void Start()
    {
        FindReferencesIfNeeded();

        currentNightmare = 0f;
        totalTicksElapsed = 0;

        if (currentLevelConfig != null) LoadLevel(currentLevelConfig);
        else Debug.LogError("[GameManager] Keine LevelConfiguration zugewiesen!");

        RefillFocus();
        UpdateHoldUI(0f);
        ChangeState(GameState.MacroPlan);
    }

    public bool CanAffordFocus(int cost) => !isResettingRun;

    public bool TrySpendFocus(int cost, string actionName = "")
    {
        if (isResettingRun) return false;
        AdvanceTicks(cost, actionName);
        return true;
    }

    public void ForceDrainFocus(int amount, string reason = "") => AdvanceTicks(amount, reason);

    /// <summary>
    /// Zählt alle Gefahren-Symbole oben rechts um 'ticks' herunter.
    /// </summary>
    public void AdvanceTicks(int ticks, string reason = "")
    {
        if (ticks <= 0 || isResettingRun) return;

        totalTicksElapsed += ticks;
        if (eventManager == null) eventManager = FindAnyObjectByType<EventManager>();
        if (threatTracker == null) threatTracker = ThreatTrackerManager.Instance;

        if (threatTracker != null)
        {
            threatTracker.AdvanceAllOrbs(ticks, eventManager);
            currentFocus = threatTracker.GetBossOrbCountdown();
        }

        NotifyFocusChanged();
    }

    /// <summary>
    /// Erhöht den Countdown aller Gefahren-Symbole oben rechts (z. B. durch 'Zeit-Anker').
    /// </summary>
    public void DelayNextBossEvent(int extraTicks, string reason = "")
    {
        if (extraTicks <= 0 || isResettingRun) return;

        if (threatTracker == null) threatTracker = ThreatTrackerManager.Instance;
        if (threatTracker != null)
        {
            threatTracker.DelayAllOrbs(extraTicks);
            currentFocus = threatTracker.GetBossOrbCountdown();
        }

        NotifyFocusChanged();
    }

    public void TriggerMicroCameraRumble(float intensity = 0.25f, float duration = 0.3f)
    {
        if (currentState != GameState.FreeRoam || slimeController == null) return;

        if (activeRumbleCoroutine != null) StopCoroutine(activeRumbleCoroutine);
        activeRumbleCoroutine = StartCoroutine(MicroRumbleRoutine(intensity, duration));
    }

    private IEnumerator MicroRumbleRoutine(float intensity, float duration)
    {
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
        if (inventoryManager != null) inventoryManager.MulliganHand();

        AdvanceTicks(mulliganTickCost, "Neue Erinnerungen gerufen");
    }

    public void RefillFocus()
    {
        if (threatTracker == null) threatTracker = GetComponent<ThreatTrackerManager>();
        if (threatTracker != null)
        {
            threatTracker.InitializeOrbs(maxFocusPerTurn);
            currentFocus = threatTracker.GetBossOrbCountdown();
        }
        NotifyFocusChanged();
    }

    public void AddNightmare(float amount, string reason = "")
    {
        if (Mathf.Approximately(amount, 0f) || isResettingRun) return;

        currentNightmare = Mathf.Clamp(currentNightmare + amount, 0f, maxNightmare);
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
        if (currentState == GameState.MapView) ChangeState(GameState.FreeRoam);

        AddNightmare(nightmarePerDamage, "Treffer im Raum");
        if (!isResettingRun) AdvanceTicks(damageFocusCost, "Straf-Tick durch Treffer");
    }

    private IEnumerator WakeUpInHubRoutine()
    {
        isResettingRun = true;
        SetSlimeActive(false);

        if (TurnBannerUI.Instance != null)
            yield return StartCoroutine(TurnBannerUI.Instance.ShowBannerRoutine("ALBTRAUM: 100% - ERWACHEN IM HUB", isEnemyTurn: true, duration: 1.6f));
        else
            yield return new WaitForSeconds(1.2f);

        currentNightmare = 0f;
        totalTicksElapsed = 0;
        NotifyNightmareChanged();

        if (inventoryManager == null) inventoryManager = FindAnyObjectByType<InventoryManager>();
        if (inventoryManager != null) inventoryManager.ResetDeckAndDrawStartingHand();

        if (currentLevelConfig != null) LoadLevel(currentLevelConfig);
        RefillFocus();

        ChangeState(GameState.MacroPlan);
        isResettingRun = false;
    }

    private void NotifyFocusChanged()
    {
        UpdateHUDTexts();
        OnFocusChanged?.Invoke(currentFocus, maxFocusPerTurn);
        if (MacroGridManager.Instance != null) MacroGridManager.Instance.RefreshActiveRoomDoors();
    }

    private void NotifyNightmareChanged()
    {
        UpdateHUDTexts();
        OnNightmareChanged?.Invoke(currentNightmare, maxNightmare);
    }

    private void UpdateHUDTexts()
    {
        if (focusText != null)
            focusText.text = $"BOSS IN: {currentFocus}   |   ALBTRAUM: {currentNightmare:0}%";

        if (nightmareFillImage != null)
            nightmareFillImage.fillAmount = NightmareRatio;
    }

    public void LoadLevel(LevelConfiguration config)
    {
        currentLevelConfig = config;
        if (GridManager.Instance != null) GridManager.Instance.Initialize(config);

        if (MacroGridManager.Instance != null)
        {
            MacroGridManager.Instance.Initialize(config);
            if (slimeController != null)
                MacroGridManager.Instance.PlaceSlimeInRoom(config.startCoordinate, slimeController.transform);
        }

        SetupLevelEncounter(config);
        CalculateMacroOverviewParameters(config);
        UpdateHUDTexts();
    }

    private void SetupLevelEncounter(LevelConfiguration config)
    {
        if (activeBossInstance != null) Destroy(activeBossInstance.gameObject);
        if (eventManager == null) eventManager = FindAnyObjectByType<EventManager>();

        if (config != null && config.bossPrefab != null)
        {
            Transform spawnParent = transform.parent != null ? transform.parent : transform;
            activeBossInstance = Instantiate(config.bossPrefab, spawnParent);
            activeBossInstance.transform.localPosition = new Vector3(
                config.bossSpawnCoordinate.x * config.cellOffset,
                config.bossSpawnCoordinate.y * config.cellOffset,
                0f
            );
            if (eventManager != null) eventManager.boss = activeBossInstance;
        }
        else if (eventManager != null)
        {
            eventManager.boss = null;
        }
    }

    private void CalculateMacroOverviewParameters(LevelConfiguration config)
    {
        if (macroCamera == null) macroCamera = GameObject.Find("MacroCamera")?.GetComponent<Camera>();

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
                touchpadPressed = dualShock.touchpadButton.wasPressedThisFrame;
            else
                touchpadPressed = gamepad.selectButton.wasPressedThisFrame;
        }

        if (currentState == GameState.FreeRoam)
        {
            bool isGrounded = slimeController != null && slimeController.IsGrounded;
            bool isStandingStill = slimeController != null && slimeController.Velocity.sqrMagnitude < 0.1f;

            if (touchpadPressed || (keyboard != null && keyboard.mKey.wasPressedThisFrame))
            {
                if (isGrounded)
                {
                    InterruptMeditation();
                    ChangeState(GameState.MapView);
                    return;
                }
            }

            bool isHoldingBuild = (gamepad != null && gamepad.buttonNorth.isPressed) ||
                                  (keyboard != null && keyboard.bKey.isPressed);

            if (isHoldingBuild && isGrounded && isStandingStill)
            {
                if (slimeController != null) slimeController.IsMeditating = true;
                buildModeHoldTimer += Time.deltaTime;
                UpdateHoldUI(BuildHoldProgress);

                if (buildModeHoldTimer >= buildModeHoldDuration)
                {
                    if (slimeController != null) slimeController.IsMeditating = false;
                    buildModeHoldTimer = 0f;
                    UpdateHoldUI(0f);
                    ChangeState(GameState.MacroPlan);
                }
            }
            else if (buildModeHoldTimer > 0f || (slimeController != null && slimeController.IsMeditating))
            {
                InterruptMeditation();
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
        if (slimeController != null) slimeController.IsMeditating = false;
    }

    private void UpdateHoldUI(float progress)
    {
        if (holdProgressContainer != null) holdProgressContainer.SetActive(progress > 0.01f);
        if (holdProgressImage != null) holdProgressImage.fillAmount = progress;
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
            SetUIActive(false);
            if (slimeController != null) slimeController.IsViewingMap = false;
            if (activeCameraTransition != null) StopCoroutine(activeCameraTransition);
            activeCameraTransition = StartCoroutine(TransitionToMicroRoutine());
        }
        else if (isGoingToMacro && comingFromMicro)
        {
            if (state == GameState.MapView)
            {
                if (slimeController != null) { slimeController.enabled = true; slimeController.IsViewingMap = true; }
                if (slimeRigidbody != null) slimeRigidbody.simulated = true;
            }
            else SetSlimeActive(false);

            bool showInventory = (state == GameState.MacroPlan || state == GameState.Placement);
            if (activeCameraTransition != null) StopCoroutine(activeCameraTransition);
            activeCameraTransition = StartCoroutine(TransitionToMacroRoutine(showInventory));
        }
        else
        {
            SetCameraActive(true, false);
            if (state == GameState.MapView)
            {
                if (slimeController != null) { slimeController.enabled = true; slimeController.IsViewingMap = true; }
                if (slimeRigidbody != null) slimeRigidbody.simulated = true;
            }
            else SetSlimeActive(false);

            SetUIActive(state == GameState.MacroPlan || state == GameState.Placement);
            if (state == GameState.MacroPlan && inventoryManager != null) inventoryManager.ResetSelection();
        }
    }

    private void ExitState(GameState oldState)
    {
        if (oldState == GameState.MapView && slimeController != null)
            slimeController.IsViewingMap = false;
        else if (oldState == GameState.Placement)
            FindAnyObjectByType<PlacementManager>()?.ClearSelection();
    }

    private IEnumerator TransitionToMacroRoutine(bool showInventory)
    {
        Vector3 startLocalPos = MacroSlimeMarker.Instance != null
            ? new Vector3(MacroSlimeMarker.Instance.transform.localPosition.x, MacroSlimeMarker.Instance.transform.localPosition.y, -10f)
            : macroOverviewPosition;

        float closeUpSize = 6f;
        if (macroCamera != null)
        {
            macroCamera.transform.localPosition = startLocalPos;
            macroCamera.orthographicSize = closeUpSize;
        }

        SetCameraActive(true, false);
        SetUIActive(false);

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

        SetUIActive(showInventory);
        if (showInventory && inventoryManager != null) inventoryManager.ResetSelection();
        activeCameraTransition = null;
    }

    private IEnumerator TransitionToMicroRoutine()
    {
        Vector3 targetLocalPos = MacroSlimeMarker.Instance != null
            ? new Vector3(MacroSlimeMarker.Instance.transform.localPosition.x, MacroSlimeMarker.Instance.transform.localPosition.y, -10f)
            : macroOverviewPosition;

        float closeUpSize = 6f;
        Vector3 startLocalPos = macroCamera != null ? macroCamera.transform.localPosition : macroOverviewPosition;
        float startSize = macroCamera != null ? macroCamera.orthographicSize : macroOverviewSize;

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

        SetCameraActive(false, true);
        SetSlimeActive(true);
        if (MacroGridManager.Instance != null) MacroGridManager.Instance.RefreshActiveRoomDoors();
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
        if (slimeController != null) slimeController.enabled = active;
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
        if (eventManager == null) eventManager = FindAnyObjectByType<EventManager>();
        if (macroCamera == null) macroCamera = GameObject.Find("MacroCamera")?.GetComponent<Camera>();
        if (mainCamera == null)
        {
            var camObj = GameObject.Find("Main Camera");
            mainCamera = camObj != null ? camObj.GetComponent<Camera>() : Camera.main;
        }
        if (slimeController == null) slimeController = FindAnyObjectByType<PlatformerSlimeController>();
        if (slimeRigidbody == null && slimeController != null) slimeRigidbody = slimeController.GetComponent<Rigidbody2D>();
    }
}