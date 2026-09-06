using UnityEngine;

public class PlatformerAirborneState : PlatformerSlimeState
{
    public PlatformerAirborneState(PlatformerSlimeController controller, PlatformerStateMachine stateMachine, PlatformerPhysicsData data) 
        : base(controller, stateMachine, data) { }

    public override void Enter()
    {
    }

    public override void FrameUpdate()
    {
        // 1. Kontakt mit einer Wand ODER Decke im Flug -> Sofort anheften
        if (controller.HitWall || controller.HitCeiling)
        {
            controller.Velocity = Vector2.zero;
            controller.IsGrounded = true;
            stateMachine.ChangeState(controller.GroundedState);
            return;
        }

        // 2. Bodenkontakt nach einem Fall
        if (controller.IsGrounded && controller.Velocity.y <= 0.01f)
        {
            stateMachine.ChangeState(controller.GroundedState);
        }
    }

    public override void PhysicsUpdate()
    {
        ApplyGravity();
        ApplyHorizontalMovement(data.airAcceleration, data.airAcceleration);
    }
}