using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using QDND.Combat.Rules;
using QDND.Combat.Entities;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Statuses;
using QDND.Combat.Reactions;

#nullable enable

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for reaction triggers in EffectPipeline.
    /// Tests that damage and ability cast events correctly trigger reaction checks.
    /// </summary>
    public class EffectPipelineReactionTests
    {
        private (EffectPipeline Pipeline, RulesEngine Rules, StatusManager Statuses, ReactionSystem Reactions) CreatePipelineWithReactions(int seed = 42)
        {
            var rules = new RulesEngine(seed);
            var statuses = new StatusManager(rules);
            var reactions = new ReactionSystem(rules.Events);
            var pipeline = new EffectPipeline
            {
                Rules = rules,
                Statuses = statuses,
                Rng = new Random(seed),
                Reactions = reactions
            };
            return (pipeline, rules, statuses, reactions);
        }

        private Combatant CreateCombatant(string id, Faction faction = Faction.Player, int hp = 100, int initiative = 10)
        {
            return new Combatant(id, $"Test_{id}", faction, hp, initiative);
        }

        private ActionDefinition CreateSpellAbility(int damage = 10, string damageType = "fire")
        {
            return new ActionDefinition
            {
                Id = "test_spell",
                Name = "Test Spell",
                TargetType = TargetType.SingleUnit,
                Tags = new HashSet<string> { "spell", "magic" },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = damage, DamageType = damageType }
                }
            };
        }

        private ActionDefinition CreateMeleeAbility(int damage = 10)
        {
            return new ActionDefinition
            {
                Id = "test_melee",
                Name = "Test Melee",
                TargetType = TargetType.SingleUnit,
                Tags = new HashSet<string> { "weapon", "melee" },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = damage, DamageType = "slashing" }
                }
            };
        }

        private ReactionDefinition CreateCounterspellReaction()
        {
            return new ReactionDefinition
            {
                Id = "counterspell",
                Name = "Counterspell",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.SpellCastNearby },
                Range = 60f,
                CanCancel = true,
                Priority = 10
            };
        }

        private ReactionDefinition CreateShieldReaction()
        {
            return new ReactionDefinition
            {
                Id = "shield",
                Name = "Shield",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.YouTakeDamage },
                Range = 0f, // Self only
                CanModify = true,
                Priority = 20
            };
        }

        private ReactionDefinition CreateProtectAllyReaction()
        {
            return new ReactionDefinition
            {
                Id = "protect_ally",
                Name = "Protect Ally",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.AllyTakesDamage },
                Range = 10f,
                CanModify = true,
                Priority = 30
            };
        }

        #region SpellCastNearby Trigger Tests

        [Fact]
        public void CastingSpell_TriggersSpellCastNearby_WhenEnemyHasCounterspell()
        {
            // Arrange
            var (pipeline, rules, statuses, reactions) = CreatePipelineWithReactions();
            var caster = CreateCombatant("caster", Faction.Player, 100);
            var enemy = CreateCombatant("enemy", Faction.Hostile, 100);
            enemy.Position = new Godot.Vector3(10, 0, 0); // Within 60 range, at position relative to caster at origin
            var target = CreateCombatant("target", Faction.Hostile, 100);

            var spell = CreateSpellAbility(20, "fire");
            pipeline.RegisterAction(spell);

            var counterspell = CreateCounterspellReaction();
            reactions.RegisterReaction(counterspell);
            reactions.GrantReaction(enemy.Id, "counterspell");

            var allCombatants = new List<Combatant> { caster, enemy, target };
            pipeline.GetCombatants = () => allCombatants;

            ReactionTriggerEventArgs? receivedArgs = null;
            pipeline.OnAbilityCastTrigger += (sender, args) => receivedArgs = args;

            // Act
            pipeline.ExecuteAction("test_spell", caster, new List<Combatant> { target });

            // Assert
            Assert.NotNull(receivedArgs);
            Assert.Equal(ReactionTriggerType.SpellCastNearby, receivedArgs.Context.TriggerType);
            Assert.Equal(caster.Id, receivedArgs.Context.TriggerSourceId);
            Assert.Equal("test_spell", receivedArgs.Context.ActionId);
            Assert.Single(receivedArgs.EligibleReactors);
            Assert.Equal(enemy.Id, receivedArgs.EligibleReactors[0].CombatantId);
        }

        [Fact]
        public void CastingSpell_DoesNotTrigger_WhenEnemyOutOfRange()
        {
            // Arrange
            var (pipeline, rules, statuses, reactions) = CreatePipelineWithReactions();
            var caster = CreateCombatant("caster", Faction.Player, 100);
            var enemy = CreateCombatant("enemy", Faction.Hostile, 100);
            enemy.Position = new Godot.Vector3(100, 0, 0); // Beyond 60 range
            var target = CreateCombatant("target", Faction.Hostile, 100);

            var spell = CreateSpellAbility(20, "fire");
            pipeline.RegisterAction(spell);

            var counterspell = CreateCounterspellReaction();
            reactions.RegisterReaction(counterspell);
            reactions.GrantReaction(enemy.Id, "counterspell");

            var allCombatants = new List<Combatant> { caster, enemy, target };
            pipeline.GetCombatants = () => allCombatants;

            ReactionTriggerEventArgs? receivedArgs = null;
            pipeline.OnAbilityCastTrigger += (sender, args) => receivedArgs = args;

            // Act
            pipeline.ExecuteAction("test_spell", caster, new List<Combatant> { target });

            // Assert - Event still fires but no eligible reactors
            Assert.Null(receivedArgs); // Event not fired because no eligible reactors
        }

        [Fact]
        public void CastingMeleeAbility_DoesNotTriggerSpellCastNearby()
        {
            // Arrange
            var (pipeline, rules, statuses, reactions) = CreatePipelineWithReactions();
            var attacker = CreateCombatant("attacker", Faction.Player, 100);
            var enemy = CreateCombatant("enemy", Faction.Hostile, 100);
            enemy.Position = new Godot.Vector3(5, 0, 0);
            var target = CreateCombatant("target", Faction.Hostile, 100);

            var melee = CreateMeleeAbility(15);
            pipeline.RegisterAction(melee);

            var counterspell = CreateCounterspellReaction();
            reactions.RegisterReaction(counterspell);
            reactions.GrantReaction(enemy.Id, "counterspell");

            var allCombatants = new List<Combatant> { attacker, enemy, target };
            pipeline.GetCombatants = () => allCombatants;

            ReactionTriggerEventArgs? receivedArgs = null;
            pipeline.OnAbilityCastTrigger += (sender, args) => receivedArgs = args;

            // Act
            pipeline.ExecuteAction("test_melee", attacker, new List<Combatant> { target });

            // Assert - No trigger for non-spell ability
            Assert.Null(receivedArgs);
        }

        [Fact]
        public void CastingSpell_CanBeCancelled_WhenReactionSetsCancelFlag()
        {
            // Arrange
            var (pipeline, rules, statuses, reactions) = CreatePipelineWithReactions();
            var caster = CreateCombatant("caster", Faction.Player, 100);
            var enemy = CreateCombatant("enemy", Faction.Hostile, 100);
            enemy.Position = new Godot.Vector3(10, 0, 0);
            var target = CreateCombatant("target", Faction.Hostile, 100);

            var spell = CreateSpellAbility(50, "fire");
            pipeline.RegisterAction(spell);

            var counterspell = CreateCounterspellReaction();
            reactions.RegisterReaction(counterspell);
            reactions.GrantReaction(enemy.Id, "counterspell");

            var allCombatants = new List<Combatant> { caster, enemy, target };
            pipeline.GetCombatants = () => allCombatants;

            // Set up handler that cancels the ability
            pipeline.OnAbilityCastTrigger += (sender, args) =>
            {
                args.Cancel = true; // Counterspell!
            };

            int targetHPBefore = target.Resources.CurrentHP;

            // Act
            var result = pipeline.ExecuteAction("test_spell", caster, new List<Combatant> { target });

            // Assert
            Assert.False(result.Success);
            Assert.Contains("countered", result.ErrorMessage!.ToLower());
            Assert.Equal(targetHPBefore, target.Resources.CurrentHP); // No damage dealt
        }

        #endregion

        #region DamageTaken Trigger Tests

        [Fact]
        public void DealingDamage_TriggersDamageTaken_WhenTargetHasShield()
        {
            // Arrange
            var (pipeline, rules, statuses, reactions) = CreatePipelineWithReactions();
            var attacker = CreateCombatant("attacker", Faction.Player, 100);
            var target = CreateCombatant("target", Faction.Hostile, 100);

            var melee = CreateMeleeAbility(20);
            pipeline.RegisterAction(melee);

            var shield = CreateShieldReaction();
            reactions.RegisterReaction(shield);
            reactions.GrantReaction(target.Id, "shield");

            var allCombatants = new List<Combatant> { attacker, target };
            pipeline.GetCombatants = () => allCombatants;

            ReactionTriggerEventArgs? receivedArgs = null;
            pipeline.OnDamageTrigger += (sender, args) => receivedArgs = args;

            // Act
            pipeline.ExecuteAction("test_melee", attacker, new List<Combatant> { target });

            // Assert
            Assert.NotNull(receivedArgs);
            Assert.Equal(ReactionTriggerType.YouTakeDamage, receivedArgs.Context.TriggerType);
            Assert.Equal(attacker.Id, receivedArgs.Context.TriggerSourceId);
            Assert.Equal(target.Id, receivedArgs.Context.AffectedId);
            Assert.Single(receivedArgs.EligibleReactors);
            Assert.Equal(target.Id, receivedArgs.EligibleReactors[0].CombatantId);
        }

        [Fact]
        public void DealingDamage_TriggersAllyTakesDamage_WhenAllyHasProtectReaction()
        {
            // Arrange
            var (pipeline, rules, statuses, reactions) = CreatePipelineWithReactions();
            var attacker = CreateCombatant("attacker", Faction.Player, 100);
            var target = CreateCombatant("target", Faction.Hostile, 100);
            var ally = CreateCombatant("ally", Faction.Hostile, 100);
            ally.Position = new Godot.Vector3(5, 0, 0); // Within 10 range of target

            var melee = CreateMeleeAbility(20);
            pipeline.RegisterAction(melee);

            var protect = CreateProtectAllyReaction();
            reactions.RegisterReaction(protect);
            reactions.GrantReaction(ally.Id, "protect_ally");

            var allCombatants = new List<Combatant> { attacker, target, ally };
            pipeline.GetCombatants = () => allCombatants;

            ReactionTriggerEventArgs? receivedArgs = null;
            pipeline.OnDamageTrigger += (sender, args) => receivedArgs = args;

            // Act
            pipeline.ExecuteAction("test_melee", attacker, new List<Combatant> { target });

            // Assert - Should have found the ally's protect reaction
            Assert.NotNull(receivedArgs);
            Assert.Contains(receivedArgs.EligibleReactors, r => r.CombatantId == ally.Id);
        }

        [Fact]
        public void DamageModifier_CanReduceDamage()
        {
            // Arrange
            var (pipeline, rules, statuses, reactions) = CreatePipelineWithReactions();
            var attacker = CreateCombatant("attacker", Faction.Player, 100);
            var target = CreateCombatant("target", Faction.Hostile, 100);

            var melee = CreateMeleeAbility(40);
            pipeline.RegisterAction(melee);

            var shield = CreateShieldReaction();
            reactions.RegisterReaction(shield);
            reactions.GrantReaction(target.Id, "shield");

            var allCombatants = new List<Combatant> { attacker, target };
            pipeline.GetCombatants = () => allCombatants;

            // Set up handler that reduces damage by half (shield effect)
            pipeline.OnDamageTrigger += (sender, args) =>
            {
                args.DamageModifier = 0.5f; // Half damage
            };

            // Act
            var result = pipeline.ExecuteAction("test_melee", attacker, new List<Combatant> { target });

            // Assert - Damage should be halved (40 * 0.5 = 20)
            Assert.True(result.Success);
            Assert.Equal(80, target.Resources.CurrentHP); // 100 - 20 = 80
        }

        [Fact]
        public void DamageModifier_CanBlockAllDamage()
        {
            // Arrange
            var (pipeline, rules, statuses, reactions) = CreatePipelineWithReactions();
            var attacker = CreateCombatant("attacker", Faction.Player, 100);
            var target = CreateCombatant("target", Faction.Hostile, 100);

            var melee = CreateMeleeAbility(50);
            pipeline.RegisterAction(melee);

            var shield = CreateShieldReaction();
            reactions.RegisterReaction(shield);
            reactions.GrantReaction(target.Id, "shield");

            var allCombatants = new List<Combatant> { attacker, target };
            pipeline.GetCombatants = () => allCombatants;

            // Set up handler that blocks all damage
            pipeline.OnDamageTrigger += (sender, args) =>
            {
                args.DamageModifier = 0f; // Block all damage
            };

            int targetHPBefore = target.Resources.CurrentHP;

            // Act
            var result = pipeline.ExecuteAction("test_melee", attacker, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.Equal(targetHPBefore, target.Resources.CurrentHP); // No damage taken
        }

        #endregion

        #region No Reaction System Tests

        [Fact]
        public void CastingSpell_WorksNormally_WithoutReactionSystem()
        {
            // Arrange - Pipeline without reactions
            var rules = new RulesEngine(42);
            var statuses = new StatusManager(rules);
            var pipeline = new EffectPipeline
            {
                Rules = rules,
                Statuses = statuses,
                Rng = new Random(42)
                // No Reactions or GetCombatants set
            };

            var caster = CreateCombatant("caster", Faction.Player, 100);
            var target = CreateCombatant("target", Faction.Hostile, 100);

            var spell = CreateSpellAbility(20, "fire");
            pipeline.RegisterAction(spell);

            // Act
            var result = pipeline.ExecuteAction("test_spell", caster, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.Equal(80, target.Resources.CurrentHP);
        }

        [Fact]
        public void DamageIsNotAffected_WithoutReactionSystem()
        {
            // Arrange - Pipeline without reactions
            var rules = new RulesEngine(42);
            var statuses = new StatusManager(rules);
            var pipeline = new EffectPipeline
            {
                Rules = rules,
                Statuses = statuses,
                Rng = new Random(42)
            };

            var attacker = CreateCombatant("attacker", Faction.Player, 100);
            var target = CreateCombatant("target", Faction.Hostile, 100);

            var melee = CreateMeleeAbility(30);
            pipeline.RegisterAction(melee);

            // Act
            var result = pipeline.ExecuteAction("test_melee", attacker, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.Equal(70, target.Resources.CurrentHP); // Full 30 damage
        }

        #endregion

        #region Eligibility Tests

        [Fact]
        public void Reactor_NotEligible_WhenNoReactionBudget()
        {
            // Arrange
            var (pipeline, rules, statuses, reactions) = CreatePipelineWithReactions();
            var caster = CreateCombatant("caster", Faction.Player, 100);
            var enemy = CreateCombatant("enemy", Faction.Hostile, 100);
            enemy.Position = new Godot.Vector3(10, 0, 0);

            // Consume enemy's reaction
            enemy.ActionBudget.ConsumeReaction();

            var target = CreateCombatant("target", Faction.Hostile, 100);

            var spell = CreateSpellAbility(20, "fire");
            pipeline.RegisterAction(spell);

            var counterspell = CreateCounterspellReaction();
            reactions.RegisterReaction(counterspell);
            reactions.GrantReaction(enemy.Id, "counterspell");

            var allCombatants = new List<Combatant> { caster, enemy, target };
            pipeline.GetCombatants = () => allCombatants;

            ReactionTriggerEventArgs? receivedArgs = null;
            pipeline.OnAbilityCastTrigger += (sender, args) => receivedArgs = args;

            // Act
            pipeline.ExecuteAction("test_spell", caster, new List<Combatant> { target });

            // Assert - No eligible reactors because enemy used reaction already
            Assert.Null(receivedArgs); // Event not fired because no eligible reactors
        }

        [Fact]
        public void InactiveReactor_NotEligible()
        {
            // Arrange
            var (pipeline, rules, statuses, reactions) = CreatePipelineWithReactions();
            var caster = CreateCombatant("caster", Faction.Player, 100);
            var enemy = CreateCombatant("enemy", Faction.Hostile, 100);
            enemy.Position = new Godot.Vector3(10, 0, 0);

            // Down the enemy
            enemy.Resources.TakeDamage(100);
            enemy.LifeState = CombatantLifeState.Downed;

            var target = CreateCombatant("target", Faction.Hostile, 100);

            var spell = CreateSpellAbility(20, "fire");
            pipeline.RegisterAction(spell);

            var counterspell = CreateCounterspellReaction();
            reactions.RegisterReaction(counterspell);
            reactions.GrantReaction(enemy.Id, "counterspell");

            var allCombatants = new List<Combatant> { caster, enemy, target };
            pipeline.GetCombatants = () => allCombatants;

            ReactionTriggerEventArgs? receivedArgs = null;
            pipeline.OnAbilityCastTrigger += (sender, args) => receivedArgs = args;

            // Act
            pipeline.ExecuteAction("test_spell", caster, new List<Combatant> { target });

            // Assert - No eligible reactors because enemy is downed
            Assert.Null(receivedArgs);
        }

        #endregion

        #region Context Data Tests

        [Fact]
        public void SpellCastContext_ContainsCorrectData()
        {
            // Arrange
            var (pipeline, rules, statuses, reactions) = CreatePipelineWithReactions();
            var caster = CreateCombatant("caster", Faction.Player, 100);
            caster.Position = new Godot.Vector3(5, 2, 3);
            var enemy = CreateCombatant("enemy", Faction.Hostile, 100);
            enemy.Position = new Godot.Vector3(10, 0, 0);
            var target = CreateCombatant("target", Faction.Hostile, 100);

            var spell = CreateSpellAbility(20, "fire");
            spell.Name = "Fireball";
            pipeline.RegisterAction(spell);

            var counterspell = CreateCounterspellReaction();
            reactions.RegisterReaction(counterspell);
            reactions.GrantReaction(enemy.Id, "counterspell");

            var allCombatants = new List<Combatant> { caster, enemy, target };
            pipeline.GetCombatants = () => allCombatants;

            ReactionTriggerEventArgs? receivedArgs = null;
            pipeline.OnAbilityCastTrigger += (sender, args) => receivedArgs = args;

            // Act
            pipeline.ExecuteAction("test_spell", caster, new List<Combatant> { target });

            // Assert
            Assert.NotNull(receivedArgs);
            Assert.Equal(caster.Position, receivedArgs.Context.Position);
            Assert.Equal("Fireball", receivedArgs.Context.Data["actionName"]);
            Assert.Equal(1, receivedArgs.Context.Data["targetCount"]);
            Assert.True(receivedArgs.Context.IsCancellable);
        }

        [Fact]
        public void DamageContext_ContainsCorrectData()
        {
            // Arrange
            var (pipeline, rules, statuses, reactions) = CreatePipelineWithReactions();
            var attacker = CreateCombatant("attacker", Faction.Player, 100);
            var target = CreateCombatant("target", Faction.Hostile, 100);
            target.Position = new Godot.Vector3(7, 8, 9);

            var melee = CreateMeleeAbility(35);
            pipeline.RegisterAction(melee);

            var shield = CreateShieldReaction();
            reactions.RegisterReaction(shield);
            reactions.GrantReaction(target.Id, "shield");

            var allCombatants = new List<Combatant> { attacker, target };
            pipeline.GetCombatants = () => allCombatants;

            ReactionTriggerEventArgs? receivedArgs = null;
            pipeline.OnDamageTrigger += (sender, args) => receivedArgs = args;

            // Act
            pipeline.ExecuteAction("test_melee", attacker, new List<Combatant> { target });

            // Assert
            Assert.NotNull(receivedArgs);
            Assert.Equal(target.Position, receivedArgs.Context.Position);
            Assert.Equal("slashing", receivedArgs.Context.Data["damageType"]);
            Assert.Equal(35, (int)receivedArgs.Context.Data["originalDamage"]);
            Assert.Equal(attacker.Id, receivedArgs.Context.TriggerSourceId);
            Assert.Equal(target.Id, receivedArgs.Context.AffectedId);
        }

        #endregion
    }
}
