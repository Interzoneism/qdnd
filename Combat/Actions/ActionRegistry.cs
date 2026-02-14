using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Combat.Actions
{
    /// <summary>
    /// Centralized registry for managing all available action definitions in the game.
    /// Acts as a singleton service that stores and provides access to all actions,
    /// including BG3 spells, class abilities, weapon attacks, and custom actions.
    /// </summary>
    public class ActionRegistry
    {
        private readonly Dictionary<string, ActionDefinition> _actions = new();
        private readonly Dictionary<string, List<string>> _tagIndex = new();
        private readonly Dictionary<int, List<string>> _spellLevelIndex = new();
        private readonly Dictionary<SpellSchool, List<string>> _schoolIndex = new();
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        /// <summary>
        /// Total number of registered actions.
        /// </summary>
        public int Count => _actions.Count;

        /// <summary>
        /// Errors encountered during action registration.
        /// </summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>
        /// Warnings encountered during action registration.
        /// </summary>
        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>
        /// Register a new action definition.
        /// </summary>
        /// <param name="action">The action definition to register.</param>
        /// <param name="overwrite">If true, overwrites existing action with same ID.</param>
        /// <returns>True if registration succeeded, false if action already exists and overwrite is false.</returns>
        public bool RegisterAction(ActionDefinition action)
        {
            return RegisterAction(action, overwrite: false);
        }

        /// <summary>
        /// Register a new action definition with overwrite option.
        /// </summary>
        /// <param name="action">The action definition to register.</param>
        /// <param name="overwrite">If true, overwrites existing action with same ID.</param>
        /// <returns>True if registration succeeded, false if action already exists and overwrite is false.</returns>
        public bool RegisterAction(ActionDefinition action, bool overwrite)
        {
            if (action == null)
            {
                _errors.Add("Cannot register null action");
                return false;
            }

            if (string.IsNullOrEmpty(action.Id))
            {
                _errors.Add($"Cannot register action with null/empty ID: {action.Name ?? "Unknown"}");
                return false;
            }

            // Check if already exists
            if (_actions.ContainsKey(action.Id) && !overwrite)
            {
                _warnings.Add($"Action '{action.Id}' already registered (use overwrite=true to replace)");
                return false;
            }

            // Unindex old action if replacing
            if (_actions.ContainsKey(action.Id))
            {
                UnindexAction(_actions[action.Id]);
            }

            // Store action
            _actions[action.Id] = action;

            // Index by tags
            if (action.Tags != null)
            {
                foreach (var tag in action.Tags)
                {
                    if (!_tagIndex.ContainsKey(tag))
                        _tagIndex[tag] = new List<string>();
                    _tagIndex[tag].Add(action.Id);
                }
            }

            // Index by spell level
            if (action.SpellLevel >= 0)
            {
                if (!_spellLevelIndex.ContainsKey(action.SpellLevel))
                    _spellLevelIndex[action.SpellLevel] = new List<string>();
                _spellLevelIndex[action.SpellLevel].Add(action.Id);
            }

            // Index by school
            if (action.School != SpellSchool.None)
            {
                if (!_schoolIndex.ContainsKey(action.School))
                    _schoolIndex[action.School] = new List<string>();
                _schoolIndex[action.School].Add(action.Id);
            }

            return true;
        }

        /// <summary>
        /// Unindex an action from all indices (used when replacing).
        /// </summary>
        private void UnindexAction(ActionDefinition action)
        {
            if (action.Tags != null)
            {
                foreach (var tag in action.Tags)
                {
                    if (_tagIndex.ContainsKey(tag))
                    {
                        _tagIndex[tag].Remove(action.Id);
                        if (_tagIndex[tag].Count == 0)
                            _tagIndex.Remove(tag);
                    }
                }
            }

            if (_spellLevelIndex.ContainsKey(action.SpellLevel))
            {
                _spellLevelIndex[action.SpellLevel].Remove(action.Id);
                if (_spellLevelIndex[action.SpellLevel].Count == 0)
                    _spellLevelIndex.Remove(action.SpellLevel);
            }

            if (action.School != SpellSchool.None && _schoolIndex.ContainsKey(action.School))
            {
                _schoolIndex[action.School].Remove(action.Id);
                if (_schoolIndex[action.School].Count == 0)
                    _schoolIndex.Remove(action.School);
            }
        }

        /// <summary>
        /// Get an action by ID.
        /// </summary>
        /// <param name="actionId">The action ID to retrieve.</param>
        /// <returns>The action definition, or null if not found.</returns>
        public ActionDefinition GetAction(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
                return null;

            return _actions.TryGetValue(actionId, out var action) ? action : null;
        }

        /// <summary>
        /// Check if an action is registered.
        /// </summary>
        /// <param name="actionId">The action ID to check.</param>
        /// <returns>True if the action exists in the registry.</returns>
        public bool HasAction(string actionId)
        {
            return !string.IsNullOrEmpty(actionId) && _actions.ContainsKey(actionId);
        }

        /// <summary>
        /// Get all registered actions.
        /// </summary>
        /// <returns>Read-only collection of all action definitions.</returns>
        public IReadOnlyCollection<ActionDefinition> GetAllActions()
        {
            return _actions.Values.ToList();
        }

        /// <summary>
        /// Get all action IDs.
        /// </summary>
        /// <returns>Collection of all registered action IDs.</returns>
        public IReadOnlyCollection<string> GetAllActionIds()
        {
            return _actions.Keys.ToList();
        }

        /// <summary>
        /// Get actions by tag (e.g., "weapon_attack", "cantrip", "healing").
        /// </summary>
        /// <param name="tag">The tag to filter by.</param>
        /// <returns>List of matching actions.</returns>
        public List<ActionDefinition> GetActionsByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return new List<ActionDefinition>();

            if (!_tagIndex.TryGetValue(tag, out var actionIds))
                return new List<ActionDefinition>();

            return actionIds.Select(id => _actions[id]).ToList();
        }

        /// <summary>
        /// Get actions that have ALL specified tags.
        /// </summary>
        /// <param name="tags">Tags that must all be present.</param>
        /// <returns>List of matching actions.</returns>
        public List<ActionDefinition> GetActionsByAllTags(params string[] tags)
        {
            if (tags == null || tags.Length == 0)
                return new List<ActionDefinition>();

            return _actions.Values
                .Where(a => tags.All(tag => a.Tags != null && a.Tags.Contains(tag)))
                .ToList();
        }

        /// <summary>
        /// Get actions that have ANY of the specified tags.
        /// </summary>
        /// <param name="tags">Tags to match (OR logic).</param>
        /// <returns>List of matching actions.</returns>
        public List<ActionDefinition> GetActionsByAnyTag(params string[] tags)
        {
            if (tags == null || tags.Length == 0)
                return new List<ActionDefinition>();

            var matchedIds = new HashSet<string>();
            foreach (var tag in tags)
            {
                if (_tagIndex.TryGetValue(tag, out var actionIds))
                {
                    foreach (var id in actionIds)
                        matchedIds.Add(id);
                }
            }

            return matchedIds.Select(id => _actions[id]).ToList();
        }

        /// <summary>
        /// Get actions by spell level.
        /// </summary>
        /// <param name="level">Spell level (0 for cantrips, 1-9 for leveled spells).</param>
        /// <returns>List of matching actions.</returns>
        public List<ActionDefinition> GetActionsBySpellLevel(int level)
        {
            if (!_spellLevelIndex.TryGetValue(level, out var actionIds))
                return new List<ActionDefinition>();

            return actionIds.Select(id => _actions[id]).ToList();
        }

        /// <summary>
        /// Get all cantrips (level 0 spells).
        /// </summary>
        /// <returns>List of cantrip actions.</returns>
        public List<ActionDefinition> GetCantrips()
        {
            return GetActionsBySpellLevel(0);
        }

        /// <summary>
        /// Get actions by spell school.
        /// </summary>
        /// <param name="school">The school of magic.</param>
        /// <returns>List of matching actions.</returns>
        public List<ActionDefinition> GetActionsBySchool(SpellSchool school)
        {
            if (!_schoolIndex.TryGetValue(school, out var actionIds))
                return new List<ActionDefinition>();

            return actionIds.Select(id => _actions[id]).ToList();
        }

        /// <summary>
        /// Get actions by verbal intent (e.g., damage, healing, buff).
        /// </summary>
        /// <param name="intent">The intent to filter by.</param>
        /// <returns>List of matching actions.</returns>
        public List<ActionDefinition> GetActionsByIntent(VerbalIntent intent)
        {
            return _actions.Values
                .Where(a => a.Intent == intent)
                .ToList();
        }

        /// <summary>
        /// Get actions by casting time.
        /// </summary>
        /// <param name="castingTime">The casting time type.</param>
        /// <returns>List of matching actions.</returns>
        public List<ActionDefinition> GetActionsByCastingTime(CastingTimeType castingTime)
        {
            return _actions.Values
                .Where(a => a.CastingTime == castingTime)
                .ToList();
        }

        /// <summary>
        /// Get all damage-dealing spells/actions.
        /// </summary>
        /// <returns>List of damage actions.</returns>
        public List<ActionDefinition> GetDamageActions()
        {
            return _actions.Values
                .Where(a => a.Effects != null && a.Effects.Any(e => e.Type == "damage"))
                .ToList();
        }

        /// <summary>
        /// Get all healing spells/actions.
        /// </summary>
        /// <returns>List of healing actions.</returns>
        public List<ActionDefinition> GetHealingActions()
        {
            return _actions.Values
                .Where(a => a.Effects != null && a.Effects.Any(e => e.Type == "heal"))
                .ToList();
        }

        /// <summary>
        /// Get actions that require concentration.
        /// </summary>
        /// <returns>List of concentration actions.</returns>
        public List<ActionDefinition> GetConcentrationActions()
        {
            return _actions.Values
                .Where(a => a.RequiresConcentration)
                .ToList();
        }

        /// <summary>
        /// Get actions that can be upcast.
        /// </summary>
        /// <returns>List of upcastable actions.</returns>
        public List<ActionDefinition> GetUpcastableActions()
        {
            return _actions.Values
                .Where(a => a.CanUpcast)
                .ToList();
        }

        /// <summary>
        /// Query actions with custom filter.
        /// </summary>
        /// <param name="predicate">Filter predicate.</param>
        /// <returns>List of matching actions.</returns>
        public List<ActionDefinition> Query(Func<ActionDefinition, bool> predicate)
        {
            if (predicate == null)
                return new List<ActionDefinition>();

            return _actions.Values.Where(predicate).ToList();
        }

        /// <summary>
        /// Clear all registered actions and indices.
        /// </summary>
        public void Clear()
        {
            _actions.Clear();
            _tagIndex.Clear();
            _spellLevelIndex.Clear();
            _schoolIndex.Clear();
            _errors.Clear();
            _warnings.Clear();
        }

        /// <summary>
        /// Get statistics about registered actions.
        /// </summary>
        /// <returns>Dictionary of statistic name to count.</returns>
        public Dictionary<string, int> GetStatistics()
        {
            var stats = new Dictionary<string, int>
            {
                ["total"] = _actions.Count,
                ["cantrips"] = GetCantrips().Count,
                ["level_1_spells"] = GetActionsBySpellLevel(1).Count,
                ["level_2_spells"] = GetActionsBySpellLevel(2).Count,
                ["level_3_spells"] = GetActionsBySpellLevel(3).Count,
                ["level_4_spells"] = GetActionsBySpellLevel(4).Count,
                ["level_5_spells"] = GetActionsBySpellLevel(5).Count,
                ["level_6_spells"] = GetActionsBySpellLevel(6).Count,
                ["level_7_spells"] = GetActionsBySpellLevel(7).Count,
                ["level_8_spells"] = GetActionsBySpellLevel(8).Count,
                ["level_9_spells"] = GetActionsBySpellLevel(9).Count,
                ["damage_actions"] = GetDamageActions().Count,
                ["healing_actions"] = GetHealingActions().Count,
                ["concentration_actions"] = GetConcentrationActions().Count,
                ["upcastable_actions"] = GetUpcastableActions().Count,
                ["reactions"] = GetActionsByCastingTime(CastingTimeType.Reaction).Count,
                ["bonus_actions"] = GetActionsByCastingTime(CastingTimeType.BonusAction).Count
            };

            // Add school counts
            foreach (SpellSchool school in Enum.GetValues(typeof(SpellSchool)))
            {
                if (school != SpellSchool.None)
                {
                    stats[$"{school.ToString().ToLowerInvariant()}_spells"] = GetActionsBySchool(school).Count;
                }
            }

            return stats;
        }

        /// <summary>
        /// Get a formatted statistics report.
        /// </summary>
        /// <returns>Multi-line statistics string.</returns>
        public string GetStatisticsReport()
        {
            var stats = GetStatistics();
            var lines = new List<string>
            {
                "=== Action Registry Statistics ===",
                $"Total Actions: {stats["total"]}",
                "",
                "By Spell Level:",
                $"  Cantrips (0): {stats["cantrips"]}",
                $"  Level 1: {stats["level_1_spells"]}",
                $"  Level 2: {stats["level_2_spells"]}",
                $"  Level 3: {stats["level_3_spells"]}",
                $"  Level 4: {stats["level_4_spells"]}",
                $"  Level 5: {stats["level_5_spells"]}",
                $"  Level 6: {stats["level_6_spells"]}",
                $"  Level 7: {stats["level_7_spells"]}",
                $"  Level 8: {stats["level_8_spells"]}",
                $"  Level 9: {stats["level_9_spells"]}",
                "",
                "By Type:",
                $"  Damage: {stats["damage_actions"]}",
                $"  Healing: {stats["healing_actions"]}",
                $"  Concentration: {stats["concentration_actions"]}",
                $"  Upcastable: {stats["upcastable_actions"]}",
                $"  Reactions: {stats["reactions"]}",
                $"  Bonus Actions: {stats["bonus_actions"]}",
                ""
            };

            // Add top tags
            var topTags = _tagIndex
                .OrderByDescending(kvp => kvp.Value.Count)
                .Take(10)
                .ToList();

            if (topTags.Count > 0)
            {
                lines.Add("Top Tags:");
                foreach (var (tag, ids) in topTags)
                {
                    lines.Add($"  {tag}: {ids.Count}");
                }
            }

            return string.Join("\n", lines);
        }
    }
}
