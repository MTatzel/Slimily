public class PlatformerStateMachine
{
    public PlatformerSlimeState CurrentState { get; private set; }

    public void Initialize(PlatformerSlimeState startingState)
    {
        CurrentState = startingState;
        CurrentState.Enter();
    }

    public void ChangeState(PlatformerSlimeState newState)
    {
        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();
    }

    public void Update()
    {
        CurrentState?.FrameUpdate();
    }

    public void FixedUpdate()
    {
        CurrentState?.PhysicsUpdate();
    }
}