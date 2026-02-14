using Xunit;
using QDND.Combat.Statuses;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Functors;
using QDND.Combat.Entities;
using QDND.Data.Passives;
using QDND.Data.Statuses;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Tests.Unit
{
    public class PassiveFunctorIntegrationTests
    {
        [Fact]
        public void OnAttack_ExecutesFunctors_WhenPassiveHasOnAttackContext()
        {
            // Arrange
            var rulesEngine = new RulesEngine(seed: 42);
            var statusManager = new StatusManager(rulesEngine);
            var statusRegistry = new StatusRegistry();
            var passiveRegistry = new PassiveRegistry();

            // Register a passive with OnAttack functor
            var passive = new BG3PassiveData
            {
                PassiveId = "TestOnAttackPassive",
                StatsFunctorContext = "OnAttack",
                StatsFunctors = "DealDamage(1d6,Fire)"
            };
            passiveRegistry.RegisterPassive(passive);

            var attacker = CreateTestCombatant("attacker");
            var target = CreateTestCombatant("target");

            // Grant the passive to the attacker
            attacker.PassiveManager.GrantPassive(passiveRegistry, "TestOnAttackPassive");

            var executor = new FunctorExecutor(rulesEngine, statusManager);
            executor.ResolveCombatant = (id) => id == attacker.Id ? attacker : (id == target.Id ? target : null);

            var integration = new PassiveFunctorIntegration(rulesEngine, passiveRegistry, executor);
            integration.ResolveCombatant = executor.ResolveCombatant;

            int initialTargetHP = target.Resources.CurrentHP;

            // Act - Dispatch an AttackResolved event
            var attackEvent = new RuleEvent
            {
                Type = RuleEventType.AttackResolved,
                SourceId = attacker.Id,
                TargetId = target.Id
            };
            rulesEngine.Events.Dispatch(attackEvent);

            // Assert - Target should have taken fire damage from the functor
            Assert.True(target.Resources.CurrentHP < initialTargetHP, 
                $"Expected target HP to decrease from {initialTargetHP}, but got {target.Resources.CurrentHP}");
        }

        [Fact]
        public void OnDamaged_ExecutesFunctors_WhenPassiveHasOnDamagedContext()
        {
            // Arrange
            var rulesEngine = new RulesEngine(seed: 100);
            var statusManager = new StatusManager(rulesEngine);
            var statusRegistry = new StatusRegistry();
            var passiveRegistry = new PassiveRegistry();

            // Register a passive with OnDamaged functor (like Hellish Rebuke)
            var passive = new BG3PassiveData
            {
                PassiveId = "HellishRebukePassive",
                StatsFunctorContext = "OnDamaged",
                StatsFunctors = "DealDamage(2d10,Fire)"
            };
            passiveRegistry.RegisterPassive(passive);

            var defender = CreateTestCombatant("defender");
            var attacker = CreateTestCombatant("attacker");

            // Grant the passive to the defender
            defender.PassiveManager.GrantPassive(passiveRegistry, "HellishRebukePassive");

            var executor = new FunctorExecutor(rulesEngine, statusManager);
            executor.ResolveCombatant = (id) => id == defender.Id ? defender : (id == attacker.Id ? attacker : null);

            var integration = new PassiveFunctorIntegration(rulesEngine, passiveRegistry, executor);
            integration.ResolveCombatant = executor.ResolveCombatant;

            int initialAttackerHP = attacker.Resources.CurrentHP;

            // Act - Dispatch a DamageTaken event for the defender
            var damageEvent = new RuleEvent
            {
                Type = RuleEventType.DamageTaken,
                SourceId = attacker.Id,
                TargetId = defender.Id,
                Value = 10
            };
            rulesEngine.Events.Dispatch(damageEvent);

            // Assert - Attacker should have taken fire damage from the retaliatory functor
            Assert.True(attacker.Resources.CurrentHP < initialAttackerHP,
                $"Expected attacker HP to decrease from {initialAttackerHP}, but got {attacker.Resources.CurrentHP}");
        }

        [Fact]
        public void MultipleCombatants_OnlyOwnerExecutesFunctors()
        {
            // Arrange
            var rulesEngine = new RulesEngine(seed: 50);
            var statusManager = new StatusManager(rulesEngine);
            var statusRegistry = new StatusRegistry();
            var passiveRegistry = new PassiveRegistry();

            var passive = new BG3PassiveData
            {
                PassiveId = "TestPassive",
                StatsFunctorContext = "OnAttack",
                StatsFunctors = "DealDamage(5,Fire)"
            };
            passiveRegistry.RegisterPassive(passive);

            var combatant1 = CreateTestCombatant("combatant1");
            var combatant2 = CreateTestCombatant("combatant2");
            var target = CreateTestCombatant("target");

            // Only combatant1 has the passive
            combatant1.PassiveManager.GrantPassive(passiveRegistry, "TestPassive");

            var executor = new FunctorExecutor(rulesEngine, statusManager);
            var combatants = new Dictionary<string, Combatant>
            {
                [combatant1.Id] = combatant1,
                [combatant2.Id] = combatant2,
                [target.Id] = target
            };
            executor.ResolveCombatant = (id) => combatants.TryGetValue(id, out var c) ? c : null;

            var integration = new PassiveFunctorIntegration(rulesEngine, passiveRegistry, executor);
            integration.ResolveCombatant = executor.ResolveCombatant;

            int initialHP = target.Resources.CurrentHP;

            // Act - combatant2 attacks (doesn't have the passive)
            rulesEngine.Events.Dispatch(new RuleEvent
            {
                Type = RuleEventType.AttackResolved,
                SourceId = combatant2.Id,
                TargetId = target.Id
            });

            // Assert - Target should NOT have taken extra damage
            Assert.Equal(initialHP, target.Resources.CurrentHP);

            // Act - combatant1 attacks (has the passive)
            rulesEngine.Events.Dispatch(new RuleEvent
            {
                Type = RuleEventType.AttackResolved,
                SourceId = combatant1.Id,
                TargetId = target.Id
            });

            // Assert - Target should have taken damage
            Assert.True(target.Resources.CurrentHP < initialHP,
                $"Expected target HP to decrease from {initialHP}, but got {target.Resources.CurrentHP}");
        }

        [Fact]
        public void Cleanup_UnsubscribesAllHandlers()
        {
            // Arrange
            var rulesEngine = new RulesEngine(seed: 1);
            var statusManager = new StatusManager(rulesEngine);
            var statusRegistry = new StatusRegistry();
            var passiveRegistry = new PassiveRegistry();

            var passive = new BG3PassiveData
            {
                PassiveId = "CleanupTestPassive",
                StatsFunctorContext = "OnAttack",
                StatsFunctors = "DealDamage(1,Fire)"
            };
            passiveRegistry.RegisterPassive(passive);

            var combatant = CreateTestCombatant("combatant");
            combatant.PassiveManager.GrantPassive(passiveRegistry, "CleanupTestPassive");

            var executor = new FunctorExecutor(rulesEngine, statusManager);
            executor.ResolveCombatant = (id) => id == combatant.Id ? combatant : null;

            var integration = new PassiveFunctorIntegration(rulesEngine, passiveRegistry, executor);
            integration.ResolveCombatant = executor.ResolveCombatant;

            // Act - Cleanup
            integration.Dispose();

            // Dispatch an attack event after cleanup
            var target = CreateTestCombatant("target");
            int initialHP = target.Resources.CurrentHP;
            rulesEngine.Events.Dispatch(new RuleEvent
            {
                Type = RuleEventType.AttackResolved,
                SourceId = combatant.Id,
                TargetId = target.Id
            });

            // Assert - No damage should be dealt (handlers were unsubscribed)
            Assert.Equal(initialHP, target.Resources.CurrentHP);
        }

        private Combatant CreateTestCombatant(string id)
        {
            var combatant = new Combatant(id, id, Faction.Player, 100, 100);
            combatant.PassiveManager.Owner = combatant;
            return combatant;
        }
    }
}
