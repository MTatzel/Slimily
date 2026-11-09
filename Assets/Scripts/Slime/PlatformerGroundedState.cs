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
        Abilities.ResetAllCharges();

        Vector2 normal = controller.EffectiveSurfaceNormal;
        Vector2 tangent = controller.IsOnOuterCorner
            ? controller.OuterCornerDiagonalTangent
            : new Vector2(normal.y, -normal.x);

        float preservedSpeed = Vector2.Dot(controller.Velocity, tangent);

        if (controller.IsOnOuterCorner && Input.MoveInput.sqrMagnitude < 0.04f)
        {
            preservedSpeed = 0f;
        }

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

        // 1. Dash prüfen (Shift / R2 / Viereck)
        if (Input.HasBufferedDash && Abilities.CanUseDash(isGrounded: true))
        {
            stateMachine.ChangeState(controller.DashState);
            return;
        }

        // Sprint bleibt aktiv, solange die Dash-Taste gehalten wird
        if (!Input.DashHeld || !Abilities.enableSprint)
        {
            Abilities.IsSprinting = false;
        }
        else if (Abilities.enableSprint && !Abilities.enableDash && Input.DashHeld)
        {
            // Falls Dash deaktiviert ist, aber Sprint erlaubt ist, kann man direkt per Halten sprinten
            Abilities.IsSprinting = true;
        }

        Vector2 surfaceNormal = controller.EffectiveSurfaceNormal;
        bool isOuterCorner = controller.IsOnOuterCorner;
        bool isCeiling = !isOuterCorner && surfaceNormal.y < -0.5f;
        bool isWall = !isOuterCorner && Mathf.Abs(surfaceNormal.x) > 0.5f;

        bool isHoldingDownFromWall = isTraversing && initialInputVector.y < -0.5f;

        // 2. Dropdown an der Decke via Hold-Timer
        if (isCeiling && !isHoldingDownFromWall)
        {
            Vector2 input = Input.MoveInput;
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

                    float airMoveX = Input.MoveInput.x * data.moveSpeed;
                    controller.Velocity = new Vector2(airMoveX, -3f);

                    stateMachine.ChangeState(controller.AirborneState);
                    return;
                }
            }
            else
            {
                downHoldTimer = 0f;
            }
        }
        else
        {
            downHoldTimer = 0f;
        }

        // 3. Sprung von der Fläche
        if (controller.HasBufferedJump)
        {
            ExecuteJump(surfaceNormal, isOuterCorner, isCeiling, isWall);
        }
    }

    private void ExecuteJump(Vector2 surfaceNormal, bool isOuterCorner, bool isCeiling, bool isWall)
    {
        controller.ConsumeJumpBuffer();

        float stretch = (data != null) ? data.jumpStretchAmount : 0.16f;
        Visuals.ApplySquashAndStretch(1f - stretch * 0.7f, 1f + stretch);

        float sprintMult = (Abilities.IsSprinting && data != null && data.sprintSpeedMultiplier > 1f)
            ? data.sprintSpeedMultiplier
            : 1f;
        float currentMoveSpeed = data.moveSpeed * sprintMult;

        // FALL A: Absprung direkt von einer äußeren Ecke (270°)
        if (isOuterCorner)
        {
            Vector2 cornerNormal = controller.OuterCornerDiagonalNormal;
            controller.TriggerCornerDetach(controller.OuterCornerNormalA, controller.OuterCornerNormalB);

            Vector2 launchDir = (cornerNormal * 1.0f + Vector2.up * 0.55f).normalized;
            if (Input.MoveInput.sqrMagnitude > 0.1f && Vector2.Dot(Input.MoveInput.normalized, cornerNormal) > -0.25f)
            {
                launchDir = (launchDir + Input.MoveInput.normalized * 0.45f).normalized;
            }

            controller.Velocity = launchDir * data.jumpVelocity;
            stateMachine.ChangeState(controller.AirborneState);
            return;
        }

        // FALL B: Absprung direkt aus einer 90°-Innenecken-Nische
        if (controller.IsInInnerCornerPocket && controller.InnerCornerAdjacentNormal.sqrMagnitude > 0.1f)
        {
            Vector2 bisector = (surfaceNormal + controller.InnerCornerAdjacentNormal).normalized;
            controller.TriggerCornerDetach(surfaceNormal, controller.InnerCornerAdjacentNormal);

            Vector2 launchDir = (bisector + Vector2.up * 0.4f).normalized;
            controller.Velocity = launchDir * data.jumpVelocity;
            stateMachine.ChangeState(controller.AirborneState);
            return;
        }

        // FALL C: Regulärer Absprung (Decke, Wand, Boden)
        controller.TriggerDetach(surfaceNormal);

        if (isCeiling)
        {
            float airMoveX = Mathf.Abs(Input.MoveInput.x) > 0.1f
                ? Input.MoveInput.x * currentMoveSpeed
                : controller.Velocity.x;
            float verticalDrop = -data.jumpVelocity * 0.55f;
            controller.Velocity = new Vector2(airMoveX, verticalDrop);
        }
        else if (isWall)
        {
            float awayInput = Vector2.Dot(Input.MoveInput, surfaceNormal);
            float horizontalWeight = awayInput > 0.25f ? 0.95f : 0.72f;
            float verticalWeight = Input.MoveInput.y < -0.4f ? 0.55f : 1.15f;

            Vector2 jumpDir = (surfaceNormal * horizontalWeight + Vector2.up * verticalWeight).normalized;
            controller.Velocity = jumpDir * data.jumpVelocity;
        }
        else
        {
            float currentTangentSpeed = controller.Velocity.x * surfaceNormal.y;
            float moveX = Mathf.Abs(Input.MoveInput.x) > 0.1f 
                ? Input.MoveInput.x * currentMoveSpeed 
                : currentTangentSpeed;

            controller.Velocity = new Vector2(moveX, data.jumpVelocity);
        }

        stateMachine.ChangeState(controller.AirborneState);
    }

    public override void PhysicsUpdate()
    {
        Vector2 normal = controller.EffectiveSurfaceNormal;
        Vector2 tangent = controller.IsOnOuterCorner
            ? controller.OuterCornerDiagonalTangent
            : new Vector2(normal.y, -normal.x);

        Vector2 input = Input.MoveInput;
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

                if (Mathf.Abs(proj) > 0.10f)
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
                    if (Mathf.Abs(proj) > 0.10f)
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
                float sprintMult = (Abilities.IsSprinting && data != null && data.sprintSpeedMultiplier > 1f)
                    ? data.sprintSpeedMultiplier
                    : 1f;

                targetSpeed = currentTraverseSign * data.moveSpeed * sprintMult * controller.CornerSpeedMultiplier;
            }
        }

        Abilities.UpdateFacingDirection(input, tangent, currentTraverseSign);

        Vector2 vel = controller.Velocity;
        float accel = (Mathf.Abs(targetSpeed) > 0.01f) ? data.groundAcceleration : data.groundDeceleration;
        vel.x = Mathf.MoveTowards(vel.x, targetSpeed, accel * Time.fixedDeltaTime);
        vel.y = 0f;

        controller.Velocity = vel;
    }
}