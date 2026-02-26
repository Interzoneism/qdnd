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
        private static readonly string[] MetamagicOptionIds =
        {
            "careful", "distant", "empowered", "extended",
            "heightened", "quickened", "subtle", "twinned"
        };
        private static readonly string[] ElementalAdeptTypes = { "fire", "cold", "lightning", "thunder", "acid" };
        private static readonly string[] MagicInitiateCantripPool =
        {
            "fire_bolt", "ray_of_frost", "shocking_grasp", "acid_splash",
            "blade_ward", "dancing_lights", "friends", "light",
            "mage_hand", "minor_illusion", "poison_spray", "true_strike"
        };
        private static readonly string[] MagicInitiateSpellPool =
        {
            "magic_missile", "shield", "chromatic_orb", "burning_hands",
            "charm_person", "expeditious_retreat", "false_life", "fog_cloud",
            "ice_knife", "mage_armor", "sleep", "thunderwave", "witch_bolt",
            "cure_wounds", "healing_word", "guiding_bolt", "bless", "bane"
        };

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

        public ScenarioUnit GenerateRandomUnit(CharacterGenerationOptions options)
        {
            return CreateRandomUnit(options);
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
            AbilityType primary = ParseAbilityOrDefault(classDef.PrimaryAbility, AbilityType.Strength);
            AbilityType secondary = GetClassSecondary(classDef.Id);
            if (secondary == primary)
                secondary = AbilityType.Constitution;

            string namePrefix = options.DisplayName;
            if (string.IsNullOrWhiteSpace(namePrefix))
            {
                namePrefix = options.Faction == Faction.Player ? "Player" : "Enemy";
            }
            string displayName = $"{namePrefix} - {race.Name} {classDef.Name}";

            var baseScores = BuildBaseAbilityScores(classDef);
            var (abilityBonus2, abilityBonus1) = PickRacialBonuses(baseScores, primary, secondary);

            var background = BackgroundData.All[_random.Next(BackgroundData.All.Count)];
            var (sheet, subclassId) = BuildLeveledSheet(
                displayName,
                race,
                subraceId,
                classDef,
                level,
                options.ForcedSubclassId,
                baseScores,
                abilityBonus2,
                abilityBonus1,
                background);

            var resolvedPreview = new CharacterResolver(_characterDataRegistry).Resolve(sheet);
            int dexterity = resolvedPreview.AbilityScores.TryGetValue(AbilityType.Dexterity, out int finalDex)
                ? finalDex
                : sheet.BaseDexterity;
            int dexMod = CharacterSheet.GetModifier(dexterity);
            int initiative = options.Initiative != 0
                ? options.Initiative
                : Math.Clamp(10 + dexMod + _random.Next(-2, 5), 1, 30);

            var abilityOverrides = options.AbilityOverrides?
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim())
                .Distinct()
                .ToList();

            // Generate equipment
            var equipment = GenerateDefaultEquipment(classDef, level);

            return new ScenarioUnit
            {
                Id = options.UnitId,
                Name = displayName,
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
                AbilityBonus2 = abilityBonus2.ToString(),
                AbilityBonus1 = abilityBonus1.ToString(),
                FeatIds = new List<string>(sheet.FeatIds),
                FeatChoices = new Dictionary<string, Dictionary<string, string>>(sheet.FeatChoices, StringComparer.OrdinalIgnoreCase),
                AbilityScoreImprovements = new Dictionary<string, int>(sheet.AbilityScoreImprovements, StringComparer.OrdinalIgnoreCase),
                MetamagicIds = new List<string>(sheet.MetamagicIds),
                InvocationIds = new List<string>(sheet.InvocationIds),
                BackgroundId = background.Id,
                BackgroundSkills = new List<string>(background.SkillProficiencies),
                MainHandWeaponId = equipment.MainHandWeaponId,
                OffHandWeaponId = equipment.OffHandWeaponId,
                ArmorId = equipment.ArmorId,
                ShieldId = equipment.ShieldId,
                KnownActions = abilityOverrides,
                ReplaceResolvedActions = options.ReplaceResolvedActions,
                Tags = BuildTags(classDef, subclassId, options.Faction, abilityOverrides != null && abilityOverrides.Count > 0)
            };
        }

        private (CharacterSheet Sheet, string SubclassId) BuildLeveledSheet(
            string name,
            RaceDefinition race,
            string subraceId,
            ClassDefinition classDef,
            int level,
            string forcedSubclassId,
            Dictionary<AbilityType, int> baseScores,
            AbilityType abilityBonus2,
            AbilityType abilityBonus1,
            BackgroundEntry background)
        {
            var sheet = new CharacterSheet
            {
                Name = name,
                RaceId = race?.Id,
                SubraceId = subraceId,
                BaseStrength = baseScores[AbilityType.Strength],
                BaseDexterity = baseScores[AbilityType.Dexterity],
                BaseConstitution = baseScores[AbilityType.Constitution],
                BaseIntelligence = baseScores[AbilityType.Intelligence],
                BaseWisdom = baseScores[AbilityType.Wisdom],
                BaseCharisma = baseScores[AbilityType.Charisma],
                AbilityBonus2 = abilityBonus2.ToString(),
                AbilityBonus1 = abilityBonus1.ToString(),
                BackgroundId = background?.Id,
                BackgroundSkills = background?.SkillProficiencies != null
                    ? new List<string>(background.SkillProficiencies)
                    : new List<string>()
            };

            string subclassId = null;
            bool subclassChoiceResolved = false;
            if (!string.IsNullOrWhiteSpace(forcedSubclassId) && level < classDef.SubclassLevel)
            {
                throw new InvalidOperationException(
                    $"Class '{classDef.Id}' cannot select subclass before level {classDef.SubclassLevel}.");
            }

            var featLevels = classDef.FeatLevels ?? new List<int> { 4, 8, 12 };
            for (int currentLevel = 1; currentLevel <= level; currentLevel++)
            {
                if (!subclassChoiceResolved && currentLevel >= classDef.SubclassLevel)
                {
                    subclassId = PickSubclassId(classDef, currentLevel, forcedSubclassId);
                    subclassChoiceResolved = true;
                }

                sheet.ClassLevels.Add(new ClassLevel(classDef.Id, subclassId));
                LevelProgression progression = null;
                classDef.LevelTable?.TryGetValue(currentLevel.ToString(), out progression);

                ApplyMetamagicChoicesForLevel(sheet, progression);
                ApplyInvocationChoicesForLevel(sheet, progression);

                if (featLevels.Contains(currentLevel))
                {
                    ApplyFeatOrAsiChoice(sheet, classDef, primary: ParseAbilityOrDefault(classDef.PrimaryAbility, AbilityType.Strength), secondary: GetClassSecondary(classDef.Id));
                }
            }

            return (sheet, subclassId);
        }

        private (AbilityType PlusTwo, AbilityType PlusOne) PickRacialBonuses(
            Dictionary<AbilityType, int> baseScores,
            AbilityType primary,
            AbilityType secondary)
        {
            var allAbilities = Enum.GetValues(typeof(AbilityType)).Cast<AbilityType>().ToList();

            AbilityType plusTwo;
            if (_random.NextDouble() < 0.7)
            {
                plusTwo = primary;
            }
            else
            {
                var plusTwoCandidates = allAbilities
                    .Where(a => a != primary)
                    .OrderByDescending(a => baseScores[a])
                    .ThenBy(_ => _random.Next())
                    .Take(3)
                    .ToList();
                plusTwo = plusTwoCandidates.Count > 0 ? plusTwoCandidates[_random.Next(plusTwoCandidates.Count)] : primary;
            }

            AbilityType plusOne;
            var remaining = allAbilities.Where(a => a != plusTwo).ToList();
            if (_random.NextDouble() < 0.6 && remaining.Contains(secondary))
            {
                plusOne = secondary;
            }
            else
            {
                var plusOneCandidates = remaining
                    .OrderByDescending(a => baseScores[a])
                    .ThenBy(_ => _random.Next())
                    .Take(4)
                    .ToList();
                plusOne = plusOneCandidates.Count > 0 ? plusOneCandidates[_random.Next(plusOneCandidates.Count)] : remaining[0];
            }

            return (plusTwo, plusOne);
        }

        private void ApplyMetamagicChoicesForLevel(CharacterSheet sheet, LevelProgression progression)
        {
            if (sheet == null || progression?.Features == null || progression.Features.Count == 0)
                return;

            int choices = 0;
            foreach (var feature in progression.Features)
            {
                if (feature?.Id == null)
                    continue;

                if (string.Equals(feature.Id, "metamagic", StringComparison.OrdinalIgnoreCase))
                    choices += 2;
                else if (feature.Id.StartsWith("metamagic_", StringComparison.OrdinalIgnoreCase))
                    choices += 1;
            }

            if (choices <= 0)
                return;

            var available = MetamagicOptionIds
                .Where(id => !sheet.MetamagicIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                .ToList();

            while (choices > 0 && available.Count > 0)
            {
                int index = _random.Next(available.Count);
                sheet.MetamagicIds.Add(available[index]);
                available.RemoveAt(index);
                choices--;
            }
        }

        private void ApplyInvocationChoicesForLevel(CharacterSheet sheet, LevelProgression progression)
        {
            if (sheet == null || progression?.InvocationsKnown == null)
                return;

            int targetCount = Math.Max(0, progression.InvocationsKnown.Value);
            if (sheet.InvocationIds.Count > targetCount)
            {
                sheet.InvocationIds = sheet.InvocationIds.Take(targetCount).ToList();
                return;
            }

            var invocationPool = _characterDataRegistry.GetAllFeats()
                .Where(IsInvocationFeat)
                .Select(f => f.Id)
                .Where(id => !sheet.InvocationIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                .ToList();

            while (sheet.InvocationIds.Count < targetCount && invocationPool.Count > 0)
            {
                int index = _random.Next(invocationPool.Count);
                sheet.InvocationIds.Add(invocationPool[index]);
                invocationPool.RemoveAt(index);
            }
        }

        private static bool IsInvocationFeat(FeatDefinition feat)
        {
            if (feat == null)
                return false;

            if (feat.Features == null)
                return false;

            return feat.Features.Any(feature =>
                feature?.Tags?.Any(tag => string.Equals(tag, "invocation", StringComparison.OrdinalIgnoreCase)) == true
                || (feature?.Source?.IndexOf("invocation", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
        }

        private void ApplyFeatOrAsiChoice(CharacterSheet sheet, ClassDefinition classDef, AbilityType primary, AbilityType secondary)
        {
            if (sheet == null)
                return;

            if (primary == secondary)
                secondary = AbilityType.Constitution;

            var snapshot = new CharacterResolver(_characterDataRegistry).Resolve(sheet);
            bool chooseAsi = ShouldChooseAsi(snapshot, primary, secondary);

            if (!chooseAsi)
            {
                var selectedFeat = PickFeatForSnapshot(sheet, classDef, snapshot, primary);
                if (selectedFeat != null)
                {
                    sheet.FeatIds.Add(selectedFeat.Id);
                    var choices = BuildFeatChoices(selectedFeat.Id, snapshot, primary, secondary);
                    if (choices.Count > 0)
                    {
                        sheet.FeatChoices[selectedFeat.Id] = choices;
                    }
                    return;
                }
            }

            ApplyAbilityImprovement(sheet, snapshot, primary, secondary);
        }

        private bool ShouldChooseAsi(ResolvedCharacter snapshot, AbilityType primary, AbilityType secondary)
        {
            int primaryScore = snapshot.AbilityScores.TryGetValue(primary, out var p) ? p : 10;
            int secondaryScore = snapshot.AbilityScores.TryGetValue(secondary, out var s) ? s : 10;

            if (primaryScore >= 20 && secondaryScore >= 20)
                return false;

            double chance = primaryScore switch
            {
                <= 17 => 0.65,
                <= 19 => 0.45,
                _ => 0.3
            };

            if (secondaryScore >= 20)
                chance += 0.1;

            return _random.NextDouble() < chance;
        }

        private void ApplyAbilityImprovement(CharacterSheet sheet, ResolvedCharacter snapshot, AbilityType primary, AbilityType secondary)
        {
            sheet.FeatIds.Add("ability_improvement");
            var abilityOrder = new List<AbilityType> { primary, secondary, AbilityType.Constitution, AbilityType.Dexterity, AbilityType.Wisdom, AbilityType.Charisma, AbilityType.Intelligence, AbilityType.Strength };

            int firstScore = snapshot.AbilityScores.TryGetValue(primary, out int score) ? score : 10;
            if (firstScore <= 18)
            {
                AddAbilityImprovement(sheet, primary, 2);
                return;
            }

            var improvable = abilityOrder
                .Distinct()
                .Where(a => snapshot.AbilityScores.TryGetValue(a, out int value) && value < 20)
                .ToList();

            if (improvable.Count == 0)
                return;

            if (improvable.Count == 1)
            {
                AddAbilityImprovement(sheet, improvable[0], 1);
                return;
            }

            int bestScore = snapshot.AbilityScores[improvable[0]];
            if (bestScore <= 18 || _random.NextDouble() < 0.55)
            {
                AddAbilityImprovement(sheet, improvable[0], 2);
            }
            else
            {
                AddAbilityImprovement(sheet, improvable[0], 1);
                AddAbilityImprovement(sheet, improvable[1], 1);
            }
        }

        private static void AddAbilityImprovement(CharacterSheet sheet, AbilityType ability, int amount)
        {
            if (sheet == null || amount <= 0)
                return;

            string key = ability.ToString();
            if (!sheet.AbilityScoreImprovements.ContainsKey(key))
            {
                sheet.AbilityScoreImprovements[key] = 0;
            }

            sheet.AbilityScoreImprovements[key] += amount;
        }

        private FeatDefinition PickFeatForSnapshot(
            CharacterSheet sheet,
            ClassDefinition classDef,
            ResolvedCharacter snapshot,
            AbilityType primaryAbility)
        {
            var selectedIds = new HashSet<string>(sheet.FeatIds ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var candidates = _characterDataRegistry.GetAllFeats()
                .Where(f => IsSelectableGeneralFeat(f))
                .Where(f => !selectedIds.Contains(f.Id))
                .Where(f => MeetsFeatPrerequisites(f, classDef, snapshot))
                .ToList();

            if (candidates.Count == 0)
                return null;

            var preferredIds = GetArchetypeFeatPreferences(classDef, primaryAbility);
            var preferred = candidates
                .Where(f => preferredIds.Contains(f.Id, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (preferred.Count > 0 && _random.NextDouble() < 0.75)
                return preferred[_random.Next(preferred.Count)];

            return candidates[_random.Next(candidates.Count)];
        }

        private static bool IsSelectableGeneralFeat(FeatDefinition feat)
        {
            if (feat == null || string.IsNullOrWhiteSpace(feat.Id))
                return false;

            if (string.Equals(feat.Id, "ability_improvement", StringComparison.OrdinalIgnoreCase))
                return false;

            if (feat.IsASI)
                return false;

            return !IsInvocationFeat(feat) && !IsRandomFixtureDefinition(feat.Id);
        }

        private static IReadOnlyList<string> GetArchetypeFeatPreferences(ClassDefinition classDef, AbilityType primaryAbility)
        {
            bool isCaster = !string.IsNullOrWhiteSpace(classDef?.SpellcastingAbility);
            bool isMartial = classDef?.Id != null && (
                classDef.Id.Equals("fighter", StringComparison.OrdinalIgnoreCase) ||
                classDef.Id.Equals("barbarian", StringComparison.OrdinalIgnoreCase) ||
                classDef.Id.Equals("paladin", StringComparison.OrdinalIgnoreCase) ||
                classDef.Id.Equals("ranger", StringComparison.OrdinalIgnoreCase) ||
                classDef.Id.Equals("monk", StringComparison.OrdinalIgnoreCase));
            bool isRanged = primaryAbility == AbilityType.Dexterity &&
                            (classDef?.Id?.Equals("ranger", StringComparison.OrdinalIgnoreCase) == true ||
                             classDef?.Id?.Equals("fighter", StringComparison.OrdinalIgnoreCase) == true);

            if (isCaster)
            {
                return new[] { "war_caster", "resilient", "alert", "lucky", "elemental_adept", "spell_sniper", "tough" };
            }

            if (isMartial && !isRanged)
            {
                return new[] { "great_weapon_master", "sentinel", "polearm_master", "tough", "alert", "savage_attacker", "shield_master" };
            }

            if (isRanged)
            {
                return new[] { "sharpshooter", "crossbow_expert", "alert", "mobile", "lucky", "piercer" };
            }

            return new[] { "alert", "tough", "mobile", "lucky", "resilient", "athlete" };
        }

        private static bool MeetsFeatPrerequisites(FeatDefinition feat, ClassDefinition classDef, ResolvedCharacter snapshot)
        {
            var prereq = feat?.Prerequisites;
            if (prereq == null)
                return true;

            if (prereq.MinLevel > 0 && snapshot?.Sheet?.TotalLevel < prereq.MinLevel)
                return false;

            if (prereq.RequiresSpellcasting)
            {
                bool hasSpellcastingClass = !string.IsNullOrWhiteSpace(classDef?.SpellcastingAbility);
                bool hasSpellSlots = snapshot?.Resources?.Any(kvp =>
                    kvp.Key.StartsWith("spell_slot_", StringComparison.Ordinal) && kvp.Value > 0) == true
                    || (snapshot?.Resources?.TryGetValue("pact_slots", out int pactSlots) == true && pactSlots > 0);

                if (!hasSpellcastingClass && !hasSpellSlots)
                    return false;
            }

            if (prereq.MinAbilityScores != null)
            {
                foreach (var (abilityName, minValue) in prereq.MinAbilityScores)
                {
                    if (!Enum.TryParse<AbilityType>(abilityName, true, out var ability))
                        continue;

                    if (snapshot.AbilityScores.TryGetValue(ability, out int value) && value < minValue)
                        return false;
                }
            }

            if (prereq.RequiredArmorProficiencies != null)
            {
                foreach (var armor in prereq.RequiredArmorProficiencies)
                {
                    if (!Enum.TryParse<ArmorCategory>(armor, true, out var category))
                        continue;
                    if (!snapshot.Proficiencies.ArmorCategories.Contains(category))
                        return false;
                }
            }

            if (prereq.RequiredWeaponProficiencies != null)
            {
                foreach (var weapon in prereq.RequiredWeaponProficiencies)
                {
                    if (!Enum.TryParse<WeaponCategory>(weapon, true, out var category))
                        continue;
                    if (!snapshot.Proficiencies.WeaponCategories.Contains(category))
                        return false;
                }
            }

            return true;
        }

        private Dictionary<string, string> BuildFeatChoices(
            string featId,
            ResolvedCharacter snapshot,
            AbilityType primary,
            AbilityType secondary)
        {
            var choices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string lower = featId?.ToLowerInvariant() ?? string.Empty;

            switch (lower)
            {
                case "resilient":
                {
                    var preferred = new[] { AbilityType.Constitution, primary, AbilityType.Wisdom, AbilityType.Dexterity, secondary };
                    AbilityType selected = preferred
                        .Distinct()
                        .FirstOrDefault(ability => !snapshot.Proficiencies.SavingThrows.Contains(ability));
                    if (!Enum.IsDefined(typeof(AbilityType), selected))
                    {
                        selected = AbilityType.Constitution;
                    }
                    choices["ability"] = selected.ToString();
                    break;
                }
                case "elemental_adept":
                    choices["damageType"] = ElementalAdeptTypes[_random.Next(ElementalAdeptTypes.Length)];
                    break;
                case "skilled":
                {
                    var skillPool = Enum.GetValues(typeof(Skill))
                        .Cast<Skill>()
                        .Where(skill => !snapshot.Proficiencies.Skills.Contains(skill))
                        .OrderBy(_ => _random.Next())
                        .Take(3)
                        .ToList();
                    for (int i = 0; i < skillPool.Count; i++)
                    {
                        choices[$"skill{i + 1}"] = skillPool[i].ToString();
                    }
                    break;
                }
                case "athlete":
                case "lightly_armoured":
                case "moderately_armoured":
                case "tavern_brawler":
                case "weapon_master":
                    choices["ability"] = ChooseFeatAbility(primary, secondary);
                    break;
            }

            if (lower.StartsWith("magic_initiate", StringComparison.OrdinalIgnoreCase))
            {
                var cantrips = MagicInitiateCantripPool
                    .OrderBy(_ => _random.Next())
                    .Take(2)
                    .ToList();
                for (int i = 0; i < cantrips.Count; i++)
                {
                    choices[$"cantrip{i + 1}"] = cantrips[i];
                }
                choices["spell1"] = MagicInitiateSpellPool[_random.Next(MagicInitiateSpellPool.Length)];
            }

            return choices;
        }

        private string ChooseFeatAbility(AbilityType primary, AbilityType secondary)
        {
            var candidates = new List<AbilityType> { primary, secondary, AbilityType.Strength, AbilityType.Dexterity };
            var distinct = candidates.Distinct().ToList();
            AbilityType selected = distinct[_random.Next(distinct.Count)];

            if (selected != AbilityType.Strength && selected != AbilityType.Dexterity && _random.NextDouble() < 0.8)
            {
                selected = _random.NextDouble() < 0.5 ? AbilityType.Strength : AbilityType.Dexterity;
            }

            return selected.ToString();
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

        private static List<string> BuildTags(ClassDefinition classDef, string subclassId, Faction faction, bool hasAbilityOverride)
        {
            var tags = new List<string>();

            if (faction == Faction.Player) tags.Add("player");
            if (faction == Faction.Hostile) tags.Add("enemy");

            bool subclassCaster = classDef?.Subclasses?.Any(sub =>
                string.Equals(sub.Id, subclassId, StringComparison.OrdinalIgnoreCase)
                && sub.SpellcasterModifier > 0) == true;

            if (!string.IsNullOrWhiteSpace(classDef?.SpellcastingAbility) || subclassCaster)
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
        private (string MainHandWeaponId, string OffHandWeaponId, string ArmorId, string ShieldId) GenerateDefaultEquipment(ClassDefinition classDef, int level)
        {
            string mainHand = null, offHand = null, armor = null, shield = null;
            string normalizedClass = classDef?.Id?.ToLowerInvariant() ?? "";

            bool hasSimpleWeapons = HasWeaponCategory(classDef, "Simple");
            bool hasMartialWeapons = HasWeaponCategory(classDef, "Martial");
            bool hasShieldProficiency = HasArmorCategory(classDef, "Shield");
            bool hasLightArmor = HasArmorCategory(classDef, "Light");
            bool hasMediumArmor = HasArmorCategory(classDef, "Medium");
            bool hasHeavyArmor = HasArmorCategory(classDef, "Heavy");

            switch (normalizedClass)
            {
                case "fighter":
                    mainHand = hasMartialWeapons
                        ? PickRandomWeapon(new[] { "longsword", "battleaxe", "warhammer", "morningstar", "greatsword", "halberd", "glaive", "rapier" })
                        : PickRandomWeapon(new[] { "mace", "quarterstaff" });
                    if (IsTwoHandedWeapon(mainHand))
                    {
                        shield = null;
                    }
                    else
                    {
                        shield = hasShieldProficiency ? "shield" : null;
                    }
                    armor = SelectArmorByProficiency(level, hasHeavyArmor, hasMediumArmor, hasLightArmor, preferHeavyAtHigherLevels: true);
                    break;
                case "barbarian":
                    mainHand = PickRandomWeapon(new[] { "greataxe", "greatsword", "maul", "halberd", "glaive" });
                    // No armor - use Unarmored Defence
                    break;
                case "paladin":
                    mainHand = hasMartialWeapons
                        ? PickRandomWeapon(new[] { "longsword", "battleaxe", "warhammer", "morningstar", "greatsword", "maul" })
                        : PickRandomWeapon(new[] { "mace", "quarterstaff" });
                    if (IsTwoHandedWeapon(mainHand))
                    {
                        shield = null;
                    }
                    else
                    {
                        shield = hasShieldProficiency ? "shield" : null;
                    }
                    armor = SelectArmorByProficiency(level, hasHeavyArmor, hasMediumArmor, hasLightArmor, preferHeavyAtHigherLevels: true);
                    break;
                case "ranger":
                    mainHand = hasMartialWeapons
                        ? PickRandomWeapon(new[] { "longbow", "shortbow", "shortsword" })
                        : "shortbow";
                    if (mainHand == "shortsword" && hasMartialWeapons)
                    {
                        offHand = "shortsword";
                    }
                    armor = hasMediumArmor ? "scale_mail" : (hasLightArmor ? "studded_leather" : null);
                    break;
                case "rogue":
                    mainHand = hasMartialWeapons ? PickRandomWeapon(new[] { "rapier", "shortsword" }) : "dagger";
                    if (mainHand == "shortsword")
                        offHand = "dagger";
                    armor = hasLightArmor ? "leather" : null;
                    break;
                case "monk":
                    mainHand = hasSimpleWeapons ? "quarterstaff" : "club";
                    // No armor - use Unarmored Defence
                    break;
                case "cleric":
                    // Keep clerics on proficiencies unless subclass features explicitly add more later.
                    mainHand = hasMartialWeapons
                        ? PickRandomWeapon(new[] { "mace", "morningstar", "flail", "warhammer" })
                        : PickRandomWeapon(new[] { "mace", "quarterstaff" });
                    shield = hasShieldProficiency ? "shield" : null;
                    armor = hasMediumArmor ? "scale_mail" : (hasLightArmor ? "leather" : (hasHeavyArmor ? "chain_mail" : null));
                    break;
                case "druid":
                    mainHand = hasMartialWeapons || HasSpecificWeaponProficiency(classDef, "Scimitar")
                        ? "scimitar"
                        : "quarterstaff";
                    shield = hasShieldProficiency ? "shield" : null;
                    armor = hasLightArmor ? "leather" : null;
                    break;
                case "wizard":
                    mainHand = hasSimpleWeapons ? "quarterstaff" : "club";
                    // No armor
                    break;
                case "sorcerer":
                    mainHand = "dagger";
                    // No armor (relies on Mage Armor or Draconic Resilience)
                    break;
                case "warlock":
                    mainHand = hasSimpleWeapons ? "light_crossbow" : "dagger";
                    armor = hasLightArmor ? "leather" : null;
                    break;
                case "bard":
                    mainHand = hasMartialWeapons || HasSpecificWeaponProficiency(classDef, "Rapier")
                        ? "rapier"
                        : "dagger";
                    armor = hasLightArmor ? "leather" : null;
                    break;
                default:
                    mainHand = hasSimpleWeapons ? "mace" : "club";
                    break;
            }

            return (mainHand, offHand, armor, shield);
        }

        private static bool HasWeaponCategory(ClassDefinition classDef, string category)
        {
            if (classDef?.StartingProficiencies?.WeaponCategories == null)
                return false;

            return classDef.StartingProficiencies.WeaponCategories.Any(value =>
                string.Equals(value, category, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasArmorCategory(ClassDefinition classDef, string category)
        {
            if (classDef?.StartingProficiencies?.ArmorCategories == null)
                return false;

            return classDef.StartingProficiencies.ArmorCategories.Any(value =>
                string.Equals(value, category, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasSpecificWeaponProficiency(ClassDefinition classDef, string weaponName)
        {
            if (classDef?.StartingProficiencies?.Weapons == null)
                return false;

            return classDef.StartingProficiencies.Weapons.Any(value =>
                string.Equals(value, weaponName, StringComparison.OrdinalIgnoreCase));
        }

        private static string SelectArmorByProficiency(int level, bool hasHeavyArmor, bool hasMediumArmor, bool hasLightArmor, bool preferHeavyAtHigherLevels)
        {
            if (hasHeavyArmor)
            {
                if (preferHeavyAtHigherLevels && level >= 4)
                    return "splint";
                return "chain_mail";
            }

            if (hasMediumArmor)
                return "scale_mail";

            if (hasLightArmor)
                return "leather";

            return null;
        }

        private string PickRandomWeapon(string[] options)
        {
            if (options == null || options.Length == 0)
                return "club"; // safe fallback
            return options[_random.Next(options.Length)];
        }

        private static bool IsTwoHandedWeapon(string weaponId)
        {
            return weaponId is "greatsword" or "greataxe" or "maul" or "halberd" or "glaive" or "pike";
        }

    }
}
