using UnityEngine;

public class PlatformerAirborneState : PlatformerSlimeState
{
    public PlatformerAirborneState(PlatformerSlimeController controller, PlatformerStateMachine stateMachine, PlatformerPhysicsData data) 
        : base(controller, stateMachine, data) { }

    public override void Enter()
    {
        // In der Luft richtet sich der Slime sofort wieder normal nach oben aus
        controller.TargetSurfaceNormal = Vector2.up;
    }

    public override void FrameUpdate()
    {
        // Boden- / Flächenkontakt nach einem Fall
        if (controller.IsGrounded && controller.Velocity.y <= 0.01f)
        {
            stateMachine.ChangeState(controller.GroundedState);
            return;
        }

        // Kontakt mit einer Wand im Flug -> Sofort haften
        if (controller.HitWall)
        {
            controller.Velocity = Vector2.zero;
            stateMachine.ChangeState(controller.GroundedState);
        }
    }

    public override void PhysicsUpdate()
    {
        // In der Luft zieht immer die echte Welt-Schwerkraft nach unten
        ApplyGravity();

        // Horizontale Luftsteuerung in Welt-X
        ApplyHorizontalMovement(data.airAcceleration, data.airAcceleration);
    }
}