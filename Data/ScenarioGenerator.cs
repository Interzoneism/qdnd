using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Data.Backgrounds;
using QDND.Data.CharacterModel;

namespace QDND.Data
{
    public class CharacterGenerationOptions
    {
        public string UnitId { get; set; }
        public string DisplayName { get; set; }
        public Faction Faction { get; set; }
        public int Level { get; set; } = 3;
        public int Initiative { get; set; }
        public int InitiativeTiebreaker { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public string ForcedRaceId { get; set; }
        public string ForcedSubraceId { get; set; }
        public string ForcedClassId { get; set; }
        public string ForcedSubclassId { get; set; }
        public List<string> AbilityOverrides { get; set; }
        public bool ReplaceResolvedActions { get; set; }
    }

    public class ScenarioGenerator
    {
        private const string TestFixtureId = "test_dummy";
        private readonly CharacterDataRegistry _characterDataRegistry;
        private readonly Random _random;
        private readonly List<RaceDefinition> _races;
        private readonly List<ClassDefinition> _classes;

        public int Seed { get; }

        public ScenarioGenerator(CharacterDataRegistry characterDataRegistry, int seed)
        {
            _characterDataRegistry = characterDataRegistry ?? throw new ArgumentNullException(nameof(characterDataRegistry));
            Seed = seed;
            _random = new Random(seed);
            _races = _characterDataRegistry.GetAllRaces()
                .Where(race => !IsRandomFixtureDefinition(race?.Id))
                .ToList();
            _classes = _characterDataRegistry.GetAllClasses()
                .Where(classDef => !IsRandomFixtureDefinition(classDef?.Id))
                .ToList();

            if (_races.Count == 0) throw new InvalidOperationException("No races available in CharacterDataRegistry.");
            if (_classes.Count == 0) throw new InvalidOperationException("No classes available in CharacterDataRegistry.");
        }

        private static bool IsRandomFixtureDefinition(string id)
        {
            return string.Equals(id, TestFixtureId, StringComparison.OrdinalIgnoreCase);
        }

        public ScenarioDefinition GenerateRandomScenario(int team1Size, int team2Size, int level = 3)
        {
            if (team1Size <= 0) throw new ArgumentOutOfRangeException(nameof(team1Size), "Team size must be >= 1.");
            if (team2Size <= 0) throw new ArgumentOutOfRangeException(nameof(team2Size), "Team size must be >= 1.");
            level = Math.Clamp(level, 1, 12);

            var scenario = new ScenarioDefinition
            {
                Id = $"random_{team1Size}v{team2Size}_seed_{Seed}",
                Name = $"Random {team1Size}v{team2Size} Combat",
                Seed = Seed,
                Units = new List<ScenarioUnit>()
            };

            for (int i = 0; i < team1Size; i++)
            {
                scenario.Units.Add(CreateRandomUnit(new CharacterGenerationOptions
                {
                    UnitId = $"player_{i + 1}",
                    Faction = Faction.Player,
                    Level = level,
                    X = -4f,
                    Y = 0f,
                    Z = i * 2f
                }));
            }

            for (int i = 0; i < team2Size; i++)
            {
                scenario.Units.Add(CreateRandomUnit(new CharacterGenerationOptions
                {
                    UnitId = $"enemy_{i + 1}",
                    Faction = Faction.Hostile,
                    Level = level,
                    X = 4f,
                    Y = 0f,
                    Z = i * 2f
                }));
            }

            return scenario;
        }

        public ScenarioDefinition GenerateShortGameplayScenario(int level = 3)
        {
            level = Math.Clamp(level, 1, 12);

            var player = CreateRandomUnit(new CharacterGenerationOptions
            {
                UnitId = "player_1",
                DisplayName = "Player Challenger",
                Faction = Faction.Player,
                Level = level,
                X = -2f,
                Y = 0f,
                Z = 0f
            });

            var enemy = CreateRandomUnit(new CharacterGenerationOptions
            {
                UnitId = "enemy_1",
                DisplayName = "Enemy Challenger",
                Faction = Faction.Hostile,
                Level = level,
                X = 2f,
                Y = 0f,
                Z = 0f
            });

            if (player.Initiative == enemy.Initiative)
            {
                enemy.InitiativeTiebreaker = Math.Max(0, enemy.InitiativeTiebreaker - 1);
            }

            return new ScenarioDefinition
            {
                Id = $"ff_short_gameplay_1v1_seed_{Seed}",
                Name = $"FF Short Gameplay 1v1 (L{level})",
                Seed = Seed,
                Units = new List<ScenarioUnit> { player, enemy }
            };
        }

