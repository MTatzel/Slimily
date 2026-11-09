using UnityEngine;
using UnityEngine.InputSystem;

public class SlimeInputHandler : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public bool JumpPressedThisFrame { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool DashPressedThisFrame { get; private set; }
    public bool DashHeld { get; private set; }

    private float stickDeadzone = 0.2f;
    private float jumpBufferDuration = 0.12f;
    private float dashBufferDuration = 0.12f;

    private float jumpBufferTimer;
    private float dashBufferTimer;

    public bool HasBufferedJump => jumpBufferTimer > 0f;
    public bool HasBufferedDash => dashBufferTimer > 0f;

    public void Initialize(float deadzone, float jumpBuffer)
    {
        stickDeadzone = deadzone;
        jumpBufferDuration = jumpBuffer;
    }

    public void ConsumeJumpBuffer()
    {
        jumpBufferTimer = 0f;
    }

    public void ConsumeDashBuffer()
    {
        dashBufferTimer = 0f;
    }

    public void ClearAllInputs()
    {
        MoveInput = Vector2.zero;
        JumpPressedThisFrame = false;
        JumpHeld = false;
        DashPressedThisFrame = false;
        DashHeld = false;
        jumpBufferTimer = 0f;
        dashBufferTimer = 0f;
    }

    public void ReadInput(bool blockInput)
    {
        if (blockInput)
        {
            ClearAllInputs();
            return;
        }

        Vector2 input = Vector2.zero;
        bool jumpPressed = false;
        bool jumpIsHeld = false;
        bool dashPressed = false;
        bool dashIsHeld = false;

        // --- 1. TASTATUR ---
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;

            if (keyboard.spaceKey.wasPressedThisFrame) jumpPressed = true;
            if (keyboard.spaceKey.isPressed) jumpIsHeld = true;

            if (keyboard.leftShiftKey.wasPressedThisFrame) dashPressed = true;
            if (keyboard.leftShiftKey.isPressed) dashIsHeld = true;
        }

        // --- 2. GAMEPAD / PS5 DUALSENSE ---
        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            Vector2 stick = Vector2.zero;
            Vector2 rawStick = gamepad.leftStick.ReadValue();

            if (rawStick.magnitude >= stickDeadzone)
            {
                Vector2 stickDir = rawStick.normalized;
                float stickMag = Mathf.Clamp01((rawStick.magnitude - stickDeadzone) / (1f - stickDeadzone));

                if (Mathf.Abs(stickDir.x) > 0.7f)
                {
                    stick = new Vector2(Mathf.Sign(stickDir.x) * stickMag, 0f);
                }
                else if (Mathf.Abs(stickDir.y) > 0.7f)
                {
                    stick = new Vector2(0f, Mathf.Sign(stickDir.y) * stickMag);
                }
                else
                {
                    stick = stickDir * stickMag;
                }
            }

            Vector2 dpad = gamepad.dpad.ReadValue();
            if (dpad.sqrMagnitude > 0.1f)
            {
                input += dpad;
            }
            else if (stick.sqrMagnitude > 0.01f)
            {
                input += stick;
            }

            // Sprung / Schweben auf Kreuz (Button South)
            if (gamepad.buttonSouth.wasPressedThisFrame) jumpPressed = true;
            if (gamepad.buttonSouth.isPressed) jumpIsHeld = true;

            // Dash / Sprint auf R2 (Right Trigger) oder Viereck (Button West)
            if (gamepad.rightTrigger.wasPressedThisFrame || gamepad.buttonWest.wasPressedThisFrame) dashPressed = true;
            if (gamepad.rightTrigger.isPressed || gamepad.buttonWest.isPressed) dashIsHeld = true;
        }

        input.x = Mathf.Clamp(input.x, -1f, 1f);
        input.y = Mathf.Clamp(input.y, -1f, 1f);

        if (jumpPressed)
        {
            jumpBufferTimer = jumpBufferDuration;
        }
        else if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer -= Time.deltaTime;
        }

        if (dashPressed)
        {
            dashBufferTimer = dashBufferDuration;
        }
        else if (dashBufferTimer > 0f)
        {
            dashBufferTimer -= Time.deltaTime;
        }

        MoveInput = input;
        JumpPressedThisFrame = jumpPressed;
        JumpHeld = jumpIsHeld;
        DashPressedThisFrame = dashPressed;
        DashHeld = dashIsHeld;
    }
}