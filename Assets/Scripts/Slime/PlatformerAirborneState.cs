using UnityEngine;

public class PlatformerAirborneState : PlatformerSlimeState
{
    private bool jumpCutApplied = false;

    public PlatformerAirborneState(PlatformerSlimeController controller, PlatformerStateMachine stateMachine, PlatformerPhysicsData data) 
        : base(controller, stateMachine, data) { }

    public override void Enter()
    {
        jumpCutApplied = false;
    }

    public override void FrameUpdate()
    {
        // 1. Coyote-Time-Sprung: Falls man gerade erst von einer Kante gefallen ist und noch innerhalb der Kulanzzeit springt
        if (controller.HasBufferedJump && controller.HasCoyoteTime)
        {
            controller.ConsumeJumpBuffer();
            float moveX = Mathf.Abs(controller.MoveInput.x) > 0.1f
                ? controller.MoveInput.x * data.moveSpeed
                : controller.Velocity.x;

            controller.Velocity = new Vector2(moveX, data.jumpVelocity);
            jumpCutApplied = false;
            return;
        }

        // 2. Sanfte Dämpfung nur dann, wenn die Taste sehr früh im Aufstieg losgelassen wird
        if (!jumpCutApplied && !controller.JumpHeld && controller.Velocity.y > data.jumpVelocity * 0.5f)
        {
            float cutMultiplier = (data != null && data.jumpCutMultiplier > 0.5f) ? data.jumpCutMultiplier : 0.85f;
            controller.Velocity = new Vector2(controller.Velocity.x, controller.Velocity.y * cutMultiplier);
            jumpCutApplied = true;
        }
    }

    public override void PhysicsUpdate()
    {
        float apexThreshold = (data != null && data.apexVelocityThreshold > 0.1f) ? data.apexVelocityThreshold : 2.5f;
        bool isNearApex = Mathf.Abs(controller.Velocity.y) <= apexThreshold;

        // 1. Schwerkraft:
        // - Hält der Spieler Sprung NICHT mehr gedrückt während der Slime noch steigt -> sanft erhöhte Schwerkraft für runden Bogen
        // - Nahe am Scheitelpunkt bei gehaltener Taste -> leichte Apex-Hang-Time
        float gravMult = 1f;
        if (controller.Velocity.y > 0f && !controller.JumpHeld)
        {
            gravMult = (data != null && data.lowJumpGravityMultiplier > 1f) ? data.lowJumpGravityMultiplier : 1.75f;
        }
        else if (isNearApex && controller.JumpHeld && data != null && data.apexGravityMultiplier > 0.1f)
        {
            gravMult = data.apexGravityMultiplier;
        }

        Vector2 vel = controller.Velocity;
        vel.y += (data.gravity * gravMult) * Time.fixedDeltaTime;
        vel.y = Mathf.Max(vel.y, -data.maxFallSpeed);
        controller.Velocity = vel;

        // 2. Horizontale Luftsteuerung:
        // Volle Beschleunigung beim Steuern, aber minimaler Luftwiderstand (airDeceleration) beim Loslassen!
        float airBoost = isNearApex && data != null && data.apexAirAccelerationMultiplier > 0.5f
            ? data.apexAirAccelerationMultiplier
            : 1f;

        float accel = data.airAcceleration * airBoost;
        float decel = (data != null && data.airDeceleration >= 0f) ? data.airDeceleration : 5f;

        ApplyHorizontalMovement(accel, decel);
    }
}