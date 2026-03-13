using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Data;

namespace QDND.Combat.Actions
{
    /// <summary>
    /// Centralized registry for managing all available action definitions in the game.
    /// Acts as a singleton service that stores and provides access to all actions,
    /// including BG3 spells, class abilities, weapon attacks, and custom actions.
    /// </summary>
    public class ActionRegistry : Registry<ActionDefinition>
    {
        private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _tagIndex = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, List<string>> _spellLevelIndex = new();
        private readonly Dictionary<SpellSchool, List<string>> _schoolIndex = new();

        protected override string GetId(ActionDefinition item) => item.Id;

        protected override void AfterRegister(ActionDefinition action)
        {
            IndexAction(action);
        }

        protected override void AfterUnregister(ActionDefinition action)
        {
            UnindexAction(action);
        }

        protected override void OnClear()
        {
            _aliases.Clear();
            _tagIndex.Clear();
            _spellLevelIndex.Clear();
            _schoolIndex.Clear();
        }

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
                AddError("Cannot register null action");
                return false;
            }

            if (string.IsNullOrEmpty(action.Id))
            {
                AddError($"Cannot register action with null/empty ID: {action.Name ?? "Unknown"}");
                return false;
            }

            return BaseRegister(action, overwrite, "action");
        }

        private void IndexAction(ActionDefinition action)
        {
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
        /// Register an alias ID that resolves to a canonical action ID.
        /// Uses TryAdd semantics — first alias for a given key wins.
        /// </summary>
        public void RegisterAlias(string aliasId, string canonicalId)
        {
            if (!string.IsNullOrEmpty(aliasId) && !string.IsNullOrEmpty(canonicalId))
                _aliases.TryAdd(aliasId, canonicalId);
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

            var action = Get(actionId);
            if (action != null)
                return action;

            // Fallback: check aliases
            if (_aliases.TryGetValue(actionId, out var canonicalId))
                return Get(canonicalId);

            return null;
        }

        /// <summary>
        /// Check if an action is registered.
        /// </summary>
        /// <param name="actionId">The action ID to check.</param>
        /// <returns>True if the action exists in the registry.</returns>
        public bool HasAction(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return false;
             return Has(actionId) ||
                 (_aliases.TryGetValue(actionId, out var canonicalId) && Has(canonicalId));
        }

        /// <summary>
        /// Get all registered actions.
        /// </summary>
        /// <returns>Read-only collection of all action definitions.</returns>
        public IReadOnlyCollection<ActionDefinition> GetAllActions()
        {
            return GetAll();
        }

        /// <summary>
        /// Get all action IDs (canonical IDs plus all registered alias IDs).
        /// </summary>
        /// <returns>Collection of all registered action IDs and aliases.</returns>
        public IReadOnlyCollection<string> GetAllActionIds()
        {
            var ids = new HashSet<string>(Items.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var alias in _aliases.Keys)
                ids.Add(alias);
            return ids.ToList();
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

            return actionIds.Select(id => Items[id]).ToList();
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

            return Items.Values
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

            return matchedIds.Select(id => Items[id]).ToList();
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

            return actionIds.Select(id => Items[id]).ToList();
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

            return actionIds.Select(id => Items[id]).ToList();
        }

        /// <summary>
        /// Get actions by verbal intent (e.g., damage, healing, buff).
        /// </summary>
        /// <param name="intent">The intent to filter by.</param>
        /// <returns>List of matching actions.</returns>
        public List<ActionDefinition> GetActionsByIntent(VerbalIntent intent)
        {
            return Items.Values
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
            return Items.Values
                .Where(a => a.CastingTime == castingTime)
                .ToList();
        }

        /// <summary>
        /// Get all damage-dealing spells/actions.
        /// </summary>
        /// <returns>List of damage actions.</returns>
        public List<ActionDefinition> GetDamageActions()
        {
            return Items.Values
                .Where(a => a.Effects != null && a.Effects.Any(e => e.Type == "damage"))
                .ToList();
        }

        /// <summary>
        /// Get all healing spells/actions.
        /// </summary>
        /// <returns>List of healing actions.</returns>
        public List<ActionDefinition> GetHealingActions()
        {
            return Items.Values
                .Where(a => a.Effects != null && a.Effects.Any(e => e.Type == "heal"))
                .ToList();
        }

        /// <summary>
        /// Get actions that require concentration.
        /// </summary>
        /// <returns>List of concentration actions.</returns>
        public List<ActionDefinition> GetConcentrationActions()
        {
            return Items.Values
                .Where(a => a.RequiresConcentration)
                .ToList();
        }

        /// <summary>
        /// Get actions that can be upcast.
        /// </summary>
        /// <returns>List of upcastable actions.</returns>
        public List<ActionDefinition> GetUpcastableActions()
        {
            return Items.Values
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

            return Items.Values.Where(predicate).ToList();
        }

        /// <summary>
        /// Get statistics about registered actions.
        /// </summary>
        /// <returns>Dictionary of statistic name to count.</returns>
        public Dictionary<string, int> GetStatistics()
        {
            var stats = new Dictionary<string, int>
            {
                ["total"] = Count,
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
