using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for AIDecisionPipeline.
    /// Note: Tests that require CombatContext (Godot Node) are skipped as they cannot run in xUnit.
    /// </summary>
    public class AIDecisionPipelineTests
    {
        /// <summary>
        /// Lightweight ICombatContext for unit tests — no Godot Node dependency.
        /// </summary>
        private class TestCombatContext : ICombatContext
        {
            private readonly Dictionary<string, Combatant> _combatants = new();
            private readonly Dictionary<Type, object> _services = new();
            private readonly List<string> _registeredServiceNames = new();

            public void RegisterCombatant(Combatant combatant) => _combatants[combatant.Id] = combatant;
            public void AddCombatant(Combatant combatant) => RegisterCombatant(combatant);
            public Combatant GetCombatant(string id) => _combatants.TryGetValue(id, out var c) ? c : null;
            public IEnumerable<Combatant> GetAllCombatants() => _combatants.Values;
            public void ClearCombatants() => _combatants.Clear();

            public void RegisterService<T>(T service) where T : class
            {
                _services[typeof(T)] = service;
                var name = typeof(T).Name;
                if (!_registeredServiceNames.Contains(name))
                    _registeredServiceNames.Add(name);
            }
            public T GetService<T>() where T : class =>
                _services.TryGetValue(typeof(T), out var s) ? s as T : null;
            public bool TryGetService<T>(out T service) where T : class
            {
                var ok = _services.TryGetValue(typeof(T), out var obj);
                service = ok ? obj as T : null;
                return ok && service != null;
            }
            public bool HasService<T>() where T : class => _services.ContainsKey(typeof(T));
            public List<string> GetRegisteredServices() => new(_registeredServiceNames);
            public void ClearServices() { _services.Clear(); _registeredServiceNames.Clear(); }
        }

        [Fact(Skip = "Requires CombatContext which is a Godot Node - cannot instantiate in xUnit")]
        public void GenerateCandidates_ReturnsValidActions()
        {
            // This test requires CombatContext instantiation
        }

        [Fact(Skip = "Requires CombatContext which is a Godot Node - cannot instantiate in xUnit")]
        public void GenerateCandidates_IncludesEndTurn()
        {
            // This test requires CombatContext instantiation
        }

        [Fact(Skip = "Requires CombatContext which is a Godot Node - cannot instantiate in xUnit")]
        public void ScoreCandidates_AssignsFiniteScores()
        {
            // This test requires CombatContext instantiation
        }

        [Fact]
        public void SelectBest_ChoosesHighestScore()
        {
            // Arrange - This test doesn't need CombatContext
            var candidates = new[]
            {
                new AIAction { ActionType = AIActionType.Move, Score = 2.0f },
                new AIAction { ActionType = AIActionType.Attack, Score = 5.0f },
                new AIAction { ActionType = AIActionType.EndTurn, Score = 0.1f }
            }.ToList();

            var profile = new AIProfile { Difficulty = AIDifficulty.Nightmare };

            // Create pipeline with null context - SelectBest doesn't use context
            // Use Nightmare difficulty for deterministic selection (always picks highest score)
            var pipeline = new AIDecisionPipeline(null, seed: 42);

            // Act
            var best = pipeline.SelectBest(candidates, profile);

            // Assert
            Assert.Equal(AIActionType.Attack, best.ActionType);
            Assert.Equal(5.0f, best.Score);
        }

        [Fact(Skip = "Requires CombatContext which is a Godot Node - cannot instantiate in xUnit")]
        public void Profile_AffectsScoring()
        {
            // This test requires CombatContext instantiation
        }

        [Fact]
        public void Difficulty_Easy_SometimesSuboptimal()
        {
            // Arrange - This test doesn't need CombatContext
            var candidates = new[]
            {
                new AIAction { ActionType = AIActionType.Attack, Score = 10.0f },
                new AIAction { ActionType = AIActionType.Move, Score = 5.0f },
                new AIAction { ActionType = AIActionType.EndTurn, Score = 1.0f }
            }.ToList();

            var easyProfile = new AIProfile { Difficulty = AIDifficulty.Easy };
            var pipeline = new AIDecisionPipeline(null, seed: 123);

            // Act - run multiple times to check for variety
            var selections = Enumerable.Range(0, 20)
                .Select(_ => pipeline.SelectBest(candidates.ToList(), easyProfile))
                .ToList();

            var uniqueChoices = selections.Select(s => s.ActionType).Distinct().Count();

            // Assert
            Assert.True(uniqueChoices > 1,
                "Easy difficulty should sometimes choose suboptimal actions");
        }

        [Fact(Skip = "Requires CombatContext which is a Godot Node - cannot instantiate in xUnit")]
        public void TimedOut_FallsBackToFirstAction()
        {
            // This test requires CombatContext instantiation
        }

        [Fact(Skip = "Requires CombatContext which is a Godot Node - cannot instantiate in xUnit")]
        public void DebugLogging_CapturesScoreBreakdown()
        {
            // This test requires CombatContext instantiation
        }

        [Fact]
        public void AIAction_ToString_FormatsCorrectly()
        {
            // Arrange - This test doesn't need CombatContext
            var action = new AIAction
            {
                ActionType = AIActionType.Attack,
                ActionId = "Projectile_Fireball",
                TargetId = "enemy1",
                Score = 7.5f
            };

            // Act
            var str = action.ToString();

            // Assert
            Assert.Contains("Attack", str);
            Assert.Contains("Projectile_Fireball", str);
            Assert.Contains("enemy1", str);
        }

        [Fact]
        public void AIProfile_CreateForArchetype_SetsWeights()
        {
            // Arrange & Act - This test doesn't need CombatContext
            var aggressive = AIProfile.CreateForArchetype(AIArchetype.Aggressive);
            var support = AIProfile.CreateForArchetype(AIArchetype.Support);

            // Assert
            Assert.True(aggressive.GetWeight("damage") > support.GetWeight("damage"));
            Assert.True(support.GetWeight("healing") > aggressive.GetWeight("healing"));
        }

        [Fact]
        public void AIProfile_Difficulty_AffectsRandomFactor()
        {
            // Arrange & Act - This test doesn't need CombatContext
            var easy = AIProfile.CreateForArchetype(AIArchetype.Tactical, AIDifficulty.Easy);
            var nightmare = AIProfile.CreateForArchetype(AIArchetype.Tactical, AIDifficulty.Nightmare);

            // Assert
            Assert.True(easy.RandomFactor > nightmare.RandomFactor);
            Assert.Equal(0f, nightmare.RandomFactor);
        }

        private Combatant CreateTestCombatant(string id, Faction faction, int hp = 50)
        {
            var combatant = new Combatant(id, id, faction, hp, initiative: 10);
            combatant.ActionBudget.ResetFull();
            return combatant;
        }

        // ─────────────────────────────────────────────────────
        //  Helpers for BG3 item-usage parameter tests
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Build a minimal pipeline with InventoryService + EffectPipeline wired up,
        /// plus one combatant with a healing potion in their bag.
        /// </summary>
        private (AIDecisionPipeline pipeline, TestCombatContext ctx, Combatant actor) BuildItemTestPipeline()
        {
            var ctx = new TestCombatContext();

            // Combatant at low HP so the healing potion is considered
            var actor = CreateTestCombatant("hero", Faction.Player, hp: 50);
            actor.Resources.TakeDamage(30); // 20/50 = 40 % HP
            ctx.RegisterCombatant(actor);

            // Enemy so pipeline doesn't short-circuit some methods
            var enemy = CreateTestCombatant("enemy1", Faction.Hostile, hp: 40);
            enemy.Position = new Vector3(10, 0, 0);
            ctx.RegisterCombatant(enemy);

            // InventoryService with a healing potion
            var invService = new InventoryService(new CharacterDataRegistry());
            var inv = invService.GetInventory(actor.Id);
            inv.AddItem(new InventoryItem
            {
                DefinitionId = "potion_healing",
                Name = "Potion of Healing",
                Category = ItemCategory.Potion,
                Quantity = 2,
                UseActionId = "use_potion_healing"
            });
            ctx.RegisterService(invService);

            // EffectPipeline with matching action definition
            var effectPipeline = new EffectPipeline();
            effectPipeline.RegisterAction(new ActionDefinition
            {
                Id = "use_potion_healing",
                Name = "Use Potion of Healing",
                TargetType = TargetType.Self,
                Range = 5f,
                Cost = new ActionCost { UsesBonusAction = true },
                AIBaseDesirability = 1.0f
            });
            ctx.RegisterService(effectPipeline);

            var pipeline = new AIDecisionPipeline(ctx, seed: 42);
            pipeline.LateInitialize();

            return (pipeline, ctx, actor);
        }

        [Fact]
        public void UseInventoryItemsEnabled_Zero_ReturnsEmptyList()
        {
            // Arrange
            var (pipeline, _, actor) = BuildItemTestPipeline();

            var bg3 = new BG3ArchetypeProfile();
            bg3.LoadFromSettings(new Dictionary<string, float>
            {
                ["USE_INVENTORY_ITEMS_ENABLED"] = 0f
            });

            var profile = new AIProfile
            {
                Id = "test_gate",
                Difficulty = AIDifficulty.Nightmare,
                BG3Profile = bg3
            };

            // Act
            var result = pipeline.MakeDecision(actor, profile);
            var itemCandidates = result.AllCandidates
                .Where(c => c.ActionType == AIActionType.UseItem)
                .ToList();

            // Assert — gate should block all item candidates
            Assert.Empty(itemCandidates);
        }

        [Fact]
        public void UseItemModifier_ScalesItemScores()
        {
            // Arrange — run once with default BG3 (UseItemModifier=0.3) and once without BG3
            var (pipelineWithBG3, _, actorBG3) = BuildItemTestPipeline();
            var (pipelineNoBG3, _, actorNoBG3) = BuildItemTestPipeline();

            var bg3 = new BG3ArchetypeProfile();
            // UseItemModifier defaults to 0.3

            var profileWithBG3 = new AIProfile
            {
                Id = "test_modifier",
                Difficulty = AIDifficulty.Nightmare,
                BG3Profile = bg3
            };
            var profileNoBG3 = new AIProfile
            {
                Id = "test_no_bg3",
                Difficulty = AIDifficulty.Nightmare
            };

            // Act
            var resultBG3 = pipelineWithBG3.MakeDecision(actorBG3, profileWithBG3);
            var resultNoBG3 = pipelineNoBG3.MakeDecision(actorNoBG3, profileNoBG3);

            var itemsBG3 = resultBG3.AllCandidates
                .Where(c => c.ActionType == AIActionType.UseItem)
                .ToList();
            var itemsNoBG3 = resultNoBG3.AllCandidates
                .Where(c => c.ActionType == AIActionType.UseItem)
                .ToList();

            // Assert — both pipelines should produce item candidates
            Assert.NotEmpty(itemsNoBG3);
            Assert.NotEmpty(itemsBG3);

            // BG3 scores should be scaled by UseItemModifier (0.3) relative to legacy scores
            float bg3Score = itemsBG3.First().Score;
            float legacyScore = itemsNoBG3.First().Score;

            Assert.True(bg3Score < legacyScore,
                $"BG3 item score ({bg3Score:F3}) should be less than legacy score ({legacyScore:F3}) due to UseItemModifier=0.3");
            // Check the ratio is approximately 0.3
            float ratio = bg3Score / legacyScore;
            Assert.InRange(ratio, 0.25f, 0.35f);
        }

        // ─────────────────────────────────────────────────────
        //  Phase 1 review – missing coverage tests
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Build a pipeline with throwable items and a SingleUnit-targeted action.
        /// Enemy is placed at <paramref name="enemyDistance"/> from the actor.
        /// </summary>
        private (AIDecisionPipeline pipeline, TestCombatContext ctx, Combatant actor) BuildThrowableTestPipeline(
            int throwableCount = 3, float enemyDistance = 10f)
        {
            var ctx = new TestCombatContext();

            var actor = CreateTestCombatant("hero", Faction.Player, hp: 50);
            actor.Resources.TakeDamage(30); // 20/50 = 40 % HP
            ctx.RegisterCombatant(actor);

            var enemy = CreateTestCombatant("enemy1", Faction.Hostile, hp: 40);
            enemy.Position = new Vector3(enemyDistance, 0, 0);
            ctx.RegisterCombatant(enemy);

            var invService = new InventoryService(new CharacterDataRegistry());
            var inv = invService.GetInventory(actor.Id);
            for (int i = 0; i < throwableCount; i++)
            {
                inv.AddItem(new InventoryItem
                {
                    DefinitionId = $"bomb_{i}",
                    Name = $"Bomb {i}",
                    Category = ItemCategory.Throwable,
                    Quantity = 1,
                    UseActionId = "throw_bomb"
                });
            }
            ctx.RegisterService(invService);

            var effectPipeline = new EffectPipeline();
            effectPipeline.RegisterAction(new ActionDefinition
            {
                Id = "throw_bomb",
                Name = "Throw Bomb",
                TargetType = TargetType.SingleUnit,
                TargetFilter = TargetFilter.Enemies,
                Range = 15f,
                Cost = new ActionCost { UsesAction = true },
                AIBaseDesirability = 2.0f
            });
            ctx.RegisterService(effectPipeline);

            var pipeline = new AIDecisionPipeline(ctx, seed: 42);
            pipeline.LateInitialize();

            return (pipeline, ctx, actor);
        }

        [Fact]
        public void UseItemRadius_ExcludesTargetsBeyondRadius()
        {
            // Arrange — enemy at 25 units; UseItemRadius = 12 but action range = 15.
            // Clamped effectiveRange = max(12, 15) = 15 < 25, so enemy is excluded.
            var (pipeline, _, actor) = BuildThrowableTestPipeline(throwableCount: 1, enemyDistance: 25f);

            var bg3 = new BG3ArchetypeProfile();
            bg3.LoadFromSettings(new Dictionary<string, float>
            {
                ["USE_ITEM_RADIUS"] = 12f // less than action Range (15), clamped to 15
            });

            var profile = new AIProfile
            {
                Id = "test_radius",
                Difficulty = AIDifficulty.Nightmare,
                BG3Profile = bg3
            };

            // Act
            var result = pipeline.MakeDecision(actor, profile);
            var itemCandidates = result.AllCandidates
                .Where(c => c.ActionType == AIActionType.UseItem)
                .ToList();

            // Assert — enemy at 25 is beyond effectiveRange 15, so no item candidates
            Assert.Empty(itemCandidates);
        }

        [Fact]
        public void ThrowInventoryItemLimit_CapsThrowableCandidates()
        {
            // Arrange — 5 throwables in bag, limit set to 2
            var (pipeline, _, actor) = BuildThrowableTestPipeline(throwableCount: 5, enemyDistance: 10f);

            var bg3 = new BG3ArchetypeProfile();
            bg3.LoadFromSettings(new Dictionary<string, float>
            {
                ["THROW_INVENTORY_ITEM_LIMIT"] = 2f
            });

            var profile = new AIProfile
            {
                Id = "test_throw_limit",
                Difficulty = AIDifficulty.Nightmare,
                BG3Profile = bg3
            };

            // Act
            var result = pipeline.MakeDecision(actor, profile);
            var itemCandidates = result.AllCandidates
                .Where(c => c.ActionType == AIActionType.UseItem)
                .ToList();

            // Assert — only 2 throwable candidates despite 5 in inventory
            Assert.Equal(2, itemCandidates.Count);
        }

        [Fact]
        public void NullBG3Profile_LegacyBehaviorPreserved()
        {
            // Arrange — use base helper which adds a healing potion; no BG3 profile
            var (pipeline, _, actor) = BuildItemTestPipeline();

            var profile = new AIProfile
            {
                Id = "test_legacy",
                Difficulty = AIDifficulty.Nightmare
                // BG3Profile is null (legacy path)
            };

            // Act
            var result = pipeline.MakeDecision(actor, profile);
            var itemCandidates = result.AllCandidates
                .Where(c => c.ActionType == AIActionType.UseItem)
                .ToList();

            // Assert — legacy path should still generate and score item candidates
            Assert.NotEmpty(itemCandidates);
            var healingCandidate = itemCandidates.First();
            Assert.Equal("potion_healing", healingCandidate.ActionId);
            Assert.True(healingCandidate.Score > 0f, "Legacy item score should be positive");
        }

        [Fact]
        public void MultiplierSelfOnlyThrow_AppliedToSelfTargetedThrowable()
        {
            // Arrange — a throwable targeted at self (e.g., splash self-buff).
            var ctx = new TestCombatContext();

            var actor = CreateTestCombatant("hero", Faction.Player, hp: 50);
            ctx.RegisterCombatant(actor);

            var enemy = CreateTestCombatant("enemy1", Faction.Hostile, hp: 40);
            enemy.Position = new Vector3(10, 0, 0);
            ctx.RegisterCombatant(enemy);

            var invService = new InventoryService(new CharacterDataRegistry());
            var inv = invService.GetInventory(actor.Id);
            inv.AddItem(new InventoryItem
            {
                DefinitionId = "self_bomb",
                Name = "Self Bomb",
                Category = ItemCategory.Throwable,
                Quantity = 1,
                UseActionId = "throw_self_bomb"
            });
            ctx.RegisterService(invService);

            var effectPipeline = new EffectPipeline();
            effectPipeline.RegisterAction(new ActionDefinition
            {
                Id = "throw_self_bomb",
                Name = "Throw Self Bomb",
                TargetType = TargetType.Self,
                Range = 0f,
                Cost = new ActionCost { UsesAction = true },
                AIBaseDesirability = 2.0f
            });
            ctx.RegisterService(effectPipeline);

            var pipeline = new AIDecisionPipeline(ctx, seed: 42);
            pipeline.LateInitialize();

            // BG3 profile with MultiplierSelfOnlyThrow = 0.5
            var bg3 = new BG3ArchetypeProfile();
            bg3.LoadFromSettings(new Dictionary<string, float>
            {
                ["MULTIPLIER_SELF_ONLY_THROW"] = 0.5f
            });

            var profile = new AIProfile
            {
                Id = "test_self_throw",
                Difficulty = AIDifficulty.Nightmare,
                BG3Profile = bg3
            };

            // Act
            var result = pipeline.MakeDecision(actor, profile);
            var itemCandidate = result.AllCandidates
                .Where(c => c.ActionType == AIActionType.UseItem && c.ActionId == "self_bomb")
                .FirstOrDefault();

            // Assert — candidate should exist and its final score should reflect
            // the MultiplierSelfOnlyThrow (0.5) and UseItemModifier (default 0.3)
            Assert.NotNull(itemCandidate);
            // Base score = AIBaseDesirability(2.0) * 3.0 = 6.0
            // Scored: 6.0 * UseItemModifier(0.3) * MultiplierSelfOnlyThrow(0.5) = 0.9
            float expectedScore = 2.0f * 3.0f * 0.3f * 0.5f;
            Assert.InRange(itemCandidate.Score, expectedScore - 0.1f, expectedScore + 0.1f);
        }

        // ─────────────────────────────────────────────────────
        //  Phase 7 – Weapon Pickup AI parameter tests
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Build a minimal pipeline with GroundItemService registered and one weapon on the ground.
        /// Actor is at origin; enemy present to prevent short-circuits.
        /// </summary>
        private (AIDecisionPipeline pipeline, TestCombatContext ctx, Combatant actor) BuildWeaponPickupTestPipeline(
            List<GroundItemService.GroundWeapon> groundWeapons = null)
        {
            var ctx = new TestCombatContext();

            var actor = CreateTestCombatant("fighter", Faction.Hostile, hp: 50);
            actor.Position = Vector3.Zero;
            ctx.RegisterCombatant(actor);

            var enemy = CreateTestCombatant("player1", Faction.Player, hp: 40);
            enemy.Position = new Vector3(10, 0, 0);
            ctx.RegisterCombatant(enemy);

            // Register GroundItemService with weapons
            var groundItemService = new GroundItemService();
            if (groundWeapons != null)
            {
                foreach (var w in groundWeapons)
                    groundItemService.DropWeapon(w);
            }
            ctx.RegisterService(groundItemService);

            var pipeline = new AIDecisionPipeline(ctx, seed: 42);
            pipeline.LateInitialize();

            return (pipeline, ctx, actor);
        }

        private static GroundItemService.GroundWeapon MakeGroundWeapon(
            string id, Vector3? position = null, float avgDmg = 5f,
            bool isRanged = false, string prevOwner = null, string prevFaction = null,
            bool requiresProf = false, string weaponType = "Longsword", string weaponCategory = "Martial")
        {
            return new GroundItemService.GroundWeapon
            {
                Id = id,
                Name = id,
                Position = position ?? new Vector3(3, 0, 0),
                AverageDamage = avgDmg,
                IsRanged = isRanged,
                PreviousOwnerId = prevOwner,
                PreviousOwnerFaction = prevFaction,
                RequiresProficiency = requiresProf,
                WeaponType = weaponType,
                WeaponCategory = weaponCategory
            };
        }

        [Fact]
        public void WeaponPickup_DisabledModifier_NoCandidate()
        {
            var weapons = new List<GroundItemService.GroundWeapon>
            {
                MakeGroundWeapon("sword1")
            };
            var (pipeline, _, actor) = BuildWeaponPickupTestPipeline(weapons);

            var bg3 = new BG3ArchetypeProfile();
            bg3.LoadFromSettings(new Dictionary<string, float>
            {
                ["WEAPON_PICKUP_MODIFIER"] = 0f // disabled
            });

            var profile = new AIProfile
            {
                Id = "test_disabled",
                Difficulty = AIDifficulty.Nightmare,
                BG3Profile = bg3
            };

            var result = pipeline.MakeDecision(actor, profile);
            var pickupCandidates = result.AllCandidates
                .Where(c => c.ActionType == AIActionType.PickupWeapon)
                .ToList();

            Assert.Empty(pickupCandidates);
        }

        [Fact]
        public void WeaponPickup_HighDamageWeapon_ScoredHigher()
        {
            var weapons = new List<GroundItemService.GroundWeapon>
            {
                MakeGroundWeapon("weak", avgDmg: 5f, position: new Vector3(2, 0, 0)),
                MakeGroundWeapon("strong", avgDmg: 50f, position: new Vector3(3, 0, 0))
            };
            var (pipeline, _, actor) = BuildWeaponPickupTestPipeline(weapons);

            var bg3 = new BG3ArchetypeProfile(); // defaults: modifier=0.3, damage=0.005
            var profile = new AIProfile
            {
                Id = "test_damage",
                Difficulty = AIDifficulty.Nightmare,
                BG3Profile = bg3
            };

            var result = pipeline.MakeDecision(actor, profile);
            var pickups = result.AllCandidates
                .Where(c => c.ActionType == AIActionType.PickupWeapon)
                .ToList();

            Assert.Equal(2, pickups.Count);
            var weakScore = pickups.First(c => c.ActionId == "weak").Score;
            var strongScore = pickups.First(c => c.ActionId == "strong").Score;
            Assert.True(strongScore > weakScore,
                $"High-damage weapon ({strongScore:F3}) should score higher than low-damage ({weakScore:F3})");
        }

        [Fact]
        public void WeaponPickup_NoProficiency_PenaltyApplied()
        {
            // Actor without ResolvedCharacter.Proficiencies = assumed proficient (no penalty).
            // Actor WITH ResolvedCharacter but empty proficiencies = not proficient.
            var weapons = new List<GroundItemService.GroundWeapon>
            {
                MakeGroundWeapon("sword1", requiresProf: true, weaponType: "Longsword", weaponCategory: "Martial")
            };
            var (pipeline, _, actor) = BuildWeaponPickupTestPipeline(weapons);

            // Give actor an empty proficiency set (not proficient with anything)
            actor.ResolvedCharacter = new QDND.Data.CharacterModel.ResolvedCharacter
            {
                Proficiencies = new QDND.Data.CharacterModel.ProficiencySet()
            };

            var bg3 = new BG3ArchetypeProfile(); // defaults: NoProficiency=0.5
            var profile = new AIProfile
            {
                Id = "test_noprof",
                Difficulty = AIDifficulty.Nightmare,
                BG3Profile = bg3
            };

            var result = pipeline.MakeDecision(actor, profile);
            var pickups = result.AllCandidates
                .Where(c => c.ActionType == AIActionType.PickupWeapon)
                .ToList();

            Assert.Single(pickups);
            // Expected: base(0.3) + damage(0.005*5=0.025) = 0.325, then * NoProficiency(0.5) = 0.1625
            float expectedBase = 0.3f + 0.005f * 5f;
            float expected = expectedBase * 0.5f;
            Assert.InRange(pickups[0].Score, expected - 0.01f, expected + 0.01f);
        }

        [Fact]
        public void WeaponPickup_PartyAllyWeapon_ZeroScore()
        {
            // Weapon dropped by same-faction ally (not self) with PartyAlly=0.0 → score = 0, not generated
            var weapons = new List<GroundItemService.GroundWeapon>
            {
                MakeGroundWeapon("ally_sword", prevOwner: "ally1", prevFaction: "Hostile")
            };
            var (pipeline, _, actor) = BuildWeaponPickupTestPipeline(weapons);

            var bg3 = new BG3ArchetypeProfile(); // defaults: PartyAlly=0.0
            var profile = new AIProfile
            {
                Id = "test_partyally",
                Difficulty = AIDifficulty.Nightmare,
                BG3Profile = bg3
            };

            var result = pipeline.MakeDecision(actor, profile);
            var pickups = result.AllCandidates
                .Where(c => c.ActionType == AIActionType.PickupWeapon)
                .ToList();

            // PartyAlly = 0.0 multiplier → score becomes 0 → candidate filtered out
            Assert.Empty(pickups);
        }

        [Fact]
        public void WeaponPickup_OutsideRadius_NotIncluded()
        {
            var weapons = new List<GroundItemService.GroundWeapon>
            {
                MakeGroundWeapon("far_sword", position: new Vector3(50, 0, 0))
            };
            var (pipeline, _, actor) = BuildWeaponPickupTestPipeline(weapons);

            var bg3 = new BG3ArchetypeProfile(); // defaults: radius=12
            var profile = new AIProfile
            {
                Id = "test_radius",
                Difficulty = AIDifficulty.Nightmare,
                BG3Profile = bg3
            };

            var result = pipeline.MakeDecision(actor, profile);
            var pickups = result.AllCandidates
                .Where(c => c.ActionType == AIActionType.PickupWeapon)
                .ToList();

            Assert.Empty(pickups);
        }
    }
}
