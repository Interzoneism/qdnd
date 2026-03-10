using System;
using System.Collections.Generic;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.UI;

namespace QDND.Combat.Arena
{
    public interface ICombatController
    {
        ICombatContext Context { get; }
        ActionBarModel ActionBarModel { get; }
        TurnTrackerModel TurnTrackerModel { get; }
        ResourceBarModel ResourceBarModel { get; }
        string ActiveCombatantId { get; }
        string SelectedCombatantId { get; }
        string SelectedAbilityId { get; }
        bool IsPlayerTurn { get; }
        bool IsAutoBattleMode { get; }
        float DefaultMovePoints { get; }

        event Action<string> CombatantHoverChanged;
        event Action<Combatant, ActionDefinition> OnAIAbilityUsed;

        IEnumerable<Combatant> GetCombatants();
        ActionDefinition GetActionById(string actionId);
        List<ActionDefinition> GetActionsForCombatant(string combatantId);
        void SelectAction(string actionId, ActionExecutionOptions options = null);
        void EndCurrentTurn();
        void ReorderActionBarSlots(string combatantId, int fromSlot, int toSlot);
        CombatantVisual GetVisual(string combatantId);
    }
}
