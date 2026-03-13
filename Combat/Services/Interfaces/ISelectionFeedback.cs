using QDND.Combat.Entities;

namespace QDND.Combat.Services
{
    public interface ISelectionFeedback
    {
        void ClearSelection();
        void RefreshActionBarUsability(string combatantId);
        void UpdateResourceModel(Combatant combatant);
    }
}
