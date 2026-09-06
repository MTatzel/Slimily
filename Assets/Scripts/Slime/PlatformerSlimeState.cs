using UnityEngine;

public abstract class PlatformerSlimeState
{
    protected PlatformerSlimeController controller;
    protected PlatformerStateMachine stateMachine;
    protected PlatformerPhysicsData data;

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

    // --- Zentrale Hilfsmethoden für alle abgeleiteten Zustände ---

    /// <summary>
    /// Berechnet die horizontale Bewegung basierend auf dem Input und der übergebenen Beschleunigung.
    /// </summary>
    protected void ApplyHorizontalMovement(float accel, float decel)
    {
        float targetSpeed = controller.MoveInput.x * data.moveSpeed;
        Vector2 vel = controller.Velocity;

        float rate = (Mathf.Abs(targetSpeed) > 0.01f) ? accel : decel;
        vel.x = Mathf.MoveTowards(vel.x, targetSpeed, rate * Time.fixedDeltaTime);

        controller.Velocity = vel;
    }

    /// <summary>
    /// Wendet die konfigurierte Schwerkraft bis zum Geschwindigkeitslimit an.
    /// </summary>
    protected void ApplyGravity()
    {
        Vector2 vel = controller.Velocity;
        vel.y += data.gravity * Time.fixedDeltaTime;
        vel.y = Mathf.Max(vel.y, -data.maxFallSpeed);
        controller.Velocity = vel;
    }
}