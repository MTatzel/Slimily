using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class AltarTeleportController : MonoBehaviour
{
    public static AltarTeleportController Instance { get; private set; }

    [Header("Teleport-Regeln")]
    [Tooltip("Maximaler Radius auf dem Macro-Grid (in Feldern), in dem andere Altäre erreicht werden können.")]
    [Range(2, 25)] public int teleportRadius = 8;

    [Tooltip("Tick-Kosten pro Teleport-Sprung.")]
    [Range(0, 5)] public int teleportTickCost = 1;

    [Tooltip("Wenn AN, kann man von jedem aktiven Altar auch direkt zurück zum Start-Hub springen (sofern im Radius).")]
    public bool allowTeleportToHub = true;

    [Header("Optionaler Boss-Bannkreis")]
    [Tooltip("Wenn AN, blockiert ein aktiver Boss in der Nähe das Wegteleportieren zum Start-Hub.")]
    public bool blockHubTeleportNearBoss = true;

    [Tooltip("Abstand zum Boss (in Grid-Feldern), ab dem der Hub-Teleport blockiert wird.")]
    [Range(1, 12)] public int bossBlockRadius = 5;

    [Header("Eingabe")]
    [SerializeField] private float inputRepeatDelay = 0.22f;

    private AltarTeleportVisualizer visualizer;
    private bool isSelectingTarget = false;
    private Vector2Int sourceAltarCoord;
    private readonly List<Vector2Int> reachableTargets = new List<Vector2Int>();
    private int selectedIndex = 0;
    private float inputCooldown = 0f;
    private bool isHubBlockedByBoss = false;

    public bool IsSelectingTarget => isSelectingTarget;
    public Vector2Int SourceAltarCoord => sourceAltarCoord;
    public IReadOnlyList<Vector2Int> ReachableTargets => reachableTargets;
    public int SelectedIndex => selectedIndex;
    public bool IsHubBlockedByBoss => isHubBlockedByBoss;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }

        EnsureVisualizer();
    }

    private void EnsureVisualizer()
    {
        if (visualizer == null)
        {
            visualizer = GetComponent<AltarTeleportVisualizer>();
            if (visualizer == null)
            {
                visualizer = gameObject.AddComponent<AltarTeleportVisualizer>();
            }
        }
    }

    /// <summary>
    /// Öffnet die Macro-Kartenansicht zur Auswahl eines Ziel-Altars.
    /// </summary>
    public bool TryBeginTeleportSelection(Vector2Int fromCoord, out string feedbackMessage)
    {
        feedbackMessage = "";
        EnsureVisualizer();

        sourceAltarCoord = fromCoord;
        isHubBlockedByBoss = CheckBossBlocksHub(fromCoord);
        CollectReachableTargets(fromCoord);

        if (reachableTargets.Count == 0)
        {
            feedbackMessage = isHubBlockedByBoss
                ? "Kein Ziel im Radius (Hub durch Boss blockiert)"
                : $"Kein anderer aktiver Altar im Radius ({teleportRadius})";
            return false;
        }

        selectedIndex = 0;
        isSelectingTarget = true;
        inputCooldown = 0.2f; // Kurzer Schutz, damit derselbe Tastendruck nicht sofort bestätigt

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(GameState.AltarTeleport);
        }

        FocusCameraOnSelectedTarget();
        visualizer.ShowSelection(sourceAltarCoord, reachableTargets, selectedIndex, teleportTickCost, isHubBlockedByBoss);
        return true;
    }

    public void CancelSelection()
    {
        if (!isSelectingTarget) return;

        isSelectingTarget = false;
        if (visualizer != null) visualizer.HideSelection();

        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.CameraController != null)
            {
                GameManager.Instance.CameraController.ClearPlacementFocus(resetManualPan: true);
            }

            if (GameManager.Instance.currentState == GameState.AltarTeleport)
            {
                GameManager.Instance.ChangeState(GameState.FreeRoam);
            }
        }
    }

    private void Update()
    {
        if (!isSelectingTarget) return;

        if (GameManager.Instance != null && GameManager.Instance.currentState != GameState.AltarTeleport)
        {
            CancelSelection();
            return;
        }

        if (inputCooldown > 0f)
        {
            inputCooldown -= Time.unscaledDeltaTime;
        }

        var gamepad = Gamepad.current;
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        // 1. Abbrechen (B / Escape / Rechtsklick)
        bool cancelPressed = (gamepad != null && gamepad.buttonEast.wasPressedThisFrame) ||
                             (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                             (mouse != null && mouse.rightButton.wasPressedThisFrame);

        if (cancelPressed)
        {
            CancelSelection();
            return;
        }

        // 2. Ziel durchschalten (Stick / D-Pad / WASD / Pfeiltasten)
        if (inputCooldown <= 0f && reachableTargets.Count > 1)
        {
            int step = 0;

            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                Vector2 dpad = gamepad.dpad.ReadValue();
                if (dpad.x > 0.5f || stick.x > 0.55f || dpad.y > 0.5f || stick.y > 0.55f || gamepad.rightShoulder.wasPressedThisFrame)
                    step = 1;
                else if (dpad.x < -0.5f || stick.x < -0.55f || dpad.y < -0.5f || stick.y < -0.55f || gamepad.leftShoulder.wasPressedThisFrame)
                    step = -1;
            }

            if (keyboard != null && step == 0)
            {
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed || keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                    step = 1;
                else if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed || keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                    step = -1;
            }

            if (step != 0)
            {
                selectedIndex = (selectedIndex + step + reachableTargets.Count) % reachableTargets.Count;
                inputCooldown = inputRepeatDelay;
                FocusCameraOnSelectedTarget();
                visualizer.UpdateSelectedIndex(selectedIndex);
            }
        }

        // 3. Ziel per Maus anklicken
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && inputCooldown <= 0f)
        {
            int clickedIdx = FindTargetIndexUnderMouse();
            if (clickedIdx >= 0)
            {
                if (clickedIdx == selectedIndex)
                {
                    ConfirmTeleport();
                    return;
                }
                else
                {
                    selectedIndex = clickedIdx;
                    FocusCameraOnSelectedTarget();
                    visualizer.UpdateSelectedIndex(selectedIndex);
                    return;
                }
            }
        }

        // 4. Bestätigen (A / Enter / Space / E)
        bool confirmPressed = (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame) ||
                              (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame));

        if (confirmPressed && inputCooldown <= 0f)
        {
            ConfirmTeleport();
        }
    }

    private void ConfirmTeleport()
    {
        if (reachableTargets.Count == 0) return;

        Vector2Int targetCoord = reachableTargets[Mathf.Clamp(selectedIndex, 0, reachableTargets.Count - 1)];

        isSelectingTarget = false;
        if (visualizer != null) visualizer.HideSelection();

        if (GameManager.Instance != null && GameManager.Instance.CameraController != null)
        {
            GameManager.Instance.CameraController.ClearPlacementFocus(resetManualPan: true);
        }

        // Cooldown / Ladung am Quell-Altar verbuchen
        if (AltarManager.Instance != null)
        {
            AltarManager.Instance.ConsumeTeleportUse(sourceAltarCoord);
        }

        // Tick-Kosten zahlen
        if (GameManager.Instance != null && teleportTickCost > 0)
        {
            GameManager.Instance.AdvanceTicks(teleportTickCost, $"Altar-Teleport nach {targetCoord}");
        }

        // Zurück in FreeRoam & Slime an den Ziel-Altar teleportieren
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(GameState.FreeRoam);
        }

        if (MacroGridManager.Instance != null)
        {
            MacroGridManager.Instance.TeleportPlayerToAltar(targetCoord);
        }
    }

    private void CollectReachableTargets(Vector2Int fromCoord)
    {
        reachableTargets.Clear();

        // 1. Optional: Start-Hub als Ziel zulassen (sofern nicht durch Boss-Bannkreis blockiert)
        if (allowTeleportToHub && !isHubBlockedByBoss && MacroGridManager.Instance != null)
        {
            Vector2Int hubCoord = MacroGridManager.Instance.StartCoordinate;
            if (hubCoord != fromCoord && GetGridDistance(fromCoord, hubCoord) <= teleportRadius)
            {
                reachableTargets.Add(hubCoord);
            }
        }

        // 2. Alle anderen aktiven (nicht korrumpierten) Altäre im Radius sammeln
        if (AltarManager.Instance != null)
        {
            foreach (var kvp in AltarManager.Instance.Altars)
            {
                Vector2Int coord = kvp.Key;
                AltarStateData data = kvp.Value;

                if (coord == fromCoord) continue;
                if (data == null || data.status == AltarStatus.Inactive || data.status == AltarStatus.Corrupted) continue;
                if (reachableTargets.Contains(coord)) continue;

                if (GetGridDistance(fromCoord, coord) <= teleportRadius)
                {
                    reachableTargets.Add(coord);
                }
            }
        }

        // Nach Entfernung sortieren (nächster Altar zuerst)
        reachableTargets.Sort((a, b) => GetGridDistance(fromCoord, a).CompareTo(GetGridDistance(fromCoord, b)));
    }

    private bool CheckBossBlocksHub(Vector2Int fromCoord)
    {
        if (!blockHubTeleportNearBoss || GameManager.Instance == null || GameManager.Instance.ActiveBoss == null)
            return false;

        if (GridManager.Instance == null) return false;

        float offset = GridManager.Instance.offset > 0.01f ? GridManager.Instance.offset : 1.1f;
        Vector3 bossLocalPos = GameManager.Instance.ActiveBoss.transform.localPosition;
        Vector2Int bossCoord = new Vector2Int(
            Mathf.RoundToInt(bossLocalPos.x / offset),
            Mathf.RoundToInt(bossLocalPos.y / offset)
        );

        return GetGridDistance(fromCoord, bossCoord) <= bossBlockRadius;
    }

    private int GetGridDistance(Vector2Int a, Vector2Int b)
    {
        // Chebyshev-Abstand (Quadratischer Radius auf dem Grid)
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    private void FocusCameraOnSelectedTarget()
    {
        if (reachableTargets.Count == 0 || GridManager.Instance == null) return;
        if (GameManager.Instance == null || GameManager.Instance.CameraController == null) return;

        Vector2Int target = reachableTargets[Mathf.Clamp(selectedIndex, 0, reachableTargets.Count - 1)];
        float offset = GridManager.Instance.offset > 0.01f ? GridManager.Instance.offset : 1.1f;
        Vector3 localPos = new Vector3(target.x * offset, target.y * offset, -10f);

        GameManager.Instance.CameraController.SetPlacementFocus(localPos, resetManualPan: true);
    }

    private int FindTargetIndexUnderMouse()
    {
        if (Mouse.current == null || GridManager.Instance == null) return -1;

        Camera cam = (GameManager.Instance != null && GameManager.Instance.CameraController != null && GameManager.Instance.CameraController.MacroCamera != null)
            ? GameManager.Instance.CameraController.MacroCamera
            : Camera.main;

        if (cam == null) return -1;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, -cam.transform.position.z));
        Vector3 localPos = GridManager.Instance.transform.InverseTransformPoint(worldPos);

        float offset = GridManager.Instance.offset > 0.01f ? GridManager.Instance.offset : 1.1f;
        Vector2Int hoveredCoord = new Vector2Int(
            Mathf.RoundToInt(localPos.x / offset),
            Mathf.RoundToInt(localPos.y / offset)
        );

        for (int i = 0; i < reachableTargets.Count; i++)
        {
            if (reachableTargets[i] == hoveredCoord) return i;
        }
        return -1;
    }
}