using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Services;

namespace QDND.Data
{
    /// <summary>
    /// Unit definition in a scenario file.
    /// </summary>
    public class ScenarioUnit
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public object Faction { get; set; }

        [JsonPropertyName("team")]
        public object Team { get => Faction; set => Faction = value; }

        public int? HP { get; set; }
        public int? MaxHp { get; set; }  // If not set, defaults to HP
        public int Initiative { get; set; }
        public int InitiativeTiebreaker { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    /// <summary>
    /// Scenario definition loaded from JSON.
    /// </summary>
    public class ScenarioDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Seed { get; set; }
        public List<ScenarioUnit> Units { get; set; } = new();

        [JsonPropertyName("combatants")]
        public List<ScenarioUnit> Combatants { get => Units; set => Units = value; }
    }

    /// <summary>
    /// Loads combat scenarios from JSON files.
    /// </summary>
    public class ScenarioLoader
    {
        private Random _rng;

        /// <summary>
        /// Current RNG (seeded from scenario).
        /// </summary>
        public Random Rng => _rng;

        /// <summary>
        /// Current seed.
        /// </summary>
        public int CurrentSeed { get; private set; }

        /// <summary>
        /// Load a scenario from a file path.
        /// </summary>
        /// <param name="path">Path relative to res:// or absolute path</param>
        /// <returns>The parsed scenario definition</returns>
        public ScenarioDefinition LoadFromFile(string path)
        {
            string json;

            // Try to load using Godot's FileAccess first (for res:// paths)
            if (path.StartsWith("res://"))
            {
                using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    throw new FileNotFoundException($"Could not load scenario file: {path}");
                }
                json = file.GetAsText();
            }
            else
            {
                // Fall back to System.IO for absolute paths
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"Could not find scenario file: {path}");
                }
                json = File.ReadAllText(path);
            }

            return LoadFromJson(json);
        }

        /// <summary>
        /// Load a scenario from JSON string.
        /// </summary>
        public ScenarioDefinition LoadFromJson(string json)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(json, options);
            if (scenario == null)
            {
                throw new InvalidOperationException("Failed to parse scenario JSON");
            }

            // Initialize RNG with scenario seed
            CurrentSeed = scenario.Seed;
            _rng = new Random(scenario.Seed);

            return scenario;
        }

        /// <summary>
        /// Convert a scenario definition to combatants and add to turn queue.
        /// </summary>
        public List<Combatant> SpawnCombatants(ScenarioDefinition scenario, TurnQueueService turnQueue)
        {
            var combatants = new List<Combatant>();

            foreach (var unit in scenario.Units)
            {
                var faction = ParseFaction(unit.Faction);
                var name = string.IsNullOrEmpty(unit.Name) ? unit.Id : unit.Name;
                int maxHp = unit.MaxHp ?? unit.HP ?? 0;
                int currentHp = unit.HP ?? maxHp;

                var combatant = new Combatant(unit.Id, name, faction, maxHp, unit.Initiative)
                {
                    InitiativeTiebreaker = unit.InitiativeTiebreaker,
                    Position = new Vector3(unit.X, unit.Y, unit.Z)
                };

                // If HP differs from MaxHp, set the current HP
                if (currentHp != maxHp)
                {
                    combatant.Resources.CurrentHP = currentHp;
                }

                combatants.Add(combatant);
                turnQueue.AddCombatant(combatant);
            }

            return combatants;
        }

        /// <summary>
        /// Parse faction object to enum.
        /// </summary>
        private Faction ParseFaction(object factionObj)
        {
            if (factionObj is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int val))
                {
                    return (Faction)val;
                }
                return ParseFactionString(element.ToString());
            }

            if (factionObj is int i) return (Faction)i;
            if (factionObj is long l) return (Faction)(int)l;

            return ParseFactionString(factionObj?.ToString());
        }

        private Faction ParseFactionString(string factionStr)
        {
            return factionStr?.ToLowerInvariant() switch
            {
                "player" => Faction.Player,
                "hostile" or "enemy" => Faction.Hostile,
                "neutral" => Faction.Neutral,
                "ally" => Faction.Ally,
                _ => Faction.Neutral
            };
        }

        /// <summary>
        /// Get the default scenario path.
        /// </summary>
        public static string GetDefaultScenarioPath()
        {
            return "res://Data/Scenarios/minimal_combat.json";
        }

        /// <summary>
        /// Roll a value using the scenario's seeded RNG.
        /// </summary>
        public int Roll(int min, int max)
        {
            return _rng?.Next(min, max + 1) ?? throw new InvalidOperationException("RNG not initialized - load a scenario first");
        }

        /// <summary>
        /// Roll a d20.
        /// </summary>
        public int RollD20()
        {
            return Roll(1, 20);
        }
    }
}
