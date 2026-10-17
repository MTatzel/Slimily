using UnityEngine;

public class PlatformerGroundedState : PlatformerSlimeState
{
    private bool justEntered;

    // --- Sticky Momentum & Intent-Buffer ---
    private float currentTraverseSign = 0f;
    private Vector2 initialInputVector = Vector2.zero;
    private bool isTraversing = false;

    // --- Ceiling Detach Hold-Timer ---
    private float downHoldTimer = 0f;

    public PlatformerGroundedState(PlatformerSlimeController controller, PlatformerStateMachine stateMachine, PlatformerPhysicsData data) 
        : base(controller, stateMachine, data) { }

    public override void Enter()
    {
        justEntered = true;
        controller.IsGrounded = true;

        Vector2 normal = controller.CurrentSurfaceNormal;
        Vector2 tangent = new Vector2(normal.y, -normal.x);
        float preservedSpeed = Vector2.Dot(controller.Velocity, tangent);

        controller.Velocity = new Vector2(preservedSpeed, 0f);

        currentTraverseSign = 0f;
        initialInputVector = Vector2.zero;
        isTraversing = false;
        downHoldTimer = 0f;
    }

    public override void FrameUpdate()
    {
        if (justEntered)
        {
            justEntered = false;
        }

        if (!controller.IsGrounded)
        {
            stateMachine.ChangeState(controller.AirborneState);
            return;
        }

        Vector2 surfaceNormal = controller.CurrentSurfaceNormal;
        bool isCeiling = surfaceNormal.y < -0.5f;
        bool isWall = Mathf.Abs(surfaceNormal.x) > 0.5f;

        bool isHoldingDownFromWall = isTraversing && initialInputVector.y < -0.5f;

        // 1. Dropdown an der Decke via Hold-Timer mit striktem Winkelkegel
        if (isCeiling && !isHoldingDownFromWall)
        {
            Vector2 input = controller.MoveInput;

            // Muss fast rein nach unten sein (Y <= -0.80 und X maximal 0.25)
            bool isStrictlyDown = input.y <= -0.8f && Mathf.Abs(input.x) <= 0.25f;

            if (isStrictlyDown)
            {
                downHoldTimer += Time.deltaTime;

                float requiredDuration = (data != null && data.ceilingDetachHoldDuration > 0f) 
                    ? data.ceilingDetachHoldDuration 
                    : 0.25f;

                if (downHoldTimer >= requiredDuration)
                {
                    downHoldTimer = 0f;
                    controller.TriggerDetach(surfaceNormal);

                    float airMoveX = controller.MoveInput.x * data.moveSpeed;
                    controller.Velocity = new Vector2(airMoveX, -3f);

                    stateMachine.ChangeState(controller.AirborneState);
                    return;
                }
            }
            else
            {
                // Jede Abweichung setzt den Timer sofort zurück
                downHoldTimer = 0f;
            }
        }
        else
        {
            downHoldTimer = 0f;
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
                float moveX = Mathf.Abs(controller.MoveInput.x) > 0.1f 
                    ? controller.MoveInput.x * data.moveSpeed 
                    : controller.Velocity.x;

                controller.Velocity = new Vector2(moveX, data.jumpVelocity);
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