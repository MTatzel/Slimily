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
        controller.CurrentTangentSpeed = 0f;
    }

    public override void FrameUpdate()
    {
        if (justEntered)
        {
            justEntered = false;
            return;
        }

        // Abriss in AirborneState NUR wenn gar keine Oberfläche mehr im Erkennungsradius existiert
        if (!controller.IsGrounded)
        {
            stateMachine.ChangeState(controller.AirborneState);
            return;
        }

        Vector2 surfaceNormal = controller.CurrentSurfaceNormal;
        bool isCeiling = Vector2.Dot(surfaceNormal, Vector2.down) > 0.5f;

        // 1. Manuelles Lösen an der Decke (Dropdown via Taste S / Pfeil nach unten)
        if (isCeiling && controller.MoveInput.y < -0.1f)
        {
            controller.TriggerDetach(data.detachCooldown);
            controller.Velocity = Vector2.down * data.ceilingDropVelocity;
            stateMachine.ChangeState(controller.AirborneState);
            return;
        }

        // 2. Sprung von der Fläche (entweder frisch gedrückt oder gepuffert)
        if (controller.HasBufferedJump)
        {
            controller.ConsumeJumpBuffer();
            controller.TriggerDetach(data.detachCooldown);

            // Der Absprungvektor führt exakt senkrecht von der Oberfläche weg (Richtung: Oberflächennormale / transform.up).
            // An der Decke springt der Slime nach unten in den Raum hinein.
            Vector2 jumpDir = surfaceNormal;
            Vector2 tangent = controller.SurfaceTangent;

            Vector2 jumpVel = jumpDir * data.jumpVelocity + tangent * (controller.CurrentTangentSpeed * data.jumpTangentMomentumMultiplier);
            controller.Velocity = jumpVel;

            stateMachine.ChangeState(controller.AirborneState);
            return;
        }
    }

    public override void PhysicsUpdate()
    {
        // 3. Eingabe entlang der horizontalen Achse (Input.x) steuert die Bewegung entlang der Oberflächentangente.
        // Stick rechts (Input.x > 0) = im Uhrzeigersinn um die Form/den Block herum.
        // Stick links (Input.x < 0) = gegen den Uhrzeigersinn.
        float targetSpeed = controller.MoveInput.x * data.moveSpeed;

        float accel = (Mathf.Abs(targetSpeed) > 0.01f) ? data.groundAcceleration : data.groundDeceleration;
        controller.CurrentTangentSpeed = Mathf.MoveTowards(
            controller.CurrentTangentSpeed, 
            targetSpeed, 
            accel * Time.fixedDeltaTime
        );

        // Welt-Velocity spiegelt die Bewegung entlang der Oberflächentangente wider
        controller.Velocity = controller.SurfaceTangent * controller.CurrentTangentSpeed;
    }
}