        public ScenarioDefinition GenerateActionTestScenario(string actionId, int level = 3, QDND.Combat.Actions.ActionRegistry actionRegistry = null)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                throw new ArgumentException("Action ID is required.", nameof(actionId));
            }

            level = Math.Clamp(level, 1, 12);
            string normalizedActionId = actionId.Trim();

            // Check if the action is melee so we can spawn units closer together
            bool isMeleeAction = false;
            bool isMeleeWeaponAction = false;
            ActionDefinition actionDef = actionRegistry?.GetAction(normalizedActionId);
            if (actionDef != null)
            {
                isMeleeAction = actionDef.Tags?.Contains("melee") == true ||
                                actionDef.AttackType == AttackType.MeleeWeapon ||
                                actionDef.AttackType == AttackType.MeleeSpell;
                isMeleeWeaponAction = isMeleeAction &&
                                     actionDef.Tags?.Contains("weapon_action") == true;
            }

            // Melee actions need units within melee range; ranged actions use wider spacing
            float testerX = isMeleeAction ? -0.5f : -1.5f;
            float targetX = isMeleeAction ? 0.5f : 1.5f;

            var tester = CreateRandomUnit(new CharacterGenerationOptions
            {
                UnitId = "action_tester",
                DisplayName = "Action Tester",
                Faction = Faction.Player,
                Level = level,
                Initiative = 99,
                InitiativeTiebreaker = 99,
                X = testerX,
                Y = 0f,
                Z = 0f,
                AbilityOverrides = new List<string> { normalizedActionId },
                ReplaceResolvedActions = true
            });

            // For melee weapon actions, assign a longsword so the tester has a weapon
            // that actually grants the action (e.g. lacerate, pommel_strike)
            if (isMeleeWeaponAction)
            {
                tester.MainHandWeaponId = "longsword";
            }

            var target = CreateRandomUnit(new CharacterGenerationOptions
            {
                UnitId = "action_target",
                DisplayName = "Action Target",
                Faction = Faction.Hostile,
                Level = level,
                Initiative = 1,
                InitiativeTiebreaker = 1,
                X = targetX,
                Y = 0f,
                Z = 0f,
                AbilityOverrides = new List<string> { "basic_attack" },
                ReplaceResolvedActions = true
            });

            tester.Tags ??= new List<string>();
            // Tag must match AIDecisionPipeline's "ability_test_actor:" prefix
            string testTag = $"ability_test_actor:{normalizedActionId}";
            if (!tester.Tags.Contains(testTag))
            {
                tester.Tags.Add(testTag);
            }

            target.Tags ??= new List<string>();
            if (!target.Tags.Contains("action_test_target"))
            {
                target.Tags.Add("action_test_target");
            }

            return new ScenarioDefinition
            {
                Id = $"ff_action_test_{SanitizeIdFragment(normalizedActionId)}_seed_{Seed}",
                Name = $"FF Action Test: {normalizedActionId}",
                Seed = Seed,
                Units = new List<ScenarioUnit> { tester, target }
            };
        }

