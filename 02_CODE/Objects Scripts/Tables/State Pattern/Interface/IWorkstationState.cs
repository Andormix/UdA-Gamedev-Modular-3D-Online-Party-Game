public interface IWorkstationState
{
    WorkstationPhaseId Id { get; }

    void Enter();
    void Exit();

    void Tick(float dt);
    void Interact(Player player);
}