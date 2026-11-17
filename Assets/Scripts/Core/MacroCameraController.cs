using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MacroCameraInputHandler))]
public class MacroCameraController : MonoBehaviour
{
    [Header("Kamera-Referenzen")]
    [SerializeField] private Camera macroCamera;
    [SerializeField] private Camera mainCamera;

    [Header("Zoom-Einstellungen (Macro Grid)")]
    [Tooltip("Standard-Zoomweite beim ersten Start (kleiner = näher dran).")]
    [SerializeField] private float defaultZoomSize = 14f;
    [Tooltip("Minimaler Zoom (Maximale Nahaufnahme).")]
    [SerializeField] private float minZoomSize = 5f;
    [Tooltip("Maximaler Zoom (Volle Übersicht über das Grid).")]
    [SerializeField] private float maxZoomSize = 30f;
    [Tooltip("Zoom-Geschwindigkeit per Controller/Tastatur.")]
    [SerializeField] private float zoomInputSpeed = 16f;
    [Tooltip("Wie geschmeidig die Kamera dem Ziel-Zoom folgt.")]
    [SerializeField] private float zoomSmoothSpeed = 14f;

    [Header("Kamera-Bewegung (Rechter Stick / Panning)")]
    [Tooltip("Basis-Schwenkgeschwindigkeit auf dem Macro-Grid.")]
    [SerializeField] private float basePanSpeed = 18f;
    [Tooltip("Skaliert die Schwenkgeschwindigkeit mit dem aktuellen Zoom (weiter rausgezoomt = schnelleres Schwenken).")]
    [SerializeField] private bool scalePanSpeedWithZoom = true;
    [Tooltip("Wie weich die Kamera der Zielposition folgt.")]
    [SerializeField] private float focusFollowSpeed = 14f;

    [Header("Zoom-Speicherung")]
    [Tooltip("Speichert die eingestellte Zoom-Weite auch über das Beenden des Spiels hinaus.")]
    [SerializeField] private bool persistZoomInPlayerPrefs = true;

    [Header("Übergang (Micro <-> Macro)")]
    [SerializeField] private float cameraTransitionDuration = 0.35f;
    [SerializeField] private float transitionCloseUpSize = 4.5f;

    private const string ZoomPrefKey = "Slimily_MacroSavedZoom";

    private MacroCameraInputHandler inputHandler;
    private float savedZoomSize;
    private float targetZoomSize;
    private bool isZoomInitialized = false;

    // Persistente Zielposition auf dem Macro-Grid (bleibt nach dem Bauen dort, wo gebaut wurde)
    private Vector3 targetFocusLocalPos = new Vector3(0f, 0f, -10f);
    private bool hasInitializedFocusPos = false;

    private Coroutine activeCameraTransition;
    private Coroutine activeRumbleCoroutine;

    public bool IsTransitioning => activeCameraTransition != null;
    public float CurrentSavedZoom => savedZoomSize;
    public Camera MacroCamera => macroCamera;

    private void Awake()
    {
        inputHandler = GetComponent<MacroCameraInputHandler>();
        if (inputHandler == null) inputHandler = gameObject.AddComponent<MacroCameraInputHandler>();

        InitializeZoomValue();
        FindCamerasIfNeeded();
    }

    private void InitializeZoomValue()
    {
        if (isZoomInitialized) return;

        if (persistZoomInPlayerPrefs && PlayerPrefs.HasKey(ZoomPrefKey))
        {
            savedZoomSize = Mathf.Clamp(PlayerPrefs.GetFloat(ZoomPrefKey, defaultZoomSize), minZoomSize, maxZoomSize);
        }
        else
        {
            savedZoomSize = Mathf.Clamp(defaultZoomSize, minZoomSize, maxZoomSize);
        }

        targetZoomSize = savedZoomSize;
        isZoomInitialized = true;
    }

