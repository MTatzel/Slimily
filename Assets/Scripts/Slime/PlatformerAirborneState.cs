using UnityEngine;

public class PlatformerAirborneState : PlatformerSlimeState
{
    public PlatformerAirborneState(PlatformerSlimeController controller, PlatformerStateMachine stateMachine, PlatformerPhysicsData data) 
        : base(controller, stateMachine, data) { }

    public override void Enter()
    {
        // Sobald er in die Luft geht (Sprung/Fall), richten sich die Füße sofort nach unten aus
        controller.TargetSurfaceNormal = Vector2.up;
    }

    public override void FrameUpdate()
    {
        // 1. Kontakt mit einer Wand im Flug -> Sofort haften
        if (controller.HitWall)
        {
            controller.Velocity = Vector2.zero;
            stateMachine.ChangeState(controller.GroundedState);
            return;
        }

        // 2. Bodenkontakt nach Fall/Sprung
        if (controller.IsGrounded && controller.Velocity.y <= 0.01f)
        {
            stateMachine.ChangeState(controller.GroundedState);
        }
    }

    public override void PhysicsUpdate()
    {
        // Schwerkraft nach unten
        ApplyGravity();

        // Horizontale Luftsteuerung
        ApplyHorizontalMovement(data.airAcceleration, data.airAcceleration);
    }
}