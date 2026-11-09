using UnityEngine;

public class PlatformerAirborneState : PlatformerSlimeState
{
    private bool jumpCutApplied = false;

    public PlatformerAirborneState(PlatformerSlimeController controller, PlatformerStateMachine stateMachine, PlatformerPhysicsData data) 
        : base(controller, stateMachine, data) { }

    public override void Enter()
    {
        jumpCutApplied = false;
        Abilities.IsBalloonFloating = false;
    }

    public override void Exit()
    {
        Abilities.IsBalloonFloating = false;
    }

    public override void FrameUpdate()
    {
        // 1. Air-Dash prüfen (Shift / R2 / Viereck)
        if (Input.HasBufferedDash && Abilities.CanUseDash(isGrounded: false))
        {
            stateMachine.ChangeState(controller.DashState);
            return;
        }

        if (!Input.DashHeld)
        {
            Abilities.IsSprinting = false;
        }

        // 2. Coyote-Time-Sprung (kurz nach Verlassen einer Kante)
        if (controller.HasBufferedJump && controller.HasCoyoteTime)
        {
            controller.ConsumeJumpBuffer();
            ExecuteAirJump(data.jumpVelocity);
            return;
        }

        // 3. Doppelsprung in der Luft
        if (controller.HasBufferedJump && Abilities.CanUseDoubleJump())
        {
            controller.ConsumeJumpBuffer();
            Abilities.ConsumeDoubleJump();

            float dblJumpVel = (data != null && data.doubleJumpVelocity > 1f) ? data.doubleJumpVelocity : 12.8f;
            ExecuteAirJump(dblJumpVel);
            return;
        }

        // 4. Sanfte Dämpfung bei sehr frühem Loslassen der Sprungtaste im Aufstieg
        if (!jumpCutApplied && !Input.JumpHeld && controller.Velocity.y > data.jumpVelocity * 0.5f)
        {
            float cutMultiplier = (data != null && data.jumpCutMultiplier > 0.5f) ? data.jumpCutMultiplier : 0.85f;
            controller.Velocity = new Vector2(controller.Velocity.x, controller.Velocity.y * cutMultiplier);
            jumpCutApplied = true;
        }

        Abilities.UpdateFacingDirection(Input.MoveInput, Vector2.right, 0f);
    }

    private void ExecuteAirJump(float verticalVelocity)
    {
        float stretch = (data != null) ? data.jumpStretchAmount : 0.16f;
        Visuals.ApplySquashAndStretch(1f - stretch * 0.8f, 1f + stretch * 1.1f);

        float speedMult = (Abilities.IsSprinting && data != null && data.sprintSpeedMultiplier > 1f)
            ? data.sprintSpeedMultiplier
            : 1f;

        float moveX = Mathf.Abs(Input.MoveInput.x) > 0.1f
            ? Input.MoveInput.x * data.moveSpeed * speedMult
            : controller.Velocity.x;

        controller.Velocity = new Vector2(moveX, verticalVelocity);
        jumpCutApplied = false;
        Abilities.IsBalloonFloating = false;
    }

    public override void PhysicsUpdate()
    {
        float apexThreshold = (data != null && data.apexVelocityThreshold > 0.1f) ? data.apexVelocityThreshold : 2.5f;
        bool isNearApex = Mathf.Abs(controller.Velocity.y) <= apexThreshold;

        // --- BALLON-SCHWEBEN PRÜFEN ---
        // Aktiviert sich, sobald der Slime den Scheitelpunkt erreicht hat (Velocity.y <= 0.35f)
        // und der Spieler die Sprungtaste gedrückt hält.
        bool wantsToFloat = Input.JumpHeld && controller.Velocity.y <= 0.35f && Abilities.CanUseBalloonFloat();

        if (wantsToFloat)
        {
            Abilities.IsBalloonFloating = true;
            Abilities.ConsumeFloatTime(Time.fixedDeltaTime);

            float targetFloatFall = (data != null && data.floatFallSpeed >= 0f) ? -data.floatFallSpeed : -0.6f;
            float brakeRate = (data != null && data.floatBrakeAcceleration > 1f) ? data.floatBrakeAcceleration : 85f;

            Vector2 floatVel = controller.Velocity;
            floatVel.y = Mathf.MoveTowards(floatVel.y, targetFloatFall, brakeRate * Time.fixedDeltaTime);
            controller.Velocity = floatVel;
        }
        else
        {
            Abilities.IsBalloonFloating = false;

            // Reguläre Schwerkraft (inkl. Low-Jump-Parabel und Apex-Hang-Time)
            float gravMult = 1f;
            if (controller.Velocity.y > 0f && !Input.JumpHeld)
            {
                gravMult = (data != null && data.lowJumpGravityMultiplier > 1f) ? data.lowJumpGravityMultiplier : 1.75f;
            }
            else if (isNearApex && Input.JumpHeld && data != null && data.apexGravityMultiplier > 0.1f)
            {
                gravMult = data.apexGravityMultiplier;
            }

            Vector2 vel = controller.Velocity;
            vel.y += (data.gravity * gravMult) * Time.fixedDeltaTime;
            vel.y = Mathf.Max(vel.y, -data.maxFallSpeed);
            controller.Velocity = vel;
        }

        // --- HORIZONTALE LUFTSTEUERUNG ---
        float airBoost = (isNearApex || Abilities.IsBalloonFloating) && data != null && data.apexAirAccelerationMultiplier > 0.5f
            ? data.apexAirAccelerationMultiplier
            : 1f;

        float accel = data.airAcceleration * airBoost;
        float decel = (data != null && data.airDeceleration >= 0f) ? data.airDeceleration : 5f;

        ApplyHorizontalMovement(accel, decel);
    }
}