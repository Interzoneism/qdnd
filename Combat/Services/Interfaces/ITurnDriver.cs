using QDND.Combat.Entities;

namespace QDND.Combat.Services
{
    public interface ITurnDriver
    {
        void ExecuteAITurn(Combatant combatant);
        void ResumeDecisionStateIfExecuting(string reason);
    }
}
