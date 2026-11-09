using UnityEngine;

public class PlatformerDashState : PlatformerSlimeState
{
    private float dashTimer;
    private Vector2 dashDirection;

    public PlatformerDashState(PlatformerSlimeController controller, PlatformerStateMachine stateMachine, PlatformerPhysicsData data)
        : base(controller, stateMachine, data) { }

    public override void Enter()
    {
        bool startedGrounded = controller.IsGrounded;
        Vector2 startNormal = controller.EffectiveSurfaceNormal;

        Input.ConsumeDashBuffer();
        Abilities.ConsumeDash(startedGrounded);
        Abilities.IsDashing = true;

        dashDirection = Abilities.CalculateDashDirection(
            Input.MoveInput,
            startedGrounded,
            startNormal,
            controller.IsOnOuterCorner,
            controller.OuterCornerDiagonalTangent
        );

        dashTimer = (data != null && data.dashDuration > 0.02f) ? data.dashDuration : 0.15f;

        // Optischer Stretch in Dash-Richtung
        Visuals.ApplySquashAndStretch(1.28f, 0.76f);

        // Wenn wir von einer Fläche weg-dashen (z. B. von einer Wand weg oder schräg nach oben),
        // lösen wir uns mit kurzem Cooldown von dieser Fläche.
        if (startedGrounded)
        {
            if (Vector2.Dot(dashDirection, startNormal) > 0.15f)
            {
                controller.TriggerDetach(startNormal, 0.12f);
            }
            else
            {
                // Dash parallel zur Fläche: Nur Eckenstatus klären, ohne Re-Attach zu blockieren
                controller.ClearCornerState();
                controller.IsGrounded = false;
            }
        }

        float speed = (data != null && data.dashSpeed > 1f) ? data.dashSpeed : 22f;
        controller.Velocity = dashDirection * speed;
    }

    public override void FrameUpdate()
    {
        // Falls wir während des Dashes frontal an einer neuen Wand einrasten, beendet das den Dash sauber
        if (controller.IsGrounded)
        {
            FinishDash();
            return;
        }

        // Sprung während des Boden-Dashes (Dash-Jump für weite Sprünge!)
        if (Input.HasBufferedJump && controller.HasCoyoteTime)
        {
            Input.ConsumeJumpBuffer();
            controller.ConsumeJumpBuffer();

            float sprintSpeed = data.moveSpeed * ((data != null && data.sprintSpeedMultiplier > 1f) ? data.sprintSpeedMultiplier : 1.45f);
            float jumpHoriz = Mathf.Sign(dashDirection.x) * sprintSpeed;

            Abilities.IsDashing = false;
            Abilities.IsSprinting = Abilities.enableSprint && Input.DashHeld;
            controller.Velocity = new Vector2(jumpHoriz, data.jumpVelocity);
            stateMachine.ChangeState(controller.AirborneState);
            return;
        }

        dashTimer -= Time.deltaTime;
        if (dashTimer <= 0f)
        {
            FinishDash();
        }
    }

    public override void PhysicsUpdate()
    {
        // Während des Dashes wirkt keine Schwerkraft
        float speed = (data != null && data.dashSpeed > 1f) ? data.dashSpeed : 22f;
        controller.Velocity = dashDirection * speed;
    }

    private void FinishDash()
    {
        Abilities.IsDashing = false;

        // Prüfen, ob wir direkt an einer Fläche anliegen (z. B. nach einem Boden-Dash)
        if (!controller.IsGrounded)
        {
            controller.TrySnapToNearbySurfaceAfterDash();
        }

        // Übergang in Sprint, falls die Dash-Taste weiter gehalten wird
        Abilities.IsSprinting = Abilities.enableSprint && Input.DashHeld;

        if (controller.IsGrounded)
        {
            float endSpeed = Abilities.IsSprinting
                ? data.moveSpeed * data.sprintSpeedMultiplier
                : data.moveSpeed;

            Vector2 normal = controller.EffectiveSurfaceNormal;
            Vector2 tangent = new Vector2(normal.y, -normal.x);
            float tangentSign = Mathf.Sign(Vector2.Dot(dashDirection, tangent));

            controller.Velocity = tangent * (tangentSign * endSpeed);
            stateMachine.ChangeState(controller.GroundedState);
        }
        else
        {
            // Sanftes Auslaufen in der Luft nach dem Dash
            float postDashAirSpeed = data.moveSpeed * (Abilities.IsSprinting ? data.sprintSpeedMultiplier : 1.1f);
            Vector2 clampedVel = dashDirection * postDashAirSpeed;
            if (clampedVel.y > data.jumpVelocity * 0.6f)
            {
                clampedVel.y = data.jumpVelocity * 0.6f;
            }
            controller.Velocity = clampedVel;
            stateMachine.ChangeState(controller.AirborneState);
        }
    }

    public override void Exit()
    {
        Abilities.IsDashing = false;
    }
}