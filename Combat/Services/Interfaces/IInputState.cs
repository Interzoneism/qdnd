namespace QDND.Combat.Services
{
    public interface IInputState
    {
        bool IsPlayerTurn { get; }
        bool CanPlayerControl(string combatantId);
    }
}