        public ScenarioDefinition GenerateMultiActionTestScenario(List<string> actionIds, int level = 3, QDND.Combat.Actions.ActionRegistry actionRegistry = null)
        {
            if (actionIds == null)
                throw new ArgumentNullException(nameof(actionIds));
            if (actionIds.Count == 0 || actionIds.Count > 6)
                throw new ArgumentException("actionIds must have between 1 and 6 entries.", nameof(actionIds));

            level = Math.Clamp(level, 1, 12);

            // Pad to 6 slots with basic_attack
            var slots = new List<string>(actionIds);
            while (slots.Count < 6)
                slots.Add("basic_attack");

            var positions = new (float X, float Y, float Z)[]
            {
                (-0.5f, 0f, -0.5f),
                ( 0.5f, 0f, -0.5f),
                (-0.5f, 0f,  0.5f),
                ( 0.5f, 0f,  0.5f),
                (-0.5f, 0f,  0.0f),
                ( 0.5f, 0f,  0.0f),
            };

            int[] initiatives = { 99, 98, 97, 96, 95, 94 };

            var units = new List<ScenarioUnit>();

            for (int i = 0; i < 6; i++)
            {
                string actionId = slots[i];
                Faction faction = (i % 2 == 0) ? Faction.Player : Faction.Hostile;
                var pos = positions[i];

                var unit = CreateRandomUnit(new CharacterGenerationOptions
                {
                    UnitId = $"batch_unit_{i}",
                    DisplayName = $"Tester {i} ({actionId})",
                    Faction = faction,
                    Level = level,
                    Initiative = initiatives[i],
                    InitiativeTiebreaker = initiatives[i],
                    X = pos.X,
                    Y = pos.Y,
                    Z = pos.Z,
                    ForcedRaceId = "test_dummy",   // Requires test_dummy_race.json (loaded in DEBUG builds by BG3DataLoader)
                    ForcedClassId = "test_dummy",  // Requires test_dummy_class.json (loaded in DEBUG builds by BG3DataLoader)
                    AbilityOverrides = new List<string> { actionId },
                    ReplaceResolvedActions = true
                });

                AutoEquipWeaponForAction(unit, actionId, actionRegistry);

                unit.Tags ??= new List<string>();
                string testTag = $"ability_test_actor:{actionId}";
                if (!unit.Tags.Contains(testTag))
                    unit.Tags.Add(testTag);
                if (!unit.Tags.Contains("action_batch_member"))
                    unit.Tags.Add("action_batch_member");

                units.Add(unit);
            }

            return new ScenarioDefinition
            {
                Id = $"ff_action_batch_{actionIds.Count}_seed_{Seed}",
                Name = $"FF Action Batch Test ({actionIds.Count} actions)",
                Seed = Seed,
                Units = units
            };
        }

        private static void AutoEquipWeaponForAction(ScenarioUnit unit, string actionId, QDND.Combat.Actions.ActionRegistry actionRegistry)
        {
            if (actionRegistry == null)
            {
                unit.MainHandWeaponId = "longsword";
                return;
            }

            ActionDefinition action = actionRegistry.GetAction(actionId);
            if (action == null)
            {
                unit.MainHandWeaponId = "longsword";
                return;
            }

            var tags = action.Tags ?? new HashSet<string>();

            bool hasTag(string t) => tags.Any(tag => string.Equals(tag, t, StringComparison.OrdinalIgnoreCase));

            // Ranged weapons
            if (hasTag("ranged") || hasTag("bow") || action.AttackType == AttackType.RangedWeapon)
            {
                unit.MainHandWeaponId = "longbow";
                unit.OffHandWeaponId = null;
                unit.ShieldId = null;
                return;
            }

            if (hasTag("crossbow"))
            {
                unit.MainHandWeaponId = "light_crossbow";
                unit.OffHandWeaponId = null;
                unit.ShieldId = null;
                return;
            }

            // Two-handed melee weapons
            if (hasTag("two_handed") || hasTag("heavy"))
            {
                unit.MainHandWeaponId = "greataxe";
                unit.OffHandWeaponId = null;
                unit.ShieldId = null;
                return;
            }

            // Versatile / finesse melee
            if (hasTag("versatile") || hasTag("finesse"))
            {
                unit.MainHandWeaponId = "longsword";
                return;
            }

            // Generic melee weapon action
            if (hasTag("weapon_action") &&
                (action.AttackType == AttackType.MeleeWeapon || action.AttackType == AttackType.MeleeSpell))
            {
                unit.MainHandWeaponId = "longsword";
                return;
            }

            // Spells and non-weapon actions — leave equipment as-is
        }

