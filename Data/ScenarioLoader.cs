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
        
        [JsonPropertyName("actions")]
        public List<string> KnownActions { get; set; }
        
        [JsonPropertyName("passives")]
        public List<string> Passives { get; set; }
        
        [JsonPropertyName("replaceActions")]
        public bool ReplaceResolvedActions { get; set; }

        public List<string> Tags { get; set; }
        
        // CharacterSheet fields (optional — if present, overrides manual HP/abilities)
        public string RaceId { get; set; }
        public string SubraceId { get; set; }
        public List<ClassLevelEntry> ClassLevels { get; set; }
        public string AbilityBonus2 { get; set; }
        public string AbilityBonus1 { get; set; }
        
        // BG3 character template reference
        [JsonPropertyName("bg3TemplateId")]
        public string Bg3TemplateId { get; set; }
        
        public int? BaseStrength { get; set; }
        public int? BaseDexterity { get; set; }
        public int? BaseConstitution { get; set; }
        public int? BaseIntelligence { get; set; }
        public int? BaseWisdom { get; set; }
        public int? BaseCharisma { get; set; }
        public List<string> FeatIds { get; set; }
        public string BackgroundId { get; set; }
        public List<string> BackgroundSkills { get; set; }
        
        // Equipment fields (optional — specify weapon/armor by ID)
        public string MainHandWeaponId { get; set; }
        public string OffHandWeaponId { get; set; }
        public string ArmorId { get; set; }
        public string ShieldId { get; set; }
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
        private DataRegistry _dataRegistry;
        private Stats.StatsRegistry _statsRegistry;
        private QDND.Combat.Actions.ActionRegistry _actionRegistry;
        private HashSet<string> _cachedDataActionIds;
        private bool _cachedDataActionIdsLoaded;

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
        /// Set the DataRegistry for canonical Data/Actions ID resolution.
        /// </summary>
        public void SetDataRegistry(DataRegistry registry)
        {
            _dataRegistry = registry;
            _cachedDataActionIds = null;
            _cachedDataActionIdsLoaded = false;
        }
        
        /// <summary>
        /// Set the StatsRegistry for BG3 character template resolution.
        /// </summary>
        public void SetStatsRegistry(Stats.StatsRegistry registry)
        {
            _statsRegistry = registry;
        }
        
        /// <summary>
        /// Set the ActionRegistry for ability validation during character resolution.
        /// </summary>
        public void SetActionRegistry(QDND.Combat.Actions.ActionRegistry registry)
        {
            _actionRegistry = registry;
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
            var actionIdResolver = new ActionIdResolver(
                GetCanonicalDataActionIds(),
                _actionRegistry?.GetAllActionIds());

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
                if (unit.KnownActions != null && unit.KnownActions.Count > 0)
                {
                    var resolvedActions = ResolveKnownActions(unit.KnownActions, actionIdResolver, unit.Id);
                    if (resolvedActions.Count > 0)
                    {
                        combatant.KnownActions = resolvedActions;
                    }
                    else
                    {
                        Console.Error.WriteLine(
                            $"[ScenarioLoader] Unit '{unit.Id}' provided actions but none were resolvable. Falling back to defaults.");
                        combatant.KnownActions = ResolveKnownActions(GetDefaultAbilities(unit.Name), actionIdResolver, unit.Id);
                    }
                }
                else
                {
                    // Auto-assign role-appropriate defaults based on unit name
                    combatant.KnownActions = ResolveKnownActions(GetDefaultAbilities(unit.Name), actionIdResolver, unit.Id);
                }

                EnsureBasicAttack(combatant.KnownActions, actionIdResolver);

                // Assign passive IDs from scenario data
                if (unit.Passives != null && unit.Passives.Count > 0)
                {
                    combatant.PassiveIds = new List<string>(unit.Passives);
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
                        
                        // Override abilities:
                        // - replaceAbilities=true + explicit list: use explicit list only (action test mode)
                        // - otherwise: merge resolved + explicit and fall back to defaults if empty
                        var explicitActions = ResolveKnownActions(unit.KnownActions, actionIdResolver, unit.Id);
                        var resolvedAbilities = ResolveKnownActions(resolved.AllAbilities, actionIdResolver, unit.Id);

                        if (unit.ReplaceResolvedActions && explicitActions.Count > 0)
                        {
                            combatant.KnownActions = explicitActions;
                        }
                        else
                        {
                            var allAbilities = new List<string>(resolvedAbilities);
                            allAbilities.AddRange(explicitActions);

                            if (allAbilities.Count == 0)
                            {
                                allAbilities.AddRange(ResolveKnownActions(GetDefaultAbilities(unit.Name), actionIdResolver, unit.Id));
                            }

                            // Every combatant should always know Target_MainHandAttack - it represents
                            // the fundamental D&D 5e "Attack" action available to all creatures.
                            EnsureBasicAttack(allAbilities, actionIdResolver);

                            combatant.KnownActions = allAbilities.Distinct().ToList();
                        }
                        
                        // Store the resolved character and proficiency bonus
                        combatant.ResolvedCharacter = resolved;
                        combatant.ProficiencyBonus = resolved.Sheet.ProficiencyBonus;
                        combatant.ExtraAttacks = resolved.ExtraAttacks;
                        
                        // Set MaxAttacks based on ExtraAttacks (1 + ExtraAttacks)
                        combatant.ActionBudget.MaxAttacks = 1 + resolved.ExtraAttacks;
                        combatant.ActionBudget.ResetForTurn(); // Initialize with correct attack count

                        // Populate passive IDs from resolved features
                        if (resolved.Features != null)
                        {
                            var specificStyle = ChooseFightingStyle(unit.MainHandWeaponId);
                            var passiveFeatureIds = resolved.Features
                                .Where(f => f.IsPassive && !string.IsNullOrEmpty(f.Id))
                                .Select(f => f.Id)
                                .Select(id => string.Equals(id, "fighting_style", StringComparison.OrdinalIgnoreCase)
                                           || string.Equals(id, "fighting_style_bard", StringComparison.OrdinalIgnoreCase)
                                    ? specificStyle
                                    : id)
                                .Distinct()
                                .ToList();
                            combatant.PassiveIds.AddRange(passiveFeatureIds);
                        }
                        
                        // Add passives from BG3 template if present
                        if (!string.IsNullOrEmpty(unit.Bg3TemplateId) && _statsRegistry != null)
                        {
                            var bg3Template = _statsRegistry.GetCharacter(unit.Bg3TemplateId);
                            if (bg3Template != null && !string.IsNullOrEmpty(bg3Template.Passives))
                            {
                                var templatePassives = bg3Template.Passives
                                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(p => p.Trim())
                                    .Where(p => !string.IsNullOrEmpty(p))
                                    .ToList();
                                
                                foreach (var passive in templatePassives)
                                {
                                    if (!combatant.PassiveIds.Contains(passive))
                                        combatant.PassiveIds.Add(passive);
                                }
                            }
                        }

                        // Also add passives explicitly listed in scenario
                        if (unit.Passives != null)
                        {
                            foreach (var p in unit.Passives)
                            {
                                if (!combatant.PassiveIds.Contains(p))
                                    combatant.PassiveIds.Add(p);
                            }
                        }

                        if (resolved.Resources != null)
                        {
                            foreach (var (resourceId, maxValue) in resolved.Resources)
                            {
                                combatant.ResourcePool.SetMax(resourceId, maxValue, refillCurrent: true);
                            }
                        }
                        
                        // If unit has explicit initiative, use it; otherwise compute from character build
                        if (unit.Initiative == 0)
                        {
                            int dexMod = CombatantStats.GetModifier(resolved.AbilityScores[AbilityType.Dexterity]);
                            int initiativeBonus = 0;
                            bool hasAlertFeat = resolved.Sheet?.FeatIds?.Any(f =>
                                string.Equals(f, "alert", StringComparison.OrdinalIgnoreCase)) == true;
                            bool hasAlertTag = resolved.Features?.Any(f =>
                                f.Tags != null && f.Tags.Any(t => string.Equals(t, "initiative_bonus_5", StringComparison.OrdinalIgnoreCase))) == true;
                            if (hasAlertFeat || hasAlertTag)
                            {
                                initiativeBonus += 5;
                            }

                            combatant.Initiative = Roll(1, 20) + dexMod + initiativeBonus;
                            combatant.InitiativeTiebreaker = resolved.AbilityScores[AbilityType.Dexterity];
                        }
                    }
                }
                
                // Resolve equipment
                ResolveEquipment(combatant, unit);

                // Ensure every combatant has at least an unarmed strike weapon
                if (combatant.MainHandWeapon == null)
                {
                    combatant.MainHandWeapon = CreateUnarmedStrike(combatant);
                }

                // If the combatant wields a ranged weapon, ensure they have ranged_attack
                if (combatant.MainHandWeapon?.IsRanged == true)
                {
                    var rangedId = actionIdResolver.Resolve("Projectile_MainHandAttack");
                    var rangedActionId = rangedId.IsResolved ? rangedId.ResolvedId : "ranged_attack";
                    combatant.KnownActions ??= new List<string>();
                    if (!combatant.KnownActions.Any(a => string.Equals(a, rangedActionId, StringComparison.OrdinalIgnoreCase)))
                    {
                        combatant.KnownActions.Insert(0, rangedActionId);
                    }
                }

                combatants.Add(combatant);
                turnQueue.AddCombatant(combatant);
            }

            return combatants;
        }

        private List<string> ResolveKnownActions(IEnumerable<string> actionIds, ActionIdResolver resolver, string unitId)
        {
            var resolved = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (actionIds == null)
                return resolved;

            foreach (var actionId in actionIds)
            {
                if (string.IsNullOrWhiteSpace(actionId))
                    continue;

                var normalizedInput = actionId.Trim();
                var resolution = resolver.Resolve(normalizedInput);
                if (!resolution.IsResolved)
                {
                    var tried = resolution.CandidatesTried?.Count > 0
                        ? string.Join(", ", resolution.CandidatesTried.Take(4))
                        : normalizedInput;
                    Console.Error.WriteLine(
                        $"[ScenarioLoader] Unresolved action '{normalizedInput}' for unit '{unitId}'. Tried: {tried}");
                    continue;
                }

                if (!string.Equals(normalizedInput, resolution.ResolvedId, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(
                        $"[ScenarioLoader] Remapped action '{normalizedInput}' -> '{resolution.ResolvedId}' for unit '{unitId}'");
                }

                if (seen.Add(resolution.ResolvedId))
                {
                    resolved.Add(resolution.ResolvedId);
                }
            }

            return resolved;
        }

        private void EnsureBasicAttack(List<string> actionIds, ActionIdResolver resolver)
        {
            if (actionIds == null)
                return;

            var basicAttack = resolver.Resolve("Target_MainHandAttack");
            var fallbackId = basicAttack.IsResolved ? basicAttack.ResolvedId : "Target_MainHandAttack";

            if (!actionIds.Any(a => string.Equals(a, fallbackId, StringComparison.OrdinalIgnoreCase)))
            {
                actionIds.Insert(0, fallbackId);
            }
        }

        private HashSet<string> GetCanonicalDataActionIds()
        {
            if (_dataRegistry != null)
            {
                return _dataRegistry
                    .GetAllActions()
                    .Where(a => !string.IsNullOrWhiteSpace(a?.Id))
                    .Select(a => a.Id.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            if (!_cachedDataActionIdsLoaded)
            {
                _cachedDataActionIds = ActionIdResolver.LoadDataActionIds(AppContext.BaseDirectory);
                _cachedDataActionIdsLoaded = true;
            }

            return _cachedDataActionIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                return new List<string> { "Target_MainHandAttack", "Projectile_Fireball", "Projectile_MagicMissile", "Projectile_FireBolt" };
            if (normalized.Contains("warlock") || normalized.Contains("hexer"))
                return new List<string> { "Target_MainHandAttack", "Projectile_EldritchBlast", "Target_Hex" };
            
            // Healers  
            if (normalized.Contains("cleric") || normalized.Contains("healer") || normalized.Contains("shaman"))
                return new List<string> { "Target_MainHandAttack", "Target_CureWounds", "Target_HealingWord", "Target_SacredFlame" };
            if (normalized.Contains("paladin"))
                return new List<string> { "Target_MainHandAttack", "Target_DivineSmite", "Target_ShieldOfFaith", "Target_CureWounds" };
            if (normalized.Contains("druid"))
                return new List<string> { "Target_MainHandAttack", "Target_CureWounds", "Zone_Thunderwave", "Target_PoisonSpray" };
            
            // Martial melee
            if (normalized.Contains("fighter") || normalized.Contains("warrior") || normalized.Contains("brute") || normalized.Contains("guardian"))
                return new List<string> { "Target_MainHandAttack", "Shout_SecondWind", "Shout_ActionSurge" };
            if (normalized.Contains("barbarian") || normalized.Contains("berserker") || normalized.Contains("ravager"))
                return new List<string> { "Target_MainHandAttack", "Shout_Rage", "Shout_RecklessAttack" };
            if (normalized.Contains("rogue") || normalized.Contains("skirmisher") || normalized.Contains("scout"))
                return new List<string> { "Target_MainHandAttack", "Target_SneakAttack", "Shout_Disengage", "Shout_Hide" };
            
            // Ranged
            if (normalized.Contains("archer") || normalized.Contains("ranger"))
                return new List<string> { "Projectile_MainHandAttack", "Target_HuntersMark" };
            
            // Monsters
            if (normalized.Contains("troll") || normalized.Contains("ogre"))
                return new List<string> { "Target_MainHandAttack" };
            if (normalized.Contains("wolf") || normalized.Contains("beast") || normalized.Contains("spider"))
                return new List<string> { "Target_MainHandAttack" };
            if (normalized.Contains("goblin"))
                return new List<string> { "Target_MainHandAttack", "Shout_Disengage" };
            if (normalized.Contains("orc"))
                return new List<string> { "Target_MainHandAttack" };
            
            // Default: melee basic attack
            return new List<string> { "Target_MainHandAttack" };
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
        /// If Bg3TemplateId is set, loads the BG3 character template as a base.
        /// </summary>
        private ResolvedCharacter ResolveCharacterBuild(ScenarioUnit unit)
        {
            try
            {
                // Load BG3 template if specified
                Stats.BG3CharacterData bg3Template = null;
                if (!string.IsNullOrEmpty(unit.Bg3TemplateId) && _statsRegistry != null)
                {
                    bg3Template = _statsRegistry.GetCharacter(unit.Bg3TemplateId);
                    if (bg3Template == null)
                    {
                        Console.Error.WriteLine($"[ScenarioLoader] BG3 character template not found: {unit.Bg3TemplateId}");
                    }
                    else
                    {
                        Console.WriteLine($"[ScenarioLoader] Loaded BG3 template '{unit.Bg3TemplateId}' for unit '{unit.Id}'");
                    }
                }
                
                var sheet = new CharacterSheet
                {
                    Name = unit.Name ?? unit.Id,
                    RaceId = unit.RaceId,
                    SubraceId = unit.SubraceId,
                    BaseStrength = unit.BaseStrength ?? bg3Template?.Strength ?? 10,
                    BaseDexterity = unit.BaseDexterity ?? bg3Template?.Dexterity ?? 10,
                    BaseConstitution = unit.BaseConstitution ?? bg3Template?.Constitution ?? 10,
                    BaseIntelligence = unit.BaseIntelligence ?? bg3Template?.Intelligence ?? 10,
                    BaseWisdom = unit.BaseWisdom ?? bg3Template?.Wisdom ?? 10,
                    BaseCharisma = unit.BaseCharisma ?? bg3Template?.Charisma ?? 10,
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
                
                // Issue 4: Wire ActionRegistry into CharacterResolver for ability validation
                if (_actionRegistry != null)
                {
                    resolver.SetActionRegistry(_actionRegistry);
                }
                
                var resolved = resolver.Resolve(sheet);
                
                // Apply BG3 template passives if present
                if (bg3Template != null && !string.IsNullOrEmpty(bg3Template.Passives))
                {
                    var templatePassives = bg3Template.Passives
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrEmpty(p))
                        .ToList();
                    
                    if (templatePassives.Count > 0)
                    {
                        Console.WriteLine($"[ScenarioLoader] Adding {templatePassives.Count} passives from template: {string.Join(", ", templatePassives)}");
                        // These will be added to the combatant's PassiveIds later in SpawnCombatants
                    }
                }
                
                return resolved;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ScenarioLoader] Failed to resolve character build for {unit.Id}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Resolve equipment for a combatant from scenario unit definition.
        /// </summary>
        private void ResolveEquipment(Combatant combatant, ScenarioUnit unit)
        {
            if (_charRegistry == null)
            {
                Console.Error.WriteLine("[ScenarioLoader] CharacterDataRegistry not set, skipping equipment resolution");
                return;
            }
            
            // Get equipment loadout (use explicit or defaults)
            var loadout = new EquipmentLoadout
            {
                MainHandWeaponId = unit.MainHandWeaponId,
                OffHandWeaponId = unit.OffHandWeaponId,
                ArmorId = unit.ArmorId,
                ShieldId = unit.ShieldId
            };
            
            // If no equipment specified, get defaults
            if (string.IsNullOrEmpty(loadout.MainHandWeaponId) && 
                string.IsNullOrEmpty(loadout.ArmorId) && 
                string.IsNullOrEmpty(loadout.ShieldId))
            {
                loadout = GetDefaultEquipment(unit);
            }
            
            combatant.Equipment = loadout;
            
            // Resolve weapon references
            if (!string.IsNullOrEmpty(loadout.MainHandWeaponId))
            {
                combatant.MainHandWeapon = _charRegistry.GetWeapon(loadout.MainHandWeaponId);
                if (combatant.MainHandWeapon == null)
                    Console.Error.WriteLine($"[ScenarioLoader] Weapon not found: {loadout.MainHandWeaponId}");
            }
            
            if (!string.IsNullOrEmpty(loadout.OffHandWeaponId))
            {
                combatant.OffHandWeapon = _charRegistry.GetWeapon(loadout.OffHandWeaponId);
                if (combatant.OffHandWeapon == null)
                    Console.Error.WriteLine($"[ScenarioLoader] Off-hand weapon not found: {loadout.OffHandWeaponId}");
            }
            
            // Grant weapon actions from equipped weapons
            if (combatant.MainHandWeapon?.GrantedActionIds != null)
            {
                combatant.KnownActions ??= new List<string>();
                foreach (var actionId in combatant.MainHandWeapon.GrantedActionIds)
                {
                    if (!combatant.KnownActions.Contains(actionId))
                        combatant.KnownActions.Add(actionId);
                }
            }
            
            // Resolve armor reference
            if (!string.IsNullOrEmpty(loadout.ArmorId))
            {
                combatant.EquippedArmor = _charRegistry.GetArmor(loadout.ArmorId);
                if (combatant.EquippedArmor == null)
                    Console.Error.WriteLine($"[ScenarioLoader] Armor not found: {loadout.ArmorId}");
            }
            
            // Resolve shield
            if (!string.IsNullOrEmpty(loadout.ShieldId))
            {
                var shield = _charRegistry.GetArmor(loadout.ShieldId);
                if (shield != null && shield.Category == ArmorCategory.Shield)
                {
                    combatant.HasShield = true;
                }
                else
                {
                    Console.Error.WriteLine($"[ScenarioLoader] Shield not found or invalid: {loadout.ShieldId}");
                }
            }
            
            // Compute AC based on equipment
            if (combatant.Stats != null)
            {
                int finalAC;
                int dexMod = CombatantStats.GetModifier(combatant.Stats.Dexterity);
                
                if (combatant.EquippedArmor != null)
                {
                    // Wearing armor
                    int armorAC = combatant.EquippedArmor.BaseAC;
                    
                    if (combatant.EquippedArmor.MaxDexBonus.HasValue)
                    {
                        // Medium or heavy armor - cap dex bonus
                        finalAC = armorAC + Math.Min(dexMod, combatant.EquippedArmor.MaxDexBonus.Value);
                    }
                    else
                    {
                        // Light armor - full dex bonus
                        finalAC = armorAC + dexMod;
                    }
                    
                    // Add shield bonus
                    if (combatant.HasShield)
                        finalAC += 2;
                }
                else
                {
                    // Unarmored - check for Unarmored Defence features
                    bool hasUnarmoredDefence = combatant.ResolvedCharacter?.Features?.Any(f => 
                        string.Equals(f.Id, "unarmoured_defence", StringComparison.OrdinalIgnoreCase)) == true;
                    
                    if (hasUnarmoredDefence)
                    {
                        // Determine class for unarmored defence type
                        string primaryClass = unit.ClassLevels?.FirstOrDefault()?.ClassId?.ToLowerInvariant();
                        
                        if (primaryClass == "barbarian")
                        {
                            // Barbarian: 10 + DEX + CON
                            int conMod = CombatantStats.GetModifier(combatant.Stats.Constitution);
                            finalAC = 10 + dexMod + conMod;
                            
                            // Barbarian can use shield with Unarmored Defence
                            if (combatant.HasShield)
                                finalAC += 2;
                        }
                        else if (primaryClass == "monk")
                        {
                            // Monk: 10 + DEX + WIS (no shield allowed)
                            int wisMod = CombatantStats.GetModifier(combatant.Stats.Wisdom);
                            finalAC = 10 + dexMod + wisMod;
                            // Monk cannot benefit from shield with Unarmored Defence
                        }
                        else
                        {
                            // Fallback
                            finalAC = 10 + dexMod;
                            if (combatant.HasShield)
                                finalAC += 2;
                        }
                    }
                    else
                    {
                        // Default unarmored: 10 + DEX
                        finalAC = 10 + dexMod;
                        if (combatant.HasShield)
                            finalAC += 2;
                    }
                }
                
                combatant.Stats.BaseAC = finalAC;
            }
        }
        
        /// <summary>
        /// Get default equipment based on class or unit name.
        /// </summary>
        private EquipmentLoadout GetDefaultEquipment(ScenarioUnit unit)
        {
            var loadout = new EquipmentLoadout();
            
            // Determine primary class
            string primaryClass = unit.ClassLevels?.FirstOrDefault()?.ClassId?.ToLowerInvariant();
            
            if (primaryClass == null)
            {
                // Non-character-build units: use tags or name-based defaults
                string name = unit.Name?.ToLowerInvariant() ?? "";
                if (name.Contains("archer") || name.Contains("ranger"))
                {
                    loadout.MainHandWeaponId = "longbow";
                    loadout.ArmorId = "leather";
                }
                else if (name.Contains("wizard") || name.Contains("mage"))
                {
                    loadout.MainHandWeaponId = "quarterstaff";
                }
                else if (name.Contains("goblin"))
                {
                    loadout.MainHandWeaponId = "scimitar";
                    loadout.ArmorId = "leather";
                }
                else if (name.Contains("orc") || name.Contains("warrior"))
                {
                    loadout.MainHandWeaponId = "greataxe";
                    loadout.ArmorId = "hide";
                }
                else
                {
                    loadout.MainHandWeaponId = "club";
                }
                return loadout;
            }
            
            switch (primaryClass)
            {
                case "fighter":
                    loadout.MainHandWeaponId = "longsword";
                    loadout.ShieldId = "shield";
                    loadout.ArmorId = "chain_mail";
                    break;
                case "barbarian":
                    loadout.MainHandWeaponId = "greataxe";
                    // No armor - use Unarmored Defence
                    break;
                case "paladin":
                    loadout.MainHandWeaponId = "longsword";
                    loadout.ShieldId = "shield";
                    loadout.ArmorId = "chain_mail";
                    break;
                case "ranger":
                    loadout.MainHandWeaponId = "longbow";
                    loadout.ArmorId = "scale_mail";
                    break;
                case "rogue":
                    loadout.MainHandWeaponId = "rapier";
                    loadout.OffHandWeaponId = "dagger";
                    loadout.ArmorId = "leather";
                    break;
                case "monk":
                    // Unarmed by default
                    break;
                case "cleric":
                    loadout.MainHandWeaponId = "mace";
                    loadout.ShieldId = "shield";
                    loadout.ArmorId = "scale_mail";
                    break;
                case "wizard":
                    loadout.MainHandWeaponId = "quarterstaff";
                    break;
                case "sorcerer":
                    loadout.MainHandWeaponId = "dagger";
                    break;
                case "warlock":
                    loadout.MainHandWeaponId = "light_crossbow";
                    loadout.ArmorId = "leather";
                    break;
                case "bard":
                    loadout.MainHandWeaponId = "rapier";
                    loadout.ArmorId = "leather";
                    break;
                case "druid":
                    loadout.MainHandWeaponId = "scimitar";
                    loadout.ArmorId = "leather";
                    loadout.ShieldId = "shield";
                    break;
                default:
                    loadout.MainHandWeaponId = "club";
                    break;
            }
            return loadout;
        }
        
        /// <summary>
        /// Choose a specific fighting style based on equipped weapon.
        /// </summary>
        private static string ChooseFightingStyle(string weaponId)
        {
            var twoHandedWeapons = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "greataxe", "greatsword", "maul", "halberd", "glaive", "pike"
            };

            if (twoHandedWeapons.Contains(weaponId ?? ""))
                return "fighting_style_gwf";

            var rangedWeapons = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "longbow", "shortbow", "light_crossbow", "heavy_crossbow", "hand_crossbow"
            };

            if (rangedWeapons.Contains(weaponId ?? ""))
                return "fighting_style_defence";

            // One-handed melee weapons (longsword, rapier, mace, etc.) → Dueling
            return "fighting_style_dueling";
        }

        /// <summary>
        /// Create a default unarmed strike weapon for a combatant.
        /// In D&D 5e, all creatures can make unarmed strikes dealing 1 + STR mod bludgeoning.
        /// Monks and some others get better unarmed strikes via features.
        /// </summary>
        private static WeaponDefinition CreateUnarmedStrike(Combatant combatant)
        {
            int damageDie = 1; // Default: 1 + STR mod
            bool isMonk = string.Equals(combatant.ResolvedCharacter?.Sheet?.StartingClassId, "Monk", StringComparison.OrdinalIgnoreCase);
            if (isMonk)
            {
                int level = combatant.ResolvedCharacter?.Sheet?.TotalLevel ?? 1;
                damageDie = level switch
                {
                    >= 17 => 10,
                    >= 11 => 8,
                    >= 5 => 6,
                    _ => 4
                };
            }

            return new WeaponDefinition
            {
                Id = "unarmed_strike",
                Name = "Unarmed Strike",
                WeaponType = WeaponType.Club,  // Using Club as the closest equivalent
                Category = WeaponCategory.Simple,
                DamageType = DamageType.Bludgeoning,
                DamageDiceCount = 1,
                DamageDieFaces = damageDie,
                Properties = WeaponProperty.Light,
                NormalRange = 5,  // Melee
                Weight = 0
            };
        }
    }
}
