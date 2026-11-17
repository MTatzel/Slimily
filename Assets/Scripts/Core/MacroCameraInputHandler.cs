using UnityEngine;
using UnityEngine.InputSystem;

public class MacroCameraInputHandler : MonoBehaviour
{
    [Header("Controller Deadzones")]
    [SerializeField] private float rightStickDeadzone = 0.18f;
    [SerializeField] private float triggerThreshold = 0.25f;

    public Vector2 PanInput { get; private set; }
    public float ZoomDeltaInput { get; private set; }
    public bool ResetPanPressed { get; private set; }
    public bool ResetZoomPressed { get; private set; }

    public void ClearInputs()
    {
        PanInput = Vector2.zero;
        ZoomDeltaInput = 0f;
        ResetPanPressed = false;
        ResetZoomPressed = false;
    }

    public void ReadInput(GameState currentState)
    {
        // Striktes Trennen von Micro- und Macro-Ebene:
        // In FreeRoam oder während des Gegnerzugs nimmt die Kamera keinerlei manuelle Inputs an.
        bool isInteractiveMacro = (currentState == GameState.MapView ||
                                   currentState == GameState.MacroPlan ||
                                   currentState == GameState.Placement);

        if (!isInteractiveMacro)
        {
            ClearInputs();
            return;
        }

        Vector2 pan = Vector2.zero;
        float zoom = 0f;
        bool resetPan = false;
        bool resetZoom = false;

        bool isPlacement = (currentState == GameState.Placement);
        bool isMapView = (currentState == GameState.MapView);

        // --- 1. GAMEPAD / PS5 DUALSENSE ---
        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            Vector2 rightStick = gamepad.rightStick.ReadValue();
            if (rightStick.magnitude < rightStickDeadzone)
            {
                rightStick = Vector2.zero;
            }
            else
            {
                float normalizedMag = Mathf.Clamp01((rightStick.magnitude - rightStickDeadzone) / (1f - rightStickDeadzone));
                rightStick = rightStick.normalized * normalizedMag;
            }

            // L2 (Left Trigger) fungiert als Zoom-Modifier für den rechten Stick:
            // L2 gehalten + Rechter Stick Hoch/Runter = Stufenlos Zoomen (funktioniert auch im Placement!)
            // L2 nicht gehalten + Rechter Stick = Kamera frei über das Grid bewegen
            bool zoomModifierHeld = gamepad.leftTrigger.ReadValue() > triggerThreshold;

            if (zoomModifierHeld)
            {
                if (Mathf.Abs(rightStick.y) > 0.05f)
                {
                    zoom -= rightStick.y;
                }
            }
            else
            {
                pan += rightStick;
            }

            // Außerhalb von Placement sind L1 / R1 frei und können direkt zum Zoomen genutzt werden
            if (!isPlacement)
            {
                if (gamepad.rightShoulder.isPressed) zoom -= 1f; // R1 = Reinzoomen (Size verkleinern)
                if (gamepad.leftShoulder.isPressed) zoom += 1f;  // L1 = Rauszoomen (Size vergrößern)
            }

            // R3 (Rechten Stick reindrücken) zentriert die Kamera wieder auf Spieler / Bau-Vorschau
            if (gamepad.rightStickButton.wasPressedThisFrame)
            {
                resetPan = true;
            }

            // L3 (Linken Stick reindrücken) setzt die Zoom-Stufe auf Standard zurück
            if (gamepad.leftStickButton.wasPressedThisFrame)
            {
                resetZoom = true;
            }
        }

        // --- 2. TASTATUR ---
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // I / J / K / L bewegt die Kamera in allen Macro-Modi (auch parallel zu WASD im Placement)
            if (keyboard.jKey.isPressed) pan.x -= 1f;
            if (keyboard.lKey.isPressed) pan.x += 1f;
            if (keyboard.iKey.isPressed) pan.y += 1f;
            if (keyboard.kKey.isPressed) pan.y -= 1f;

            // Im reinen MapView sind auch WASD & Pfeiltasten frei für das Kamera-Schwenken
            if (isMapView)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) pan.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) pan.x += 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) pan.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) pan.y -= 1f;
            }

            bool zoomInHeld = keyboard.numpadPlusKey.isPressed ||
                              keyboard.equalsKey.isPressed ||
                              keyboard.pageUpKey.isPressed ||
                              keyboard.rKey.isPressed;

            bool zoomOutHeld = keyboard.numpadMinusKey.isPressed ||
                               keyboard.minusKey.isPressed ||
                               keyboard.pageDownKey.isPressed ||
                               keyboard.fKey.isPressed;

            if (zoomInHeld) zoom -= 1f;
            if (zoomOutHeld) zoom += 1f;

            // Taste C oder Pos1 (Home) zentriert die Kamera wieder
            if (keyboard.cKey.wasPressedThisFrame || keyboard.homeKey.wasPressedThisFrame)
            {
                resetPan = true;
            }

            // Taste 0 setzt den Zoom auf Standard zurück
            if (keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame)
            {
                resetZoom = true;
            }
        }

        // --- 3. MAUSRAD ---
        var mouse = Mouse.current;
        if (mouse != null)
        {
            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                zoom -= Mathf.Sign(scrollY) * 2.5f;
            }
        }

        PanInput = Vector2.ClampMagnitude(pan, 1f);
        ZoomDeltaInput = zoom;
        ResetPanPressed = resetPan;
        ResetZoomPressed = resetZoom;
    }
}