        private ScenarioUnit CreateRandomUnit(CharacterGenerationOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.UnitId))
                throw new ArgumentException("UnitId is required.", nameof(options));

            int level = Math.Clamp(options.Level, 1, 12);
            var race = PickRace(options.ForcedRaceId);
            string subraceId = PickSubraceId(race, options.ForcedSubraceId);
            var classDef = PickClass(options.ForcedClassId);
            string subclassId = PickSubclassId(classDef, level, options.ForcedSubclassId);

            var baseScores = BuildBaseAbilityScores(classDef);
            AbilityType primary = ParseAbilityOrDefault(classDef.PrimaryAbility, AbilityType.Strength);
            AbilityType secondary = GetClassSecondary(classDef.Id);
            if (secondary == primary)
                secondary = AbilityType.Constitution;

            int dexterity = baseScores[AbilityType.Dexterity];
            int dexMod = CharacterSheet.GetModifier(dexterity);
            int initiative = options.Initiative != 0
                ? options.Initiative
                : Math.Clamp(10 + dexMod + _random.Next(-2, 5), 1, 30);

            string namePrefix = options.DisplayName;
            if (string.IsNullOrWhiteSpace(namePrefix))
            {
                namePrefix = options.Faction == Faction.Player ? "Player" : "Enemy";
            }

            var abilityOverrides = options.AbilityOverrides?
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim())
                .Distinct()
                .ToList();

            // Generate feats for characters level 4+
            var featIds = new List<string>();
            if (level >= 4)
            {
                featIds = SelectFeats(classDef, level, primary);
            }

            // Generate equipment
            var equipment = GenerateDefaultEquipment(classDef.Id, level);

            // Pick a random background
            var background = BackgroundData.All[_random.Next(BackgroundData.All.Count)];

            return new ScenarioUnit
            {
                Id = options.UnitId,
                Name = $"{namePrefix} - {race.Name} {classDef.Name}",
                Faction = options.Faction,
                Initiative = initiative,
                InitiativeTiebreaker = options.InitiativeTiebreaker != 0 ? options.InitiativeTiebreaker : dexterity,
                X = options.X,
                Y = options.Y,
                Z = options.Z,
                RaceId = race.Id,
                SubraceId = subraceId,
                ClassLevels = new List<ClassLevelEntry>
                {
                    new ClassLevelEntry
                    {
                        ClassId = classDef.Id,
                        SubclassId = subclassId,
                        Levels = level
                    }
                },
                BaseStrength = baseScores[AbilityType.Strength],
                BaseDexterity = dexterity,
                BaseConstitution = baseScores[AbilityType.Constitution],
                BaseIntelligence = baseScores[AbilityType.Intelligence],
                BaseWisdom = baseScores[AbilityType.Wisdom],
                BaseCharisma = baseScores[AbilityType.Charisma],
                AbilityBonus2 = primary.ToString(),
                AbilityBonus1 = secondary.ToString(),
                FeatIds = featIds,
                BackgroundId = background.Id,
                BackgroundSkills = new List<string>(background.SkillProficiencies),
                MainHandWeaponId = equipment.MainHandWeaponId,
                OffHandWeaponId = equipment.OffHandWeaponId,
                ArmorId = equipment.ArmorId,
                ShieldId = equipment.ShieldId,
                KnownActions = abilityOverrides,
                ReplaceResolvedActions = options.ReplaceResolvedActions,
                Tags = BuildTags(classDef, options.Faction, abilityOverrides != null && abilityOverrides.Count > 0)
            };
        }

        private RaceDefinition PickRace(string forcedRaceId)
        {
            if (!string.IsNullOrWhiteSpace(forcedRaceId))
            {
                var forced = _characterDataRegistry.GetRace(forcedRaceId.Trim());
                if (forced == null)
                {
                    throw new InvalidOperationException($"Forced race not found: {forcedRaceId}");
                }
                return forced;
            }

            return _races[_random.Next(_races.Count)];
        }

        private string PickSubraceId(RaceDefinition race, string forcedSubraceId)
        {
            if (race == null) return null;
            var subraces = race.Subraces ?? new List<SubraceDefinition>();

            if (!string.IsNullOrWhiteSpace(forcedSubraceId))
            {
                string target = forcedSubraceId.Trim();
                if (subraces.Any(s => string.Equals(s.Id, target, StringComparison.OrdinalIgnoreCase)))
                {
                    return target;
                }
                throw new InvalidOperationException($"Forced subrace '{forcedSubraceId}' is not valid for race '{race.Id}'.");
            }

            if (subraces.Count == 0)
            {
                return null;
            }

            // Include a small chance to use the base race without subrace.
            int index = _random.Next(subraces.Count + 1);
            if (index == subraces.Count)
            {
                return null;
            }

            return subraces[index].Id;
        }

        private ClassDefinition PickClass(string forcedClassId)
        {
            if (!string.IsNullOrWhiteSpace(forcedClassId))
            {
                var forced = _characterDataRegistry.GetClass(forcedClassId.Trim());
                if (forced == null)
                {
                    throw new InvalidOperationException($"Forced class not found: {forcedClassId}");
                }
                return forced;
            }

            return _classes[_random.Next(_classes.Count)];
        }

        private string PickSubclassId(ClassDefinition classDef, int level, string forcedSubclassId)
        {
            if (classDef?.Subclasses == null || classDef.Subclasses.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(forcedSubclassId))
                {
                    throw new InvalidOperationException($"Class '{classDef?.Id}' does not have subclasses.");
                }
                return null;
            }

            if (level < classDef.SubclassLevel)
            {
                if (!string.IsNullOrWhiteSpace(forcedSubclassId))
                {
                    throw new InvalidOperationException(
                        $"Class '{classDef.Id}' cannot select subclass before level {classDef.SubclassLevel}.");
                }
                return null;
            }

            if (!string.IsNullOrWhiteSpace(forcedSubclassId))
            {
                string target = forcedSubclassId.Trim();
                if (classDef.Subclasses.Any(s => string.Equals(s.Id, target, StringComparison.OrdinalIgnoreCase)))
                {
                    return target;
                }
                throw new InvalidOperationException($"Subclass '{forcedSubclassId}' is not valid for class '{classDef.Id}'.");
            }

            return classDef.Subclasses[_random.Next(classDef.Subclasses.Count)].Id;
        }

        private Dictionary<AbilityType, int> BuildBaseAbilityScores(ClassDefinition classDef)
        {
            var scores = new Dictionary<AbilityType, int>();
            var available = new List<int> { 15, 14, 13, 12, 10, 8 };

            void Assign(AbilityType ability)
            {
                if (scores.ContainsKey(ability)) return;
                scores[ability] = available[0];
                available.RemoveAt(0);
            }

            AbilityType primary = ParseAbilityOrDefault(classDef?.PrimaryAbility, AbilityType.Strength);
            
            AbilityType secondary = GetClassSecondary(classDef?.Id);
            
            // If primary is already CON, make secondary DEX
            if (primary == secondary)
            {
                secondary = AbilityType.Dexterity;
            }

            Assign(primary);
            Assign(secondary);

            var remainingAbilities = Enum.GetValues(typeof(AbilityType))
                .Cast<AbilityType>()
                .Where(a => !scores.ContainsKey(a))
                .OrderBy(_ => _random.Next())
                .ToList();

            foreach (var action in remainingAbilities)
            {
                Assign(action);
            }

            return scores;
        }

        private static AbilityType GetClassSecondary(string classId)
        {
            return classId?.ToLowerInvariant() switch
            {
                "bard" => AbilityType.Dexterity,      // rapier/AC
                "paladin" => AbilityType.Charisma,     // aura/saves
                "ranger" => AbilityType.Wisdom,        // spell DC
                "monk" => AbilityType.Wisdom,          // Ki DC/unarmored AC
                _ => AbilityType.Constitution,         // Fighter, Barbarian, Rogue, all casters
            };
        }

        private static AbilityType ParseAbilityOrDefault(string value, AbilityType fallback)
        {
            if (Enum.TryParse<AbilityType>(value, true, out var parsed))
            {
                return parsed;
            }
            return fallback;
        }

        private static List<string> BuildTags(ClassDefinition classDef, Faction faction, bool hasAbilityOverride)
        {
            var tags = new List<string>();

            if (faction == Faction.Player) tags.Add("player");
            if (faction == Faction.Hostile) tags.Add("enemy");

            if (!string.IsNullOrWhiteSpace(classDef?.SpellcastingAbility))
            {
                tags.Add("caster");
            }
            else
            {
                tags.Add("martial");
            }

            if (string.Equals(classDef?.PrimaryAbility, "Dexterity", StringComparison.OrdinalIgnoreCase))
            {
                tags.Add("ranged");
            }
            else
            {
                tags.Add("melee");
            }

            if (hasAbilityOverride)
            {
                tags.Add("ability_override");
            }

            return tags.Distinct().ToList();
        }

        private static string SanitizeIdFragment(string value)
        {
            var chars = (value ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '_')
                .ToArray();
            return string.IsNullOrWhiteSpace(new string(chars)) ? "action" : new string(chars);
        }

        /// <summary>
        /// Generate appropriate starting equipment for a class.
        /// </summary>
        private (string MainHandWeaponId, string OffHandWeaponId, string ArmorId, string ShieldId) GenerateDefaultEquipment(string classId, int level)
        {
            string mainHand = null, offHand = null, armor = null, shield = null;
            string normalizedClass = classId?.ToLowerInvariant() ?? "";

            switch (normalizedClass)
            {
                case "fighter":
                    mainHand = "longsword";
                    shield = "shield";
                    armor = level >= 4 ? "splint" : "chain_mail";
                    break;
                case "barbarian":
                    mainHand = "greataxe";
                    // No armor - use Unarmored Defence
                    break;
                case "paladin":
                    mainHand = "longsword";
                    shield = "shield";
                    armor = level >= 4 ? "splint" : "chain_mail";
                    break;
                case "ranger":
                    mainHand = "longbow";
                    armor = "studded_leather";
                    break;
                case "rogue":
                    mainHand = "rapier";
                    offHand = "dagger";
                    armor = "leather";
                    break;
                case "monk":
                    mainHand = "quarterstaff";
                    // No armor - use Unarmored Defence
                    break;
                case "cleric":
                    mainHand = "mace";
                    shield = "shield";
                    armor = "chain_mail";
                    break;
                case "druid":
                    mainHand = "scimitar";
                    shield = "shield";
                    armor = "leather";
                    break;
                case "wizard":
                    mainHand = "quarterstaff";
                    // No armor
                    break;
                case "sorcerer":
                    mainHand = "dagger";
                    // No armor (relies on Mage Armor or Draconic Resilience)
                    break;
                case "warlock":
                    mainHand = "light_crossbow";
                    armor = "leather";
                    break;
                case "bard":
                    mainHand = "rapier";
                    armor = "leather";
                    break;
                default:
                    mainHand = "club";
                    break;
            }

            return (mainHand, offHand, armor, shield);
        }

        /// <summary>
        /// Select feats for a character based on class and level.
        /// </summary>
        private List<string> SelectFeats(ClassDefinition classDef, int level, AbilityType primaryAbility)
        {
            var feats = new List<string>();
            var featLevels = classDef.FeatLevels ?? new List<int> { 4, 8, 12 };
            
            // Count how many feats this character should have
            int featCount = featLevels.Count(l => l <= level);
            if (featCount == 0) return feats;

            // Get all available feats (exclude ASI since it needs special handling)
            var allFeats = _characterDataRegistry.GetAllFeats()
                .Where(f => !f.IsASI)
                .ToList();
            if (allFeats.Count == 0) return feats;

            // Determine character archetype for feat selection heuristics
            bool isCaster = !string.IsNullOrEmpty(classDef.SpellcastingAbility);
            bool isMartial = classDef.Id.Equals("fighter", StringComparison.OrdinalIgnoreCase) ||
                             classDef.Id.Equals("barbarian", StringComparison.OrdinalIgnoreCase) ||
                             classDef.Id.Equals("paladin", StringComparison.OrdinalIgnoreCase) ||
                             classDef.Id.Equals("ranger", StringComparison.OrdinalIgnoreCase) ||
                             classDef.Id.Equals("monk", StringComparison.OrdinalIgnoreCase);
            bool isRanged = primaryAbility == AbilityType.Dexterity && 
                           (classDef.Id.Equals("ranger", StringComparison.OrdinalIgnoreCase) || 
                            classDef.Id.Equals("fighter", StringComparison.OrdinalIgnoreCase));
            bool isMelee = isMartial && !isRanged;

            for (int i = 0; i < featCount; i++)
            {
                // Select appropriate feat based on archetype
                string selectedFeat = null;
                var candidates = new List<string>();

                if (isCaster)
                {
                    candidates = new List<string> { "war_caster", "resilient", "alert", "tough" };
                }
                else if (isMelee)
                {
                    candidates = new List<string> { "great_weapon_master", "sentinel", "tough", "alert", "polearm_master" };
                }
                else if (isRanged)
                {
                    candidates = new List<string> { "sharpshooter", "crossbow_expert", "alert", "tough" };
                }
                else
                {
                    candidates = new List<string> { "alert", "tough", "mobile" };
                }

                // Filter candidates to those that exist and aren't already selected
                var validCandidates = candidates
                    .Where(c => allFeats.Any(f => f.Id.Equals(c, StringComparison.OrdinalIgnoreCase)))
                    .Where(c => !feats.Contains(c))
                    .ToList();

                if (validCandidates.Count > 0)
                {
                    selectedFeat = validCandidates[_random.Next(validCandidates.Count)];
                    feats.Add(selectedFeat);
                }
                else
                {
                    // Fallback to any available feat not already selected
                    var anyAvailable = allFeats
                        .Select(f => f.Id)
                        .Where(id => !feats.Contains(id))
                        .ToList();
                    if (anyAvailable.Count > 0)
                    {
                        feats.Add(anyAvailable[_random.Next(anyAvailable.Count)]);
                    }
                }
            }

            return feats;
        }
    }
}
