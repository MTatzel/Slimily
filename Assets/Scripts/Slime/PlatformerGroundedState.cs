using UnityEngine;

public class PlatformerGroundedState : PlatformerSlimeState
{
    private bool justEntered;

    // --- Sticky Momentum & Intent-Buffer ---
    private float currentTraverseSign = 0f;      // +1 = CCW, -1 = CW, 0 = Stillstand
    private Vector2 initialInputVector = Vector2.zero; // Richtung, mit der die Bewegung gestartet wurde
    private bool isTraversing = false;

    public PlatformerGroundedState(PlatformerSlimeController controller, PlatformerStateMachine stateMachine, PlatformerPhysicsData data) 
        : base(controller, stateMachine, data) { }

    public override void Enter()
    {
        justEntered = true;
        controller.IsGrounded = true;
        controller.Velocity = Vector2.zero;

        currentTraverseSign = 0f;
        initialInputVector = Vector2.zero;
        isTraversing = false;
    }

    public override void FrameUpdate()
    {
        if (justEntered)
        {
            justEntered = false;
            return;
        }

        if (!controller.IsGrounded)
        {
            stateMachine.ChangeState(controller.AirborneState);
            return;
        }

        Vector2 surfaceNormal = controller.CurrentSurfaceNormal;
        bool isCeiling = surfaceNormal.y < -0.5f;
        bool isWall = Mathf.Abs(surfaceNormal.x) > 0.5f;

        // 1. Dropdown an der Decke via Taste S
        if (isCeiling && controller.MoveInput.y < -0.1f)
        {
            controller.TriggerDetach();

            float airMoveX = controller.MoveInput.x * data.moveSpeed;
            controller.Velocity = new Vector2(airMoveX, -2f);

            stateMachine.ChangeState(controller.AirborneState);
            return;
        }

        // 2. Sprung von der Fläche (entweder frisch gedrückt oder gepuffert)
        if (controller.HasBufferedJump)
        {
            controller.ConsumeJumpBuffer();
            controller.TriggerDetach();

            if (isCeiling)
            {
                float airMoveX = controller.MoveInput.x * data.moveSpeed;
                float verticalDrop = -data.jumpVelocity * 0.5f;
                controller.Velocity = new Vector2(airMoveX, verticalDrop);
            }
            else if (isWall)
            {
                Vector2 jumpDir = (surfaceNormal + Vector2.up * 1.2f).normalized;
                controller.Velocity = jumpDir * data.jumpVelocity;
            }
            else
            {
                float currentMoveX = controller.Velocity.x;
                controller.Velocity = new Vector2(currentMoveX, data.jumpVelocity);
            }

            stateMachine.ChangeState(controller.AirborneState);
        }
    }

    public override void PhysicsUpdate()
    {
        Vector2 normal = controller.CurrentSurfaceNormal;
        Vector2 tangent = new Vector2(normal.y, -normal.x); // Zyklische Tangente
        Vector2 input = controller.MoveInput;
        float inputSqrMag = input.sqrMagnitude;

        float targetSpeed = 0f;

        // Fall A: Keine Eingabe (Taste losgelassen / Stick in Neutralstellung)
        if (inputSqrMag < 0.04f)
        {
            isTraversing = false;
            currentTraverseSign = 0f;
            initialInputVector = Vector2.zero;
        }
        // Fall B: Eingabe aktiv
        else
        {
            Vector2 normInput = input.normalized;

            if (!isTraversing)
            {
                // Frische Bewegung auf dieser Fläche starten
                float proj = Vector2.Dot(input, tangent);

                if (Mathf.Abs(proj) > 0.15f)
                {
                    isTraversing = true;
                    currentTraverseSign = Mathf.Sign(proj);
                    initialInputVector = normInput;
                }
            }
            else
            {
                // Prüfen, ob der Spieler aktiv in die Gegenrichtung steuert (Input umgedreht)
                float reversalCheck = Vector2.Dot(normInput, initialInputVector);

                if (reversalCheck < -0.35f)
                {
                    // Spieler hat aktiv umgekehrt (z. B. von A auf D gewechselt oder Stick zurückgezogen)
                    isTraversing = false;
                    currentTraverseSign = 0f;
                    initialInputVector = Vector2.zero;

                    float proj = Vector2.Dot(input, tangent);
                    if (Mathf.Abs(proj) > 0.15f)
                    {
                        isTraversing = true;
                        currentTraverseSign = Mathf.Sign(proj);
                        initialInputVector = normInput;
                    }
                }
                else
                {
                    // Spieler hält die ursprüngliche Taste ODER dreht den Analogstick flüssig mit
                    float currentProj = Vector2.Dot(input, tangent);

                    // Falls der Stick sich aktiv an der neuen Fläche orientiert, Richtungsreferenz nachziehen
                    if (currentProj * currentTraverseSign > 0.35f)
                    {
                        initialInputVector = normInput;
                    }

                    // Sticky Momentum bleibt aktiv: currentTraverseSign wird beibehalten!
                }
            }

            if (isTraversing)
            {
                targetSpeed = currentTraverseSign * data.moveSpeed;
            }
        }

        Vector2 vel = controller.Velocity;
        float accel = (Mathf.Abs(targetSpeed) > 0.01f) ? data.groundAcceleration : data.groundDeceleration;
        vel.x = Mathf.MoveTowards(vel.x, targetSpeed, accel * Time.fixedDeltaTime);
        vel.y = 0f;

        controller.Velocity = vel;
    }
}