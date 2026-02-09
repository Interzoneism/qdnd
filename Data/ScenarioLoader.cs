using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Data.CharacterModel;

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
        public List<string> Abilities { get; set; }
        public List<string> Tags { get; set; }
        
        // CharacterSheet fields (optional — if present, overrides manual HP/abilities)
        public string RaceId { get; set; }
        public string SubraceId { get; set; }
        public List<ClassLevelEntry> ClassLevels { get; set; }
        public string AbilityBonus2 { get; set; }
        public string AbilityBonus1 { get; set; }
        public int? BaseStrength { get; set; }
        public int? BaseDexterity { get; set; }
        public int? BaseConstitution { get; set; }
        public int? BaseIntelligence { get; set; }
        public int? BaseWisdom { get; set; }
        public int? BaseCharisma { get; set; }
        public List<string> FeatIds { get; set; }
        public string BackgroundId { get; set; }
        public List<string> BackgroundSkills { get; set; }
    }

    /// <summary>
    /// Helper class for class level entries in scenario JSON.
    /// </summary>
    public class ClassLevelEntry
    {
        public string ClassId { get; set; }
        public string SubclassId { get; set; }
        public int Levels { get; set; } = 1;
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
        private CharacterDataRegistry _charRegistry;

        /// <summary>
        /// Current RNG (seeded from scenario).
        /// </summary>
        public Random Rng => _rng;

        /// <summary>
        /// Current seed.
        /// </summary>
        public int CurrentSeed { get; private set; }
        
        /// <summary>
        /// Set the CharacterDataRegistry for character build resolution.
        /// </summary>
        public void SetCharacterDataRegistry(CharacterDataRegistry registry)
        {
            _charRegistry = registry;
        }

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
                json = Encoding.UTF8.GetString(file.GetBuffer((long)file.GetLength()));
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

                // Assign abilities from scenario data (or auto-assign defaults based on name)
                if (unit.Abilities != null && unit.Abilities.Count > 0)
                {
                    combatant.Abilities = new List<string>(unit.Abilities);
                }
                else
                {
                    // Auto-assign role-appropriate defaults based on unit name
                    combatant.Abilities = GetDefaultAbilities(unit.Name);
                }

                // Assign tags from scenario data (or auto-assign defaults)
                if (unit.Tags != null && unit.Tags.Count > 0)
                {
                    combatant.Tags = new List<string>(unit.Tags);
                }
                else
                {
                    combatant.Tags = GetDefaultTags(unit.Name);
                }
                
                // Check if unit has character build data
                if (unit.ClassLevels != null && unit.ClassLevels.Count > 0 && _charRegistry != null)
                {
                    var resolved = ResolveCharacterBuild(unit);
                    if (resolved != null)
                    {
                        // Override HP from class formula
                        combatant.Resources.MaxHP = resolved.MaxHP;
                        combatant.Resources.CurrentHP = resolved.MaxHP;
                        
                        // Set stats
                        combatant.Stats = new CombatantStats
                        {
                            Strength = resolved.AbilityScores[AbilityType.Strength],
                            Dexterity = resolved.AbilityScores[AbilityType.Dexterity],
                            Constitution = resolved.AbilityScores[AbilityType.Constitution],
                            Intelligence = resolved.AbilityScores[AbilityType.Intelligence],
                            Wisdom = resolved.AbilityScores[AbilityType.Wisdom],
                            Charisma = resolved.AbilityScores[AbilityType.Charisma],
                            BaseAC = resolved.BaseAC,
                            Speed = resolved.Speed
                        };
                        
                        // Override abilities: combine resolved abilities with any explicit scenario abilities
                        var allAbilities = new List<string>(resolved.AllAbilities);
                        if (unit.Abilities != null)
                            allAbilities.AddRange(unit.Abilities);
                        combatant.Abilities = allAbilities.Distinct().ToList();
                        
                        // Store the resolved character and proficiency bonus
                        combatant.ResolvedCharacter = resolved;
                        combatant.ProficiencyBonus = resolved.Sheet.ProficiencyBonus;
                        
                        // If unit has explicit initiative, use it; otherwise compute from character build
                        if (unit.Initiative == 0)
                        {
                            int dexMod = CombatantStats.GetModifier(resolved.AbilityScores[AbilityType.Dexterity]);
                            combatant.Initiative = Roll(1, 4) + dexMod;
                            combatant.InitiativeTiebreaker = resolved.AbilityScores[AbilityType.Dexterity];
                        }
                    }
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

        /// <summary>
        /// Get default abilities based on unit name/role.
        /// </summary>
        private List<string> GetDefaultAbilities(string name)
        {
            var normalized = name?.ToLowerInvariant() ?? "";
            
            // Casters
            if (normalized.Contains("wizard") || normalized.Contains("mage") || normalized.Contains("arcanist"))
                return new List<string> { "basic_attack", "fireball", "magic_missile", "fire_bolt" };
            if (normalized.Contains("warlock") || normalized.Contains("hexer"))
                return new List<string> { "basic_attack", "eldritch_blast", "magic_missile" };
            
            // Healers  
            if (normalized.Contains("cleric") || normalized.Contains("healer") || normalized.Contains("shaman"))
                return new List<string> { "basic_attack", "heal_wounds", "healing_word", "sacred_flame" };
            if (normalized.Contains("paladin"))
                return new List<string> { "basic_attack", "smite", "shield_of_faith", "heal_wounds" };
            if (normalized.Contains("druid"))
                return new List<string> { "basic_attack", "heal_wounds", "thunderwave", "poison_strike" };
            
            // Martial melee
            if (normalized.Contains("fighter") || normalized.Contains("warrior") || normalized.Contains("brute") || normalized.Contains("guardian"))
                return new List<string> { "basic_attack", "power_strike", "second_wind", "battle_cry" };
            if (normalized.Contains("barbarian") || normalized.Contains("berserker") || normalized.Contains("ravager"))
                return new List<string> { "basic_attack", "power_strike", "rage" };
            if (normalized.Contains("rogue") || normalized.Contains("skirmisher") || normalized.Contains("scout"))
                return new List<string> { "basic_attack", "sneak_attack", "poison_strike", "disengage" };
            
            // Ranged
            if (normalized.Contains("archer") || normalized.Contains("ranger"))
                return new List<string> { "ranged_attack", "poison_strike" };
            
            // Monsters
            if (normalized.Contains("troll") || normalized.Contains("ogre"))
                return new List<string> { "basic_attack", "power_strike" };
            if (normalized.Contains("wolf") || normalized.Contains("beast") || normalized.Contains("spider"))
                return new List<string> { "basic_attack", "poison_strike" };
            if (normalized.Contains("goblin"))
                return new List<string> { "basic_attack", "poison_strike", "disengage" };
            if (normalized.Contains("orc"))
                return new List<string> { "basic_attack", "power_strike" };
            
            // Default: melee basic attack + a strike option
            return new List<string> { "basic_attack", "power_strike" };
        }

        /// <summary>
        /// Get default tags based on unit name/role.
        /// </summary>
        private List<string> GetDefaultTags(string name)
        {
            var normalized = name?.ToLowerInvariant() ?? "";
            
            // Casters
            if (normalized.Contains("wizard") || normalized.Contains("mage") || normalized.Contains("arcanist"))
                return new List<string> { "ranged", "caster", "damage" };
            if (normalized.Contains("warlock") || normalized.Contains("hexer"))
                return new List<string> { "ranged", "caster", "damage" };
            
            // Healers/Support
            if (normalized.Contains("cleric") || normalized.Contains("healer") || normalized.Contains("shaman"))
                return new List<string> { "melee", "healer", "support" };
            if (normalized.Contains("paladin"))
                return new List<string> { "melee", "tank", "support" };
            if (normalized.Contains("druid"))
                return new List<string> { "melee", "caster", "support" };
            
            // Martial melee
            if (normalized.Contains("fighter") || normalized.Contains("warrior") || normalized.Contains("brute") || normalized.Contains("guardian"))
                return new List<string> { "melee", "tank", "damage" };
            if (normalized.Contains("barbarian") || normalized.Contains("berserker") || normalized.Contains("ravager"))
                return new List<string> { "melee", "damage", "tank" };
            if (normalized.Contains("rogue") || normalized.Contains("skirmisher") || normalized.Contains("scout"))
                return new List<string> { "melee", "damage", "striker" };
            
            // Ranged
            if (normalized.Contains("archer") || normalized.Contains("ranger"))
                return new List<string> { "ranged", "damage" };
            
            // Monsters
            if (normalized.Contains("troll") || normalized.Contains("ogre"))
                return new List<string> { "melee", "damage" };
            if (normalized.Contains("wolf") || normalized.Contains("beast") || normalized.Contains("spider"))
                return new List<string> { "melee", "damage", "beast" };
            if (normalized.Contains("goblin"))
                return new List<string> { "melee", "damage" };
            if (normalized.Contains("orc"))
                return new List<string> { "melee", "tank", "damage" };
            
            // Boss detection
            if (normalized.Contains("boss") || normalized.Contains("ancient") || normalized.Contains("elder") || normalized.Contains("king") || normalized.Contains("queen"))
                return new List<string> { "melee", "damage", "boss" };
            
            return new List<string> { "melee" };
        }
        
        /// <summary>
        /// Resolve a character build from a scenario unit.
        /// </summary>
        private ResolvedCharacter ResolveCharacterBuild(ScenarioUnit unit)
        {
            try
            {
                var sheet = new CharacterSheet
                {
                    Name = unit.Name ?? unit.Id,
                    RaceId = unit.RaceId,
                    SubraceId = unit.SubraceId,
                    BaseStrength = unit.BaseStrength ?? 10,
                    BaseDexterity = unit.BaseDexterity ?? 10,
                    BaseConstitution = unit.BaseConstitution ?? 10,
                    BaseIntelligence = unit.BaseIntelligence ?? 10,
                    BaseWisdom = unit.BaseWisdom ?? 10,
                    BaseCharisma = unit.BaseCharisma ?? 10,
                    AbilityBonus2 = unit.AbilityBonus2,
                    AbilityBonus1 = unit.AbilityBonus1,
                    FeatIds = unit.FeatIds ?? new List<string>(),
                    BackgroundId = unit.BackgroundId,
                    BackgroundSkills = unit.BackgroundSkills ?? new List<string>()
                };
                
                // Build class levels
                if (unit.ClassLevels != null)
                {
                    foreach (var cl in unit.ClassLevels)
                    {
                        for (int i = 0; i < cl.Levels; i++)
                            sheet.ClassLevels.Add(new ClassLevel(cl.ClassId, cl.SubclassId));
                    }
                }
                
                var resolver = new CharacterResolver(_charRegistry);
                return resolver.Resolve(sheet);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[ScenarioLoader] Failed to resolve character build for {unit.Id}: {ex.Message}");
                return null;
            }
        }
    }
}

