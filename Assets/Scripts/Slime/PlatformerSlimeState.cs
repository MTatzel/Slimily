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

    /// <summary>
    /// Berechnet die horizontale Bewegung und erhält bestehenden Sprung-Schwung in Flugrichtung.
    /// </summary>
    protected void ApplyHorizontalMovement(float accel, float decel)
    {
        float inputX = controller.MoveInput.x;
        float targetSpeed = inputX * data.moveSpeed;
        Vector2 vel = controller.Velocity;

        float rate;
        if (Mathf.Abs(inputX) > 0.05f)
        {
            // Bewegt sich der Slime durch einen Wand-/Ecksprung bereits schneller in Eingaberichtung,
            // bremsen wir ihn nicht hart auf moveSpeed ab, sondern lassen den Schwung sanft auslaufen.
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