using UnityEngine;

public class PlatformerAirborneState : PlatformerSlimeState
{
    public PlatformerAirborneState(PlatformerSlimeController controller, PlatformerStateMachine stateMachine, PlatformerPhysicsData data) 
        : base(controller, stateMachine, data) { }

    public override void Enter()
    {
        // Nicht mehr sofort auf Vector2.up setzen, damit die Ausrichtung bei kurzen
        // Abrissen oder Kantenwechseln stabil bleibt.
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

        // 2. Boden- / Deckenkontakt nach Fall/Sprung
        if (controller.IsGrounded && controller.Velocity.y <= 0.01f)
        {
            stateMachine.ChangeState(controller.GroundedState);
        }
    }

    public override void PhysicsUpdate()
    {
        // In der Luft zieht immer die echte Schwerkraft nach unten
        ApplyGravity();

        // Horizontale Steuerung in der Luft
        ApplyHorizontalMovement(data.airAcceleration, data.airAcceleration);
    }
}