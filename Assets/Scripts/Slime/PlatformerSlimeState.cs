using UnityEngine;

public abstract class PlatformerSlimeState
{
    protected PlatformerSlimeController controller;
    protected PlatformerStateMachine stateMachine;
    protected PlatformerPhysicsData data;

    protected SlimeInputHandler Input => controller.InputHandler;
    protected SlimeAbilities Abilities => controller.Abilities;
    protected SlimeVisuals Visuals => controller.Visuals;

    public PlatformerSlimeState(PlatformerSlimeController controller, PlatformerStateMachine stateMachine, PlatformerPhysicsData data)
    {
        this.controller = controller;
        this.stateMachine = stateMachine;
        this.data = data;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void FrameUpdate() { }
    public virtual void PhysicsUpdate() { }

    /// <summary>
    /// Berechnet die horizontale Bewegung und erhält bestehenden Sprung-/Sprint-Schwung in Flugrichtung.
    /// </summary>
    protected void ApplyHorizontalMovement(float accel, float decel)
    {
        float inputX = Input.MoveInput.x;
        float speedMult = (Abilities != null && Abilities.IsSprinting && data != null && data.sprintSpeedMultiplier > 1f)
            ? data.sprintSpeedMultiplier
            : 1f;

        float targetSpeed = inputX * data.moveSpeed * speedMult;
        Vector2 vel = controller.Velocity;

        float rate;
        if (Mathf.Abs(inputX) > 0.05f)
        {
            bool movingFasterInSameDir = (vel.x * inputX > 0f) && (Mathf.Abs(vel.x) > Mathf.Abs(targetSpeed));
            rate = movingFasterInSameDir ? decel : accel;
        }
        else
        {
            rate = decel;
        }

        vel.x = Mathf.MoveTowards(vel.x, targetSpeed, rate * Time.fixedDeltaTime);
        controller.Velocity = vel;
    }

    protected void ApplyGravity()
    {
        Vector2 vel = controller.Velocity;
        vel.y += data.gravity * Time.fixedDeltaTime;
        vel.y = Mathf.Max(vel.y, -data.maxFallSpeed);
        controller.Velocity = vel;
    }
}