using System.Collections;
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

    [Header("Level Konfiguration")]
    [SerializeField] private LevelConfiguration currentLevelConfig;

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

    // Lokale Zielwerte der Makro-Kamera für die Übersicht
    private Vector3 macroOverviewPosition;
    private float macroOverviewSize;

    public float BuildHoldProgress => Mathf.Clamp01(buildModeHoldTimer / buildModeHoldDuration);

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

        CalculateMacroOverviewParameters(config);
    }

    private void CalculateMacroOverviewParameters(LevelConfiguration config)
    {
        if (macroCamera == null)
        {
            macroCamera = GameObject.Find("MacroCamera")?.GetComponent<Camera>();
        }

        float centerX = (config.gridWidth * config.cellOffset) / 2f - (config.cellOffset / 2f);
        float centerY = (config.gridHeight * config.cellOffset) / 2f - (config.cellOffset / 2f);

        // Lokale Koordinaten relativ zum Macro_Grid-Parent
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
            EventManager eventMgr = FindAnyObjectByType<EventManager>();
            if (eventMgr != null)
            {
                eventMgr.StartCoroutine("TurnSequenceRoutine");
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

            // Karte toggeln (Touchpad / M) – NUR WENN AUF EINER FLÄCHE
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

            // In den Baumodus meditieren: Dreieck / Taste B halten
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
                // Bei Loslassen oder Bewegung/Sprung sofort abbrechen
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
        currentState = newState; // Erst den State setzen...

        ExitState(previous);     // ...dann Exit des alten States ausführen
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
                // Karte: Slime bleibt physikalisch in der Welt, aber Steuerung gesperrt
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
                // Baumodus (MacroPlan): Slime komplett einfrieren
                SetSlimeActive(false);
            }

            bool showInventory = (state == GameState.MacroPlan || state == GameState.Placement);

            if (activeCameraTransition != null) StopCoroutine(activeCameraTransition);
            activeCameraTransition = StartCoroutine(TransitionToMacroRoutine(showInventory));
        }
        else
        {
            // Direkter Wechsel zwischen zwei Makro-Zuständen (z. B. MapView <-> MacroPlan)
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
                placementMgr.ClearSelection(); // Ruft nur noch ClearSelection auf!
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
}