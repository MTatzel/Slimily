using UnityEngine;

public class PlatformerGroundedState : PlatformerSlimeState
{
    private bool justEntered;

    // --- Sticky Momentum & Intent-Buffer ---
    private float currentTraverseSign = 0f;
    private Vector2 initialInputVector = Vector2.zero;
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

        // Prüfen, ob S gerade noch vom Wand-Abstieg gehalten wird (verhindert versehentlichen Detach an der Ecke)
        bool isHoldingDownFromWall = isTraversing && initialInputVector.y < -0.5f;

        // 1. Dropdown an der Decke / Unterseite via Taste S
        // Nur auslösen, wenn S nicht als aktive Kletter-Taste von der Wand mitgebracht wurde!
        if (isCeiling && controller.MoveInput.y < -0.1f && !isHoldingDownFromWall)
        {
            controller.TriggerDetach(surfaceNormal);

            float airMoveX = controller.MoveInput.x * data.moveSpeed;
            controller.Velocity = new Vector2(airMoveX, -3f);

            stateMachine.ChangeState(controller.AirborneState);
            return;
        }

        // 2. Sprung von der Fläche (Boden, Wand oder Decke)
        if (controller.HasBufferedJump)
        {
            controller.ConsumeJumpBuffer();
            controller.TriggerDetach(surfaceNormal);

            if (isCeiling)
            {
                float airMoveX = controller.MoveInput.x * data.moveSpeed;
                float verticalDrop = -data.jumpVelocity * 0.6f;
                controller.Velocity = new Vector2(airMoveX, verticalDrop);
            }
            else if (isWall)
            {
                Vector2 jumpDir = (surfaceNormal * 1.0f + Vector2.up * 1.1f).normalized;
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
        Vector2 tangent = new Vector2(normal.y, -normal.x);
        Vector2 input = controller.MoveInput;
        float inputSqrMag = input.sqrMagnitude;

        float targetSpeed = 0f;

        if (inputSqrMag < 0.04f)
        {
            isTraversing = false;
            currentTraverseSign = 0f;
            initialInputVector = Vector2.zero;
        }
        else
        {
            Vector2 normInput = input.normalized;

            if (!isTraversing)
            {
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
                float reversalCheck = Vector2.Dot(normInput, initialInputVector);

                if (reversalCheck < -0.35f)
                {
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
                    float currentProj = Vector2.Dot(input, tangent);

                    if (currentProj * currentTraverseSign > 0.35f)
                    {
                        initialInputVector = normInput;
                    }
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