using UnityEngine;
using UnityEngine.InputSystem;

public enum GameState
{
    FreeRoam,    // Platformer-Modus (Mikro-Ebene)
    MacroPlan,   // Bau- & Planungsphase auf dem Grid (Makro-Ebene)
    Inventory,
    Placement,
    EnemyTurn
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
    [SerializeField] private Camera mainCamera; // Platformer-Kamera

    [Header("UI")]
    public GameObject inventoryScreen;
    public InventoryManager inventoryManager;

    [Header("Spieler-Referenz (Micro)")]
    [SerializeField] private PlatformerSlimeController slimeController;
    [SerializeField] private Rigidbody2D slimeRigidbody;

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

        // Startet direkt im Makro-Planungsmodus (Grid aktiv, Slime pausiert)
        SetViewMode(isMacro: true);
    }

    public void LoadLevel(LevelConfiguration config)
    {
        currentLevelConfig = config;

        // 1. Makro-Grid aufbauen
        if (GridManager.Instance != null)
        {
            GridManager.Instance.Initialize(config);
        }

        // 2. Mikro-Räume & Slime initialisieren
        if (MacroGridManager.Instance != null)
        {
            MacroGridManager.Instance.Initialize(config);
        }

        // 3. Makro-Kamera auf das Grid zentrieren
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

    void Update()
    {
        if (Keyboard.current == null) return;

        // Mit TAB zwischen Makro- (Bauen) und Mikro-Ebene (FreeRoam) wechseln
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            // Nicht umschalten, solange man gerade einen Block platziert
            if (currentState == GameState.Placement) return;

            bool toMacro = (currentState == GameState.FreeRoam);
            SetViewMode(toMacro);
        }
    }

    /// <summary>
    /// Zentrale Umschaltung zwischen Makro-Bauen und Mikro-Platformer
    /// </summary>
 /// <summary>
    /// Zentrale Umschaltung zwischen Makro-Bauen und Mikro-Platformer
    /// </summary>
    public void SetViewMode(bool isMacro)
    {
        FindReferencesIfNeeded();

        // Das Inventar/Hotbar-Panel soll nun immer sichtbar bleiben
        if (inventoryScreen != null && !inventoryScreen.activeSelf)
        {
            inventoryScreen.SetActive(true);
        }

        if (isMacro)
        {
            currentState = GameState.Inventory;

            // Kameras umstellen
            if (macroCamera != null) macroCamera.gameObject.SetActive(true);
            if (mainCamera != null) mainCamera.gameObject.SetActive(false);

            Canvas.ForceUpdateCanvases();
            if (inventoryManager != null) inventoryManager.ResetSelection();

            // Slime-Steuerung & Physik deaktivieren
            SetSlimeInputActive(false);
        }
        else
        {
            currentState = GameState.FreeRoam;

            // Kameras umstellen
            if (macroCamera != null) macroCamera.gameObject.SetActive(false);
            if (mainCamera != null) mainCamera.gameObject.SetActive(true);

            // Slime-Steuerung & Physik aktivieren
            SetSlimeInputActive(true);
        }
    }

    private void SetSlimeInputActive(bool active)
    {
        if (slimeController != null)
        {
            slimeController.enabled = active;
        }

        if (slimeRigidbody != null)
        {
            if (!active)
            {
                slimeRigidbody.linearVelocity = Vector2.zero;
                slimeRigidbody.simulated = false;
            }
            else
            {
                slimeRigidbody.simulated = true;
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

    public void ChangeState(GameState newState)
    {
        currentState = newState;
    }
}