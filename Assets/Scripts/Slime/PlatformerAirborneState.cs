using UnityEngine;

public class PlatformerAirborneState : PlatformerSlimeState
{
    public PlatformerAirborneState(PlatformerSlimeController controller, PlatformerStateMachine stateMachine, PlatformerPhysicsData data) 
        : base(controller, stateMachine, data) { }

    public override void Enter()
    {
        // Slime richtet sich im Flug sanft wieder zur Welt-Oberseite (Up) aus
        controller.TargetSurfaceNormal = Vector2.up;
        controller.IsGrounded = false;
    }

    public override void FrameUpdate()
    {
        // Das Anheften wird nun direkt in MoveAndResolveCollisions präzise und race-condition-frei ausgelöst.
    }

    public override void PhysicsUpdate()
    {
        // Reale Welt-Schwerkraft zieht nach unten
        ApplyGravity();

        // Horizontale Luftsteuerung in Welt-X
        ApplyHorizontalMovement(data.airAcceleration, data.airAcceleration);
    }
}