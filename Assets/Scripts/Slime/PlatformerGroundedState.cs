using UnityEngine;

public class PlatformerGroundedState : PlatformerSlimeState
{
    private bool justEntered;

    public PlatformerGroundedState(PlatformerSlimeController controller, PlatformerStateMachine stateMachine, PlatformerPhysicsData data) 
        : base(controller, stateMachine, data) { }

    public override void Enter()
    {
        justEntered = true;
        controller.IsGrounded = true;
        controller.Velocity = Vector2.zero;
    }

    public override void FrameUpdate()
    {
        if (justEntered)
        {
            justEntered = false;
            return;
        }

        if (!controller.IsGrounded)
        {
            stateMachine.ChangeState(controller.AirborneState);
            return;
        }

        Vector2 surfaceNormal = controller.CurrentSurfaceNormal;
        bool isCeiling = surfaceNormal.y < -0.5f;
        bool isWall = Mathf.Abs(surfaceNormal.x) > 0.5f;

        // 1. Dropdown an der Decke via Taste S
        if (isCeiling && controller.MoveInput.y < -0.1f)
        {
            controller.TriggerDetach();

            // Welt-Richtung direkt aus dem Tastatur-Input ableiten,
            // um Vorzeichen-Konflikte der Decken-Tangente zu vermeiden:
            float airMoveX = controller.MoveInput.x * data.moveSpeed;
            controller.Velocity = new Vector2(airMoveX, -2f);

            stateMachine.ChangeState(controller.AirborneState);
            return;
        }

        // 2. Sprung von der Fläche (entweder frisch gedrückt oder gepuffert)
        if (controller.HasBufferedJump)
        {
            controller.ConsumeJumpBuffer();
            controller.TriggerDetach();

            if (isCeiling)
            {
                // Welt-X aus dem Input ableiten und kontrollierten Abstoßimpuls nach unten setzen:
                float airMoveX = controller.MoveInput.x * data.moveSpeed;
                float verticalDrop = -data.jumpVelocity * 0.5f;
                controller.Velocity = new Vector2(airMoveX, verticalDrop);
            }
            else if (isWall)
            {
                Vector2 jumpDir = (surfaceNormal + Vector2.up * 1.2f).normalized;
                controller.Velocity = jumpDir * data.jumpVelocity;
            }
            else
            {
                float currentMoveX = controller.Velocity.x;
                controller.Velocity = new Vector2(currentMoveX, data.jumpVelocity);
            }

            stateMachine.ChangeState(controller.AirborneState);
        }
    }

    public override void PhysicsUpdate()
    {
        Vector2 normal = controller.CurrentSurfaceNormal;
        bool isWall = Mathf.Abs(normal.x) > 0.5f;
        bool isCeiling = normal.y < -0.5f;

        float activeInput = 0f;

        if (isWall)
        {
            // Primär: Vertikale Eingabe (W / S) steuert Wände auf und ab
            if (Mathf.Abs(controller.MoveInput.y) > 0.1f)
            {
                activeInput = (normal.x < 0f) ? controller.MoveInput.y : -controller.MoveInput.y;
            }
            // Nahtloser Übergang an Innenecken: Wer weiter gegen die Wand drückt, klettert automatisch hoch!
            else if (Mathf.Abs(controller.MoveInput.x) > 0.1f)
            {
                if (normal.x < 0f) // Rechte Wand
                {
                    activeInput = (controller.MoveInput.x > 0.1f) ? 1f : -1f;
                }
                else // Linke Wand
                {
                    activeInput = (controller.MoveInput.x < -0.1f) ? -1f : 1f;
                }
            }
        }
        else if (isCeiling)
        {
            activeInput = -controller.MoveInput.x;
        }
        else
        {
            activeInput = controller.MoveInput.x;
        }

        float targetSpeed = activeInput * data.moveSpeed;
        Vector2 vel = controller.Velocity;

        float accel = (Mathf.Abs(targetSpeed) > 0.01f) ? data.groundAcceleration : data.groundDeceleration;
        vel.x = Mathf.MoveTowards(vel.x, targetSpeed, accel * Time.fixedDeltaTime);
        vel.y = 0f;

        controller.Velocity = vel;
    }
}