    public void SetupCamerasIfEmpty(Camera existingMacroCam, Camera existingMainCam)
    {
        if (macroCamera == null && existingMacroCam != null) macroCamera = existingMacroCam;
        if (mainCamera == null && existingMainCam != null) mainCamera = existingMainCam;
        FindCamerasIfNeeded();
    }

    public void InitializeForLevel(LevelConfiguration config)
    {
        InitializeZoomValue();
        FindCamerasIfNeeded();

        if (config != null)
        {
            float fullGridHalfHeight = (config.gridHeight * config.cellOffset) * 0.5f + 2f;
            maxZoomSize = Mathf.Max(maxZoomSize, fullGridHalfHeight);
        }

        SnapToMacroPlayerFocus(keepCurrentActiveState: true);
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        GameState state = GameManager.Instance.currentState;
        bool isMacroState = (state == GameState.MacroPlan ||
                             state == GameState.MapView ||
                             state == GameState.Placement ||
                             state == GameState.EnemyTurn);

        if (!isMacroState || IsTransitioning)
        {
            if (inputHandler != null) inputHandler.ClearInputs();
            return;
        }

        if (!hasInitializedFocusPos)
        {
            targetFocusLocalPos = GetPlayerFocusLocalPosition();
            hasInitializedFocusPos = true;
        }

        inputHandler.ReadInput(state);
        ProcessCameraInputs();
        UpdateCameraTransformInMacro();
    }

    private void ProcessCameraInputs()
    {
        if (inputHandler == null) return;

        // R3 / Taste C zentriert die Kamera bei Bedarf manuell wieder auf das Spielerfeld
        if (inputHandler.ResetPanPressed)
        {
            targetFocusLocalPos = GetPlayerFocusLocalPosition();
        }

        if (inputHandler.ResetZoomPressed)
        {
            ResetZoomToDefault();
        }

        // 1. Zoom verarbeiten
        if (Mathf.Abs(inputHandler.ZoomDeltaInput) > 0.001f)
        {
            float zoomStep = inputHandler.ZoomDeltaInput * zoomInputSpeed * Time.deltaTime;
            if (Mathf.Abs(inputHandler.ZoomDeltaInput) > 1.5f)
            {
                zoomStep = Mathf.Sign(inputHandler.ZoomDeltaInput) * 1.8f;
            }
            SetSavedZoom(targetZoomSize + zoomStep);
        }

        // 2. Freies Kamera-Schwenken (Rechter Stick / IJKL) verschiebt direkt die Zielposition
        if (inputHandler.PanInput.sqrMagnitude > 0.001f)
        {
            float zoomFactor = scalePanSpeedWithZoom
                ? Mathf.Clamp(targetZoomSize / Mathf.Max(1f, defaultZoomSize), 0.55f, 2.2f)
                : 1f;

            Vector2 panDelta = inputHandler.PanInput * (basePanSpeed * zoomFactor * Time.deltaTime);
            targetFocusLocalPos = ClampLocalPositionToGridBounds(targetFocusLocalPos + (Vector3)panDelta);
        }
    }

    private void UpdateCameraTransformInMacro()
    {
        if (macroCamera == null) return;

        macroCamera.orthographicSize = Mathf.Lerp(
            macroCamera.orthographicSize,
            targetZoomSize,
            Time.deltaTime * zoomSmoothSpeed
        );

        Vector3 clampedTarget = ClampLocalPositionToGridBounds(targetFocusLocalPos);
        macroCamera.transform.localPosition = Vector3.Lerp(
            macroCamera.transform.localPosition,
            clampedTarget,
            Time.deltaTime * focusFollowSpeed
        );
    }

    /// <summary>
    /// Wird vom PlacementManager aufgerufen, wenn der zu platzierende Würfel bewegt oder gedreht wird.
    /// </summary>
    public void SetPlacementFocus(Vector3 shapeCenterLocalPos, bool resetManualPan = true)
    {
        targetFocusLocalPos = ClampLocalPositionToGridBounds(new Vector3(shapeCenterLocalPos.x, shapeCenterLocalPos.y, -10f));
        hasInitializedFocusPos = true;
    }

    /// <summary>
    /// Beim Beenden des Platzierens bleibt targetFocusLocalPos unverändert an der aktuellen Baustelle stehen!
    /// </summary>
    public void ClearPlacementFocus(bool resetManualPan = true)
    {
        // Absichtlich kein Rücksprung zum Spieler: Die Kamera bleibt dort, wo zuletzt gebaut/geschaut wurde.
    }

    public void SetSavedZoom(float newZoom)
    {
        InitializeZoomValue();
        savedZoomSize = Mathf.Clamp(newZoom, minZoomSize, maxZoomSize);
        targetZoomSize = savedZoomSize;

        if (persistZoomInPlayerPrefs)
        {
            PlayerPrefs.SetFloat(ZoomPrefKey, savedZoomSize);
        }
    }

    [ContextMenu("Gespeicherten Zoom auf Standard zurücksetzen")]
    public void ResetZoomToDefault()
    {
        SetSavedZoom(defaultZoomSize);
    }

    public Vector3 GetPlayerFocusLocalPosition()
    {
        if (MacroGridManager.Instance != null && GridManager.Instance != null && GridManager.Instance.offset > 0.01f)
        {
            Vector2Int coord = MacroGridManager.Instance.CurrentCoordinate;
            float offset = GridManager.Instance.offset;
            return new Vector3(coord.x * offset, coord.y * offset, -10f);
        }

        if (MacroSlimeMarker.Instance != null)
        {
            Vector3 markerPos = MacroSlimeMarker.Instance.transform.localPosition;
            return new Vector3(markerPos.x, markerPos.y, -10f);
        }

        return new Vector3(0f, 0f, -10f);
    }

    private Vector3 ClampLocalPositionToGridBounds(Vector3 candidateLocalPos)
    {
        if (GridManager.Instance == null || GridManager.Instance.width <= 0 || GridManager.Instance.height <= 0)
        {
            return new Vector3(candidateLocalPos.x, candidateLocalPos.y, -10f);
        }

        float offset = GridManager.Instance.offset > 0.01f ? GridManager.Instance.offset : 1.1f;
        float maxX = (GridManager.Instance.width - 1) * offset;
        float maxY = (GridManager.Instance.height - 1) * offset;

        float clampedX = Mathf.Clamp(candidateLocalPos.x, 0f, maxX);
        float clampedY = Mathf.Clamp(candidateLocalPos.y, 0f, maxY);

        return new Vector3(clampedX, clampedY, -10f);
    }

    /// <summary>
    /// Setzt die Macro-Kamera sofort auf das Spielerfeld und übernimmt den gespeicherten Zoom.
    /// </summary>
    public void SnapToMacroPlayerFocus(bool keepCurrentActiveState = false)
    {
        InitializeZoomValue();
        FindCamerasIfNeeded();

        targetFocusLocalPos = GetPlayerFocusLocalPosition();
        hasInitializedFocusPos = true;

        if (!keepCurrentActiveState)
        {
            SetCameraActive(macro: true, micro: false);
        }

        if (macroCamera != null)
        {
            macroCamera.transform.localPosition = targetFocusLocalPos;
            macroCamera.orthographicSize = savedZoomSize;
        }
    }

    public void TransitionToMacro(Action onComplete = null)
    {
        InitializeZoomValue();
        FindCamerasIfNeeded();

        // Beim Wechsel von Micro -> Macro wird der Fokus auf das aktuelle Spielerfeld gesetzt
        targetFocusLocalPos = GetPlayerFocusLocalPosition();
        hasInitializedFocusPos = true;

        if (activeCameraTransition != null) StopCoroutine(activeCameraTransition);
        activeCameraTransition = StartCoroutine(TransitionToMacroRoutine(onComplete));
    }

    public void TransitionToMicro(Action onComplete = null)
    {
        InitializeZoomValue();
        FindCamerasIfNeeded();

        if (activeCameraTransition != null) StopCoroutine(activeCameraTransition);
        activeCameraTransition = StartCoroutine(TransitionToMicroRoutine(onComplete));
    }

    private IEnumerator TransitionToMacroRoutine(Action onComplete)
    {
        Vector3 playerFocusPos = GetPlayerFocusLocalPosition();
        targetFocusLocalPos = playerFocusPos;

        float startSize = Mathf.Min(transitionCloseUpSize, savedZoomSize * 0.85f);

        if (macroCamera != null)
        {
            macroCamera.transform.localPosition = playerFocusPos;
            macroCamera.orthographicSize = startSize;
        }

        SetCameraActive(macro: true, micro: false);

        float elapsed = 0f;
        while (elapsed < cameraTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / cameraTransitionDuration);

            playerFocusPos = GetPlayerFocusLocalPosition();
            targetFocusLocalPos = playerFocusPos;

            if (macroCamera != null)
            {
                macroCamera.transform.localPosition = playerFocusPos;
                macroCamera.orthographicSize = Mathf.Lerp(startSize, savedZoomSize, t);
            }
            yield return null;
        }

        if (macroCamera != null)
        {
            macroCamera.transform.localPosition = targetFocusLocalPos;
            macroCamera.orthographicSize = savedZoomSize;
        }

        activeCameraTransition = null;
        onComplete?.Invoke();
    }

    private IEnumerator TransitionToMicroRoutine(Action onComplete)
    {
        Vector3 startLocalPos = macroCamera != null ? macroCamera.transform.localPosition : targetFocusLocalPos;
        float startSize = macroCamera != null ? macroCamera.orthographicSize : savedZoomSize;
        float endSize = Mathf.Min(transitionCloseUpSize, savedZoomSize * 0.85f);

        float elapsed = 0f;
        while (elapsed < cameraTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / cameraTransitionDuration);

            Vector3 targetLocalPos = GetPlayerFocusLocalPosition();
            if (macroCamera != null)
            {
                macroCamera.transform.localPosition = Vector3.Lerp(startLocalPos, targetLocalPos, t);
                macroCamera.orthographicSize = Mathf.Lerp(startSize, endSize, t);
            }
            yield return null;
        }

        SetCameraActive(macro: false, micro: true);
        activeCameraTransition = null;
        onComplete?.Invoke();
    }

    public void TriggerMicroCameraRumble(PlatformerSlimeController slimeController, float intensity = 0.25f, float duration = 0.3f)
    {
        if (slimeController == null) return;

        FindCamerasIfNeeded();
        if (activeRumbleCoroutine != null) StopCoroutine(activeRumbleCoroutine);
        activeRumbleCoroutine = StartCoroutine(MicroRumbleRoutine(slimeController, intensity, duration));
    }

    private IEnumerator MicroRumbleRoutine(PlatformerSlimeController slimeController, float intensity, float duration)
    {
        slimeController.ApplySquashAndStretch(1f + intensity * 0.45f, 1f - intensity * 0.35f);

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

    public void SetCameraActive(bool macro, bool micro)
    {
        FindCamerasIfNeeded();
        if (macroCamera != null) macroCamera.gameObject.SetActive(macro);
        if (mainCamera != null) mainCamera.gameObject.SetActive(micro);
    }

    private void FindCamerasIfNeeded()
    {
        if (macroCamera == null)
        {
            var macroObj = GameObject.Find("MacroCamera");
            if (macroObj != null) macroCamera = macroObj.GetComponent<Camera>();
        }

        if (mainCamera == null)
        {
            var camObj = GameObject.Find("Main Camera");
            mainCamera = camObj != null ? camObj.GetComponent<Camera>() : Camera.main;
        }
    }
}