using System;
using System.Collections.Generic;
using QDND.Combat.Entities;

namespace QDND.Combat.Services
{
    public interface ICombatantRegistry
    {
        event Action<Combatant> CombatantAdded;
        event Action<Combatant> CombatantRemoved;
        event Action<IReadOnlyList<Combatant>> CombatantsReplaced;
        event Action Cleared;

        Combatant Get(string id);
        IReadOnlyList<Combatant> GetAll();
        void Add(Combatant combatant);
        bool Remove(string id);
        void ReplaceAll(IEnumerable<Combatant> combatants);
        void Clear();
    }
}
