using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Spieler-Referenz (Micro)")]
    [SerializeField] private PlatformerSlimeController slimeController;
    [SerializeField] private Rigidbody2D slimeRigidbody;

    [Header("Transition Settings")]
    [Tooltip("Dauer in Sekunden, die Dreieck gehalten werden muss, um in den Baumodus zu wechseln.")]
    [SerializeField] private float buildModeHoldDuration = 0.45f;
    private float buildModeHoldTimer = 0f;

    // Speichert die letzte Geschwindigkeit des Slimes vor dem Einfrieren
    private Vector2 cachedSlimeVelocity;

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

        // Startet direkt im Makro-Planungsmodus
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

        PositionMacroCamera(config);
    }

    private void PositionMacroCamera(LevelConfiguration config)
    {
        if (macroCamera == null)
        {
            macroCamera = GameObject.Find("MacroCamera")?.GetComponent<Camera>();
        }

        if (macroCamera != null)
        {
            float centerX = (config.gridWidth * config.cellOffset) / 2f - (config.cellOffset / 2f);
            float centerY = (config.gridHeight * config.cellOffset) / 2f - (config.cellOffset / 2f);

            macroCamera.transform.localPosition = new Vector3(centerX, centerY, -10f);
            macroCamera.orthographicSize = (config.gridHeight * config.cellOffset) / 2f + 2f;
        }
    }

    private void Update()
    {
        HandleTransitionInputs();
    }

   private void HandleTransitionInputs()
    {
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

        // Touchpad-Erkennung: Falls DualSense/DualShock angeschlossen ist, touchpadButton abfragen, sonst selectButton (Share/Back)
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

        // 2. MIKRO-EBENE (FreeRoam) -> MAKRO-EINGABEN
        if (currentState == GameState.FreeRoam)
        {
            // Karte toggeln (Touchpad / Select oder Taste M)
            bool mapToggle = touchpadPressed || (keyboard != null && keyboard.mKey.wasPressedThisFrame);

            if (mapToggle)
            {
                ChangeState(GameState.MapView);
                return;
            }

            // In den Baumodus meditieren: Dreieck / Taste B halten
            bool isHoldingBuild = (gamepad != null && gamepad.buttonNorth.isPressed) ||
                                  (keyboard != null && keyboard.bKey.isPressed);

            if (isHoldingBuild)
            {
                buildModeHoldTimer += Time.deltaTime;
                if (buildModeHoldTimer >= buildModeHoldDuration)
                {
                    buildModeHoldTimer = 0f;
                    ChangeState(GameState.MacroPlan);
                }
            }
            else
            {
                buildModeHoldTimer = 0f;
            }
        }
        // 3. MAKRO-EBENE -> ZURÜCK ZU MIKRO
        else if (currentState == GameState.MapView || currentState == GameState.MacroPlan)
        {
            // Abbrechen / Zurück: Kreis / Dreieck-Tap / Taste Escape
            bool exitMacro = (gamepad != null && (gamepad.buttonEast.wasPressedThisFrame || gamepad.buttonNorth.wasPressedThisFrame)) ||
                             (keyboard != null && keyboard.escapeKey.wasPressedThisFrame);

            bool mapToggle = touchpadPressed || (keyboard != null && keyboard.mKey.wasPressedThisFrame);

            if (exitMacro || (currentState == GameState.MapView && mapToggle))
            {
                ChangeState(GameState.FreeRoam);
            }
        }
    }

    public void ChangeState(GameState newState)
    {
        if (currentState == newState) return;

        ExitState(currentState);
        currentState = newState;
        EnterState(newState);
    }

    private void EnterState(GameState state)
    {
        FindReferencesIfNeeded();

        switch (state)
        {
            case GameState.FreeRoam:
                SetCameraActive(macro: false, micro: true);
                SetUIActive(inventory: false);
                SetSlimeActive(true);
                break;

            case GameState.MapView:
                SetCameraActive(macro: true, micro: false);
                SetUIActive(inventory: false); // Keine Hotbar im reinen Kartenmodus
                SetSlimeActive(false);
                break;

            case GameState.MacroPlan:
                SetCameraActive(macro: true, micro: false);
                SetUIActive(inventory: true);  // Hotbar aktivieren
                SetSlimeActive(false);
                if (inventoryManager != null) inventoryManager.ResetSelection();
                break;

            case GameState.Placement:
                SetCameraActive(macro: true, micro: false);
                SetUIActive(inventory: true);
                SetSlimeActive(false);
                break;

            case GameState.EnemyTurn:
                SetCameraActive(macro: true, micro: false); // Zeigt Boss-Aktionen auf dem Grid
                SetUIActive(inventory: false);
                SetSlimeActive(false);
                break;
        }
    }

    private void ExitState(GameState oldState)
    {
        if (oldState == GameState.Placement)
        {
            PlacementManager placementMgr = FindAnyObjectByType<PlacementManager>();
            if (placementMgr != null)
            {
                placementMgr.CancelSelection();
            }
        }
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