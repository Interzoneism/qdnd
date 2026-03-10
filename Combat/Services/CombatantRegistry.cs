using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;

namespace QDND.Combat.Services
{
    public class CombatantRegistry : ICombatantRegistry
    {
        private readonly List<Combatant> _orderedCombatants = new();
        private readonly Dictionary<string, Combatant> _combatantsById = new(StringComparer.OrdinalIgnoreCase);

        public event Action<Combatant> CombatantAdded;
        public event Action<Combatant> CombatantRemoved;
        public event Action<IReadOnlyList<Combatant>> CombatantsReplaced;
        public event Action Cleared;

        public Combatant Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return _combatantsById.TryGetValue(id, out var combatant) ? combatant : null;
        }

        public IReadOnlyList<Combatant> GetAll() => _orderedCombatants;

        public void Add(Combatant combatant)
        {
            if (combatant == null || string.IsNullOrWhiteSpace(combatant.Id))
            {
                return;
            }

            if (_combatantsById.TryGetValue(combatant.Id, out var existing))
            {
                int index = _orderedCombatants.IndexOf(existing);
                if (index >= 0)
                {
                    _orderedCombatants[index] = combatant;
                }

                _combatantsById[combatant.Id] = combatant;
            }
            else
            {
                _orderedCombatants.Add(combatant);
                _combatantsById[combatant.Id] = combatant;
            }

            CombatantAdded?.Invoke(combatant);
        }

        public bool Remove(string id)
        {
            if (!_combatantsById.TryGetValue(id, out var combatant))
            {
                return false;
            }

            _combatantsById.Remove(id);
            _orderedCombatants.Remove(combatant);
            CombatantRemoved?.Invoke(combatant);
            return true;
        }

        public void ReplaceAll(IEnumerable<Combatant> combatants)
        {
            _orderedCombatants.Clear();
            _combatantsById.Clear();

            if (combatants != null)
            {
                foreach (var combatant in combatants.Where(c => c != null && !string.IsNullOrWhiteSpace(c.Id)))
                {
                    _orderedCombatants.Add(combatant);
                    _combatantsById[combatant.Id] = combatant;
                }
            }

            CombatantsReplaced?.Invoke(_orderedCombatants);
        }

        public void Clear()
        {
            if (_orderedCombatants.Count == 0 && _combatantsById.Count == 0)
            {
                return;
            }

            _orderedCombatants.Clear();
            _combatantsById.Clear();
            Cleared?.Invoke();
        }
    }
}
