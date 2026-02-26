using System;
using System.Collections.Generic;
using QDND.Combat.Reactions;
using QDND.Combat.Rules;

namespace QDND.Data
{
    public interface IScenarioMigrationService
    {
        int CurrentSchemaVersion { get; }
        bool NeedsMigration(ScenarioDefinition scenario);
        ScenarioDefinition Migrate(ScenarioDefinition scenario, out ScenarioMigrationReport report);
    }

    public sealed class ScenarioMigrationReport
    {
        public int FromSchemaVersion { get; set; }
        public int ToSchemaVersion { get; set; }
        public int DistanceFieldsConverted { get; set; }
        public int ReactionAliasesRemapped { get; set; }
        public int InitiativeDefaultsApplied { get; set; }

        public bool HasChanges =>
            DistanceFieldsConverted > 0 || ReactionAliasesRemapped > 0 || InitiativeDefaultsApplied > 0
            || FromSchemaVersion != ToSchemaVersion;
    }

    public sealed class ScenarioMigrationService : IScenarioMigrationService
    {
        private readonly IReactionAliasResolver _reactionAliasResolver;

        public int CurrentSchemaVersion => 2;

        public ScenarioMigrationService(IReactionAliasResolver reactionAliasResolver = null)
        {
            _reactionAliasResolver = reactionAliasResolver ?? new ReactionAliasResolver();
        }

        public bool NeedsMigration(ScenarioDefinition scenario)
        {
            if (scenario == null)
            {
                return false;
            }

            if (scenario.SchemaVersion <= 0 || scenario.SchemaVersion < CurrentSchemaVersion)
            {
                return true;
            }

            if (!string.Equals(scenario.UnitSystem, "m", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public ScenarioDefinition Migrate(ScenarioDefinition scenario, out ScenarioMigrationReport report)
        {
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));

            report = new ScenarioMigrationReport
            {
                FromSchemaVersion = scenario.SchemaVersion <= 0 ? 1 : scenario.SchemaVersion,
                ToSchemaVersion = CurrentSchemaVersion
            };

            if (!NeedsMigration(scenario))
            {
                return scenario;
            }

            bool legacyUnits = !string.Equals(scenario.UnitSystem, "m", StringComparison.OrdinalIgnoreCase);
            if (legacyUnits && scenario.Units != null)
            {
                // Keep positions unless they are obviously legacy feet-scale values.
                foreach (var unit in scenario.Units ?? new List<ScenarioUnit>())
                {
                    float newX = CombatRules.NormalizeDistanceToMeters(unit.X);
                    float newY = CombatRules.NormalizeDistanceToMeters(unit.Y);
                    float newZ = CombatRules.NormalizeDistanceToMeters(unit.Z);
                    if (!newX.Equals(unit.X)) report.DistanceFieldsConverted++;
                    if (!newY.Equals(unit.Y)) report.DistanceFieldsConverted++;
                    if (!newZ.Equals(unit.Z)) report.DistanceFieldsConverted++;
                    unit.X = newX;
                    unit.Y = newY;
                    unit.Z = newZ;

                    if (unit.KnownActions == null)
                        continue;

                    for (int i = 0; i < unit.KnownActions.Count; i++)
                    {
                        var raw = unit.KnownActions[i];
                        var normalized = _reactionAliasResolver.Resolve(raw);
                        if (!string.Equals(raw, normalized, StringComparison.OrdinalIgnoreCase))
                        {
                            unit.KnownActions[i] = normalized;
                            report.ReactionAliasesRemapped++;
                        }
                    }
                }
            }

            if (scenario.InitiativeMode != InitiativeMode.UsePreset)
            {
                scenario.InitiativeMode = InitiativeMode.RollAtCombatStart;
                report.InitiativeDefaultsApplied++;
            }

            scenario.UnitSystem = "m";
            scenario.SchemaVersion = CurrentSchemaVersion;
            return scenario;
        }
    }
}
