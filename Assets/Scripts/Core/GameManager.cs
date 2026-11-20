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
    [SerializeField] private MacroCameraController cameraController;
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

    private float buildModeHoldTimer = 0f;
    private Vector2 cachedSlimeVelocity;
    private bool isResettingRun = false;
    private int totalTicksElapsed = 0;

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
    public MacroCameraController CameraController => cameraController;

    public event Action<int, int> OnFocusChanged;
    public event Action<float, float> OnNightmareChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (GetComponent<BiomeResonanceManager>() == null)
        {
            gameObject.AddComponent<BiomeResonanceManager>();
        }

        if (GetComponent<CubeLootManager>() == null)
        {
            gameObject.AddComponent<CubeLootManager>();
        }

        if (GetComponent<AltarManager>() == null)
        {
            gameObject.AddComponent<AltarManager>();
        }

        threatTracker = GetComponent<ThreatTrackerManager>();
        if (threatTracker == null) threatTracker = gameObject.AddComponent<ThreatTrackerManager>();

        if (GetComponent<ThreatOrbsAndPiPHUD>() == null)
        {
            gameObject.AddComponent<ThreatOrbsAndPiPHUD>();
        }

        if (cameraController == null)
        {
            cameraController = GetComponent<MacroCameraController>();
            if (cameraController == null) cameraController = gameObject.AddComponent<MacroCameraController>();
        }
        cameraController.SetupCamerasIfEmpty(macroCamera, mainCamera);
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

        // Immer auf der Macro-Ebene (MacroPlan) mit Fokus auf dem Spieler starten
        currentState = GameState.MacroPlan;
        if (cameraController != null)
        {
            cameraController.SnapToMacroPlayerFocus(keepCurrentActiveState: false);
        }
        EnterState(GameState.MacroPlan, GameState.MacroPlan);
    }

    public bool CanAffordFocus(int cost) => !isResettingRun;

    public bool TrySpendFocus(int cost, string actionName = "")
    {
        if (isResettingRun) return false;
        AdvanceTicks(cost, actionName);
        return true;
    }

    public void ForceDrainFocus(int amount, string reason = "") => AdvanceTicks(amount, reason);

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

        if (AltarManager.Instance != null)
        {
            AltarManager.Instance.OnTicksAdvanced(ticks);
        }

        NotifyFocusChanged();
    }

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
        if (currentState != GameState.FreeRoam || slimeController == null || cameraController == null) return;
        cameraController.TriggerMicroCameraRumble(slimeController, intensity, duration);
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

        if (BiomeResonanceManager.Instance != null)
        {
            BiomeResonanceManager.Instance.CheckNightmareThreshold(currentNightmare);
        }

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

        if (BiomeResonanceManager.Instance != null)
        {
            BiomeResonanceManager.Instance.ResetToDefaultResonances();
        }

        if (inventoryManager == null) inventoryManager = FindAnyObjectByType<InventoryManager>();
        if (inventoryManager != null) inventoryManager.ResetInventoryToStartingCubes();

        if (currentLevelConfig != null) LoadLevel(currentLevelConfig);
        RefillFocus();

        currentState = GameState.MacroPlan;
        if (cameraController != null)
        {
            cameraController.SnapToMacroPlayerFocus(keepCurrentActiveState: false);
        }
        EnterState(GameState.MacroPlan, GameState.MacroPlan);

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

        if (cameraController != null)
        {
            cameraController.InitializeForLevel(config);
        }

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

    private void Update()
    {
        if (isResettingRun) return;
        HandleTransitionInputs();
    }

    private void HandleTransitionInputs()
    {
        if (cameraController != null && cameraController.IsTransitioning) return;

        var gamepad = Gamepad.current;
        var keyboard = Keyboard.current;

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

            if (cameraController != null)
            {
                cameraController.TransitionToMicro(() =>
                {
                    SetSlimeActive(true);
                    if (MacroGridManager.Instance != null) MacroGridManager.Instance.RefreshActiveRoomDoors();
                });
            }
        }
        else if (isGoingToMacro && comingFromMicro)
        {
            if (state == GameState.MapView)
            {
                if (slimeController != null) { slimeController.enabled = true; slimeController.IsViewingMap = true; }
                if (slimeRigidbody != null) slimeRigidbody.simulated = true;
            }
            else
            {
                SetSlimeActive(false);
            }

            bool showInventory = (state == GameState.MacroPlan || state == GameState.Placement);
            SetUIActive(false);

            if (cameraController != null)
            {
                cameraController.TransitionToMacro(() =>
                {
                    SetUIActive(showInventory);
                    if (showInventory && inventoryManager != null) inventoryManager.ResetSelection();
                });
            }
        }
        else
        {
            // Wechsel innerhalb der Macro-Ebene (z.B. Placement <-> MacroPlan):
            // Nur sicherstellen, dass die Macro-Kamera aktiv ist, ohne Fokus-Rücksprung zum Spieler
            if (cameraController != null)
            {
                cameraController.SetCameraActive(macro: true, micro: false);
            }

            if (state == GameState.MapView)
            {
                if (slimeController != null) { slimeController.enabled = true; slimeController.IsViewingMap = true; }
                if (slimeRigidbody != null) slimeRigidbody.simulated = true;
            }
            else
            {
                SetSlimeActive(false);
            }

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
        if (cameraController == null)
        {
            cameraController = GetComponent<MacroCameraController>();
            if (cameraController == null) cameraController = gameObject.AddComponent<MacroCameraController>();
        }
        cameraController.SetupCamerasIfEmpty(macroCamera, mainCamera);

        if (slimeController == null) slimeController = FindAnyObjectByType<PlatformerSlimeController>();
        if (slimeRigidbody == null && slimeController != null) slimeRigidbody = slimeController.GetComponent<Rigidbody2D>();
    }
}