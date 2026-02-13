using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Statuses;

namespace QDND.Combat.Rules
{
    /// <summary>
    /// Registers passive rule providers from character/status data into the canonical rule-window bus.
    /// </summary>
    public class PassiveRuleService
    {
        private readonly RulesEngine _rules;
        private readonly StatusManager _statuses;
        private readonly Func<IEnumerable<Combatant>> _getCombatants;
        private readonly List<PassiveRuleDefinition> _definitions;

        private readonly Dictionary<string, HashSet<string>> _providerIdsByBucket = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _statusBucketByInstance = new(StringComparer.OrdinalIgnoreCase);

        public PassiveRuleService(
            RulesEngine rules,
            StatusManager statuses,
            Func<IEnumerable<Combatant>> getCombatants,
            IEnumerable<PassiveRuleDefinition> definitions)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
            _getCombatants = getCombatants ?? throw new ArgumentNullException(nameof(getCombatants));
            _definitions = definitions?.ToList() ?? new List<PassiveRuleDefinition>();

            _statuses.OnStatusApplied += HandleStatusApplied;
            _statuses.OnStatusRemoved += HandleStatusRemoved;
        }

        public void Dispose()
        {
            _statuses.OnStatusApplied -= HandleStatusApplied;
            _statuses.OnStatusRemoved -= HandleStatusRemoved;
            ClearAllProviders();
        }

        public void RebuildForCombatants(IEnumerable<Combatant> combatants)
        {
            ClearAllProviders();

            foreach (var combatant in combatants ?? Enumerable.Empty<Combatant>())
            {
                RegisterCombatant(combatant);
            }

            // Register status-derived providers for currently active statuses.
            foreach (var status in _statuses.GetAllStatuses())
            {
                RegisterStatusProviders(status);
            }
        }

        public void RegisterCombatant(Combatant combatant)
        {
            if (combatant == null)
                return;

            string bucket = GetCombatantBucket(combatant.Id);
            UnregisterBucket(bucket);

            // Passive resistances/immunities are always-on grants from race/class/features.
            RegisterDamageTypeModifiers(combatant);

            foreach (var definition in _definitions)
            {
                if (definition?.Selector == null)
                    continue;

                if (definition.Selector.StatusIds?.Count > 0)
                    continue; // status-sourced providers register dynamically.

                if (!MatchesSelector(definition.Selector, combatant, statusId: null))
                    continue;

                var provider = PassiveRuleProviderFactory.Create(
                    definition,
                    combatant.Id,
                    BuildDependencies(),
                    providerInstanceSuffix: null);

                if (provider == null)
                    continue;

                RegisterProvider(bucket, provider);
            }
        }

        private void HandleStatusApplied(StatusInstance instance)
        {
            RegisterStatusProviders(instance);
        }

        private void HandleStatusRemoved(StatusInstance instance)
        {
            if (instance == null)
                return;

            if (_statusBucketByInstance.TryGetValue(instance.InstanceId, out var bucket))
            {
                UnregisterBucket(bucket);
                _statusBucketByInstance.Remove(instance.InstanceId);
            }
        }

        private void RegisterStatusProviders(StatusInstance instance)
        {
            if (instance == null)
                return;

            string statusId = instance.Definition?.Id;
            if (string.IsNullOrWhiteSpace(statusId))
                return;

            string bucket = $"status:{instance.InstanceId}";
            UnregisterBucket(bucket);

            var ownerCombatant = _getCombatants()?.FirstOrDefault(c =>
                string.Equals(c.Id, instance.TargetId, StringComparison.OrdinalIgnoreCase));
            if (ownerCombatant == null)
                return;

            foreach (var definition in _definitions)
            {
                if (definition?.Selector == null || definition.Selector.StatusIds == null || definition.Selector.StatusIds.Count == 0)
                    continue;

                if (!MatchesSelector(definition.Selector, ownerCombatant, statusId))
                    continue;

                var provider = PassiveRuleProviderFactory.Create(
                    definition,
                    ownerCombatant.Id,
                    BuildDependencies(),
                    providerInstanceSuffix: instance.InstanceId);

                if (provider == null)
                    continue;

                RegisterProvider(bucket, provider);
                _statusBucketByInstance[instance.InstanceId] = bucket;
            }
        }

        private PassiveProviderDependencies BuildDependencies()
        {
            return new PassiveProviderDependencies
            {
                GetCombatants = _getCombatants,
                ResolveCombatant = id => _getCombatants()?.FirstOrDefault(c =>
                    string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)),
                HasStatusHasted = combatant => combatant != null && _statuses.HasStatus(combatant.Id, "hasted")
            };
        }

        private void RegisterDamageTypeModifiers(Combatant combatant)
        {
            var mods = _rules.GetModifiers(combatant.Id);
            string sourcePrefix = $"passive_registry:{combatant.Id}:";

            // Clear previously registered passive resistance/immunity modifiers for this combatant.
            var existingModifierIds = mods.Modifiers
                .Where(m => m.Source?.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase) == true)
                .Select(m => m.Id)
                .ToList();
            foreach (var modifierId in existingModifierIds)
            {
                mods.Remove(modifierId);
            }

            foreach (var damageType in combatant.ResolvedCharacter?.DamageResistances ?? new HashSet<QDND.Data.CharacterModel.DamageType>())
            {
                string typeText = damageType.ToString().ToLowerInvariant();
                string source = $"passive_registry:{combatant.Id}:resistance:{typeText}";
                _rules.AddModifier(combatant.Id, DamageResistance.CreateResistance(typeText, source));
            }

            foreach (var damageType in combatant.ResolvedCharacter?.DamageImmunities ?? new HashSet<QDND.Data.CharacterModel.DamageType>())
            {
                string typeText = damageType.ToString().ToLowerInvariant();
                string source = $"passive_registry:{combatant.Id}:immunity:{typeText}";
                _rules.AddModifier(combatant.Id, DamageResistance.CreateImmunity(typeText, source));
            }
        }

        private void RegisterProvider(string bucket, IRuleProvider provider)
        {
            _rules.RuleWindows.Register(provider);
            if (!_providerIdsByBucket.TryGetValue(bucket, out var ids))
            {
                ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _providerIdsByBucket[bucket] = ids;
            }

            ids.Add(provider.ProviderId);
        }

        private void UnregisterBucket(string bucket)
        {
            if (string.IsNullOrWhiteSpace(bucket))
                return;

            if (_providerIdsByBucket.TryGetValue(bucket, out var providerIds))
            {
                foreach (var providerId in providerIds)
                {
                    _rules.RuleWindows.Unregister(providerId);
                }

                _providerIdsByBucket.Remove(bucket);
            }
        }

        private void ClearAllProviders()
        {
            foreach (var bucket in _providerIdsByBucket.Keys.ToList())
            {
                UnregisterBucket(bucket);
            }

            _providerIdsByBucket.Clear();
            _statusBucketByInstance.Clear();
        }

        private static string GetCombatantBucket(string combatantId)
            => $"combatant:{combatantId}";

        private static bool MatchesSelector(PassiveRuleSelector selector, Combatant combatant, string statusId)
        {
            var featureIds = combatant.ResolvedCharacter?.Features?
                .Select(f => f.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var featureTags = combatant.ResolvedCharacter?.Features?
                .SelectMany(f => f.Tags ?? new List<string>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var featIds = combatant.ResolvedCharacter?.Sheet?.FeatIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var classIds = combatant.ResolvedCharacter?.Sheet?.ClassLevels?
                .Select(cl => cl.ClassId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var raceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(combatant.ResolvedCharacter?.Sheet?.RaceId))
                raceIds.Add(combatant.ResolvedCharacter.Sheet.RaceId);

            var itemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(combatant.Equipment?.MainHandWeaponId))
                itemIds.Add(combatant.Equipment.MainHandWeaponId);
            if (!string.IsNullOrWhiteSpace(combatant.Equipment?.OffHandWeaponId))
                itemIds.Add(combatant.Equipment.OffHandWeaponId);
            if (!string.IsNullOrWhiteSpace(combatant.Equipment?.ArmorId))
                itemIds.Add(combatant.Equipment.ArmorId);
            if (!string.IsNullOrWhiteSpace(combatant.Equipment?.ShieldId))
                itemIds.Add(combatant.Equipment.ShieldId);

            bool matchesFeatureIds = MatchesAny(selector.FeatureIds, featureIds);
            bool matchesFeatureTags = MatchesAny(selector.FeatureTags, featureTags);
            bool matchesFeatIds = MatchesAny(selector.FeatIds, featIds);
            bool matchesClassIds = MatchesAny(selector.ClassIds, classIds);
            bool matchesRaceIds = MatchesAny(selector.RaceIds, raceIds);
            bool matchesItemIds = MatchesAny(selector.ItemIds, itemIds);
            bool matchesStatusIds = MatchesStatus(selector.StatusIds, statusId);

            return matchesFeatureIds
                && matchesFeatureTags
                && matchesFeatIds
                && matchesClassIds
                && matchesRaceIds
                && matchesItemIds
                && matchesStatusIds;
        }

        private static bool MatchesAny(IReadOnlyCollection<string> expected, HashSet<string> actual)
        {
            if (expected == null || expected.Count == 0)
                return true;

            foreach (var value in expected)
            {
                if (!string.IsNullOrWhiteSpace(value) && actual.Contains(value))
                    return true;
            }

            return false;
        }

        private static bool MatchesStatus(IReadOnlyCollection<string> expected, string actualStatus)
        {
            if (expected == null || expected.Count == 0)
                return true;

            if (string.IsNullOrWhiteSpace(actualStatus))
                return false;

            return expected.Any(e => string.Equals(e, actualStatus, StringComparison.OrdinalIgnoreCase));
        }
    }
}
