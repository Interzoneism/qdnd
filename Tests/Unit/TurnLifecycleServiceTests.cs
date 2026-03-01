using System;
using System.Collections.Generic;
using Xunit;
using QDND.Combat.Arena;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Boosts;
using QDND.Combat.Services;
using QDND.Combat.Statuses;

namespace QDND.Tests.Unit
{
    public class TurnLifecycleServiceTests
    {
        private sealed class FixedRandom : Random
        {
            private readonly Queue<int> _values;

            public FixedRandom(params int[] values)
            {
                _values = new Queue<int>(values ?? Array.Empty<int>());
            }

            public override int Next(int minValue, int maxValue)
            {
                if (_values.Count == 0)
                    return minValue;

                return _values.Dequeue();
            }
        }

        private TurnLifecycleService CreateService(
            Func<Random> getRng,
            StatusManager statusManager,
            RulesEngine rulesEngine)
        {
            return new TurnLifecycleService(
                turnQueue: null,
                stateMachine: null,
                effectPipeline: null,
                statusManager: statusManager,
                surfaceManager: null,
                rulesEngine: rulesEngine,
                resourceManager: null,
                presentationService: null,
                combatLog: null,
                actionBarModel: null,
                turnTrackerModel: null,
                resourceBarModel: null,
                combatantVisuals: new Dictionary<string, CombatantVisual>(),
                defaultMovePoints: 30f,
                getCombatants: () => Array.Empty<Combatant>(),
                getRng: getRng,
                executeAITurn: _ => { },
                selectCombatant: _ => { },
                centerCameraOnCombatant: _ => { },
                populateActionBar: _ => { },
                dispatchRuleWindow: (_, _, _) => { },
                resumeDecisionStateIfExecuting: _ => { },
                createTimer: _ => null,
                isAutoBattleMode: () => false,
                useBuiltInAI: () => false,
                log: _ => { });
        }

        private static Combatant CreateDownedCombatant(string id)
        {
            var combatant = new Combatant(id, id, Faction.Player, 100, 10)
            {
                LifeState = CombatantLifeState.Downed
            };
            combatant.Resources.CurrentHP = 0;
            combatant.ResetDeathSaves();
            return combatant;
        }

        [Fact]
        public void ProcessDeathSave_RollBonusDice_ModifiesOutcome()
        {
            var rules = new RulesEngine(1);
            var statuses = new StatusManager(rules);
            var random = new FixedRandom(8, 3); // d20=8, bonus die 1d4=3
            var service = CreateService(() => random, statuses, rulesEngine: null);
            var combatant = CreateDownedCombatant("downed_bonus");

            BoostApplicator.ApplyBoosts(combatant, "RollBonus(DeathSavingThrow,1d4)", "Test", "deathsave_bonus");

            service.ProcessDeathSave(combatant);

            Assert.Equal(1, combatant.DeathSaveSuccesses);
            Assert.Equal(0, combatant.DeathSaveFailures);
            Assert.Equal(CombatantLifeState.Downed, combatant.LifeState);
        }

        [Fact]
        public void ProcessDeathSave_NaturalOne_PrecedesMinimumRollAndBonus()
        {
            var rules = new RulesEngine(1);
            var statuses = new StatusManager(rules);
            var random = new FixedRandom(1, 4); // natural 1 should short-circuit before modifiers
            var service = CreateService(() => random, statuses, rulesEngine: null);
            var combatant = CreateDownedCombatant("downed_nat1");

            BoostApplicator.ApplyBoosts(combatant, "MinimumRollResult(DeathSave,20);RollBonus(DeathSavingThrow,1d4)", "Test", "deathsave_floor");

            service.ProcessDeathSave(combatant);

            Assert.Equal(0, combatant.DeathSaveSuccesses);
            Assert.Equal(2, combatant.DeathSaveFailures);
            Assert.Equal(CombatantLifeState.Downed, combatant.LifeState);
        }
    }
}
