using QDND.Combat.Entities;

namespace QDND.Combat.Services
{
    public interface ICameraCoordinator
    {
        void CenterCameraOnCombatant(Combatant combatant);
        void SelectCombatant(string combatantId);
    }
}
