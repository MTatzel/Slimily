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
    }

    public override void FrameUpdate()
    {
        // 1-Frame-Schutz beim Betreten
        if (justEntered)
        {
            justEntered = false;
            return;
        }

        // Kontakt verloren -> Fallen
        if (!controller.IsGrounded)
        {
            stateMachine.ChangeState(controller.AirborneState);
            return;
        }

        Vector2 surfaceNormal = controller.CurrentSurfaceNormal;
        bool isCeiling = surfaceNormal.y < -0.5f;
        bool isWall = Mathf.Abs(surfaceNormal.x) > 0.5f;

        // 1. Spezieller Decken-Drop mit Taste S (einfach fallen lassen)
        if (isCeiling && controller.MoveInput.y < -0.1f)
        {
            controller.TriggerDetach();
            controller.Velocity = Vector2.down * 1.5f; // Leichter Initialimpuls
            stateMachine.ChangeState(controller.AirborneState);
            return;
        }

        // 2. Sprung von der jeweiligen Fläche
        if (controller.JumpTriggered)
        {
            controller.TriggerDetach();

            if (isCeiling)
            {
                // Kontrollierter Decken-Sprung: Moderater Schub nach unten + Schwung in A/D-Richtung
                float horizontalPush = controller.MoveInput.x * data.moveSpeed * 0.8f;
                float verticalDrop = -data.jumpVelocity * 0.4f; // ca. 40% der normalen Sprungkraft

                controller.Velocity = new Vector2(horizontalPush, verticalDrop);
            }
            else if (isWall)
            {
                // Wall-Jump: Weg von der Wand + Aufwärtsschub
                Vector2 jumpDir = (surfaceNormal + Vector2.up * 1.2f).normalized;
                controller.Velocity = jumpDir * data.jumpVelocity;
            }
            else
            {
                // Normaler Bodensprung entlang der Flächennormale
                controller.Velocity = surfaceNormal * data.jumpVelocity;
            }

            stateMachine.ChangeState(controller.AirborneState);
        }
    }

    public override void PhysicsUpdate()
    {
        Vector2 normal = controller.CurrentSurfaceNormal;
        bool isWall = Mathf.Abs(normal.x) > 0.5f;
        bool isCeiling = normal.y < -0.5f;

        float activeInput = 0f;

        if (isWall)
        {
            // Rechte Wand (normal.x < 0) vs. Linke Wand (normal.x > 0)
            // W klettert nach oben, S nach unten
            activeInput = (normal.x < 0f) ? controller.MoveInput.y : -controller.MoveInput.y;
        }
        else if (isCeiling)
        {
            // Vorzeichen an der Decke umkehren, damit D nach rechts und A nach links läuft
            activeInput = -controller.MoveInput.x;
        }
        else
        {
            // Normaler Boden
            activeInput = controller.MoveInput.x;
        }

        // Bewegung entlang der Flächen-Tangente
        float targetSpeed = activeInput * data.moveSpeed;
        Vector2 vel = controller.Velocity;

        float accel = (Mathf.Abs(targetSpeed) > 0.01f) ? data.groundAcceleration : data.groundDeceleration;
        vel.x = Mathf.MoveTowards(vel.x, targetSpeed, accel * Time.fixedDeltaTime);
        vel.y = 0f; // Festhalten an der Fläche

        controller.Velocity = vel;
    }
}