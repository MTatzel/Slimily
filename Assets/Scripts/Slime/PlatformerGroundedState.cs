using UnityEngine;

public class PlatformerGroundedState : PlatformerSlimeState
{
    public PlatformerGroundedState(PlatformerSlimeController controller, PlatformerStateMachine stateMachine, PlatformerPhysicsData data) 
        : base(controller, stateMachine, data) { }

    public override void FrameUpdate()
    {
        // 1. Wenn Fläche verloren geht -> Fallen
        if (!controller.IsGrounded)
        {
            stateMachine.ChangeState(controller.AirborneState);
            return;
        }

        // 2. Sprung von der Fläche
        if (controller.JumpTriggered)
        {
            Vector2 surfaceNormal = controller.CurrentSurfaceNormal;

            // Ist es eine vertikale Wand?
            bool isWall = Mathf.Abs(surfaceNormal.x) > 0.5f;

            if (isWall)
            {
                // Wall-Jump: Weg von der Wand + Schub nach oben
                Vector2 jumpDir = (surfaceNormal + Vector2.up * 1.2f).normalized;
                controller.Velocity = jumpDir * data.jumpVelocity;
            }
            else
            {
                // Normaler Bodensprung: Entlang der Flächennormale nach oben
                controller.Velocity = surfaceNormal * data.jumpVelocity;
            }

            stateMachine.ChangeState(controller.AirborneState);
        }
    }

    public override void PhysicsUpdate()
    {
        Vector2 normal = controller.CurrentSurfaceNormal;
        bool isWall = Mathf.Abs(normal.x) > 0.5f;

        // Eingabeachse wählen: An der Wand W/S (y), am Boden A/D (x)
        float activeInput = 0f;

        if (isWall)
        {
            // Rechte Wand (normal.x < 0) vs. Linke Wand (normal.x > 0)
            // W soll immer nach oben klettern, S immer nach unten
            activeInput = (normal.x < 0f) ? controller.MoveInput.y : -controller.MoveInput.y;
        }
        else
        {
            activeInput = controller.MoveInput.x;
        }

        // Bewegung entlang der lokalen Tangente berechnen
        float targetSpeed = activeInput * data.moveSpeed;
        Vector2 vel = controller.Velocity;

        float accel = (Mathf.Abs(targetSpeed) > 0.01f) ? data.groundAcceleration : data.groundDeceleration;
        vel.x = Mathf.MoveTowards(vel.x, targetSpeed, accel * Time.fixedDeltaTime);
        vel.y = 0f; // Haftung an der Fläche halten

        controller.Velocity = vel;
    }
}