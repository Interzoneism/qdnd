using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using QDND.Combat.Entities;
using QDND.Data;
using QDND.Data.CharacterModel;
using QDND.Tools.AutoBattler;

namespace QDND.Combat.Arena.CustomFight
{
    public static class CustomFightManager
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Resolve the combat seed. If configuredSeed is 0, generate a random seed.
        /// </summary>
        public static int ResolveSeed(int configuredSeed)
        {
            if (configuredSeed != 0)
                return configuredSeed;

            return System.Environment.TickCount & 0x7FFFFFFF; // Ensure positive
        }

        /// <summary>
        /// Build a ScenarioDefinition from the inspector-configured combatant array.
        /// </summary>
        public static ScenarioDefinition BuildScenario(
            Godot.Collections.Array<CustomFightCombatantConfig> combatants,
            int seed,
            CharacterDataRegistry charRegistry)
        {
            if (combatants == null || combatants.Count == 0)
                throw new ArgumentException("At least one combatant must be configured.");
            if (charRegistry == null)
                throw new ArgumentNullException(nameof(charRegistry));

            var generator = new ScenarioGenerator(charRegistry, seed);

            var scenario = new ScenarioDefinition
            {
                Id = $"custom_fight_seed_{seed}",
                Name = "Custom Fight",
                Seed = seed,
                Units = new List<ScenarioUnit>()
            };

            int team1Index = 0;
            int team2Index = 0;

            for (int i = 0; i < combatants.Count; i++)
            {
                var cfg = combatants[i];
                if (cfg == null) continue;

                int level = Math.Clamp(cfg.Level, 1, 12);
                var faction = cfg.Team.ToFaction();
                bool isTeam1 = cfg.Team == CombatTeam.Team1;

                int teamIdx = isTeam1 ? team1Index++ : team2Index++;
                string unitId = isTeam1
                    ? $"cf_team1_{teamIdx}"
                    : $"cf_team2_{teamIdx}";

                string displayName = !string.IsNullOrWhiteSpace(cfg.DisplayName)
                    ? cfg.DisplayName
                    : $"{cfg.Class} {cfg.Race}";

                float x = isTeam1 ? -4f : 4f;
                float z = teamIdx * 2f;

                var options = new CharacterGenerationOptions
                {
                    UnitId = unitId,
                    DisplayName = displayName,
                    Faction = faction,
                    Level = level,
                    X = x,
                    Y = 0f,
                    Z = z,
                    ForcedClassId = cfg.Class.ToClassId(),
                    ForcedRaceId = cfg.Race.ToRaceId()
                };

                scenario.Units.Add(generator.GenerateRandomUnit(options));
            }

            return scenario;
        }

        /// <summary>
        /// Save a preset to user://custom_fights/{presetName}.json.
        /// </summary>
        public static void SavePreset(
            string presetName,
            int seed,
            Godot.Collections.Array<CustomFightCombatantConfig> combatants)
        {
            if (string.IsNullOrWhiteSpace(presetName))
            {
                GD.PushError("[CustomFightManager] Cannot save preset with empty name.");
                return;
            }

            var preset = new CustomFightPreset
            {
                Name = presetName,
                Seed = seed,
                Combatants = new List<CustomFightPresetCombatant>()
            };

            foreach (var cfg in combatants)
            {
                if (cfg == null) continue;
                preset.Combatants.Add(new CustomFightPresetCombatant
                {
                    DisplayName = cfg.DisplayName ?? "",
                    Class = cfg.Class,
                    Race = cfg.Race,
                    Level = cfg.Level,
                    Team = cfg.Team,
                    AiControlled = cfg.AiControlled
                });
            }

            string dirPath = ProjectSettings.GlobalizePath("user://custom_fights");
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            // Sanitize filename
            string safeName = presetName.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
            string filePath = Path.Combine(dirPath, $"{safeName}.json");

            string json = JsonSerializer.Serialize(preset, JsonOptions);
            File.WriteAllText(filePath, json);
            GD.Print($"[CustomFightManager] Preset saved to: {filePath}");
        }

        /// <summary>
        /// Load a preset from a file path (can be user:// or absolute).
        /// </summary>
        public static CustomFightPreset LoadPreset(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Preset file path is empty.");

            // Resolve user:// paths
            string resolvedPath = filePath.StartsWith("user://") || filePath.StartsWith("res://")
                ? ProjectSettings.GlobalizePath(filePath)
                : filePath;

            if (!File.Exists(resolvedPath))
                throw new FileNotFoundException($"Preset file not found: {resolvedPath}");

            string json = File.ReadAllText(resolvedPath);
            var preset = JsonSerializer.Deserialize<CustomFightPreset>(json, JsonOptions);
            if (preset == null)
                throw new InvalidOperationException($"Failed to deserialize preset from: {resolvedPath}");

            GD.Print($"[CustomFightManager] Preset loaded: {preset.Name} ({preset.Combatants.Count} combatants)");
            return preset;
        }

        /// <summary>
        /// Apply a loaded preset to the target combatant array.
        /// </summary>
        public static void ApplyPreset(
            CustomFightPreset preset,
            Godot.Collections.Array<CustomFightCombatantConfig> targetArray,
            out int seed)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (targetArray == null) throw new ArgumentNullException(nameof(targetArray));

            targetArray.Clear();
            seed = preset.Seed;

            if (preset.Combatants == null || preset.Combatants.Count == 0)
            {
                GD.PushWarning("[CustomFightManager] Preset has no combatants.");
                return;
            }

            foreach (var pc in preset.Combatants)
            {
                var cfg = new CustomFightCombatantConfig
                {
                    DisplayName = pc.DisplayName ?? "",
                    Class = pc.Class,
                    Race = pc.Race,
                    Level = pc.Level,
                    Team = pc.Team,
                    AiControlled = pc.AiControlled
                };
                targetArray.Add(cfg);
            }
        }

        /// <summary>
        /// Create a BlackBoxLogger for custom fight combat logging.
        /// </summary>
        public static BlackBoxLogger CreateLogger(int seed)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string logDir = Path.Combine(
                ProjectSettings.GlobalizePath("res://"),
                "artifacts", "autobattle");

            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            string logPath = Path.Combine(logDir, $"CF_{seed}_{timestamp}.jsonl");
            return new BlackBoxLogger(logPath, writeToStdout: true);
        }
    }
}
