using System.Collections.Generic;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Services;
using QDND.Data;

namespace QDND.Tests.Integration
{
    public class ScenarioLoaderActionResolutionTests
    {
        private static ActionRegistry CreateActionRegistryWithCanonicalIds()
        {
            var registry = new ActionRegistry();
            registry.RegisterAction(new ActionDefinition { Id = "main_hand_attack", Name = "Main Hand Attack" }, overwrite: false);
            registry.RegisterAction(new ActionDefinition { Id = "ranged_attack", Name = "Ranged Attack" }, overwrite: false);
            registry.RegisterAction(new ActionDefinition { Id = "fireball", Name = "Fireball" }, overwrite: false);
            registry.RegisterAction(new ActionDefinition { Id = "magic_missile", Name = "Magic Missile" }, overwrite: false);
            registry.RegisterAction(new ActionDefinition { Id = "fire_bolt", Name = "Fire Bolt" }, overwrite: false);
            registry.RegisterAction(new ActionDefinition { Id = "action_surge", Name = "Action Surge" }, overwrite: false);
            return registry;
        }

        [Fact]
        public void SpawnCombatants_ResolvesLegacyScenarioActionsToCanonicalIds()
        {
            var loader = new ScenarioLoader();
            loader.SetActionRegistry(CreateActionRegistryWithCanonicalIds());

            var scenario = new ScenarioDefinition
            {
                Id = "resolution_test",
                Name = "Resolution Test",
                Seed = 1,
                Units = new List<ScenarioUnit>
                {
                    new()
                    {
                        Id = "player_1",
                        Name = "Fighter",
                        Faction = "player",
                        HP = 40,
                        Initiative = 12,
                        KnownActions = new List<string>
                        {
                            "Target_MainHandAttack",
                            "Shout_ActionSurge",
                            "Unknown_Action_404"
                        }
                    }
                }
            };

            var turnQueue = new TurnQueueService();
            var combatants = loader.SpawnCombatants(scenario, turnQueue);

            Assert.Single(combatants);
            var actions = combatants[0].KnownActions;

            Assert.Contains("main_hand_attack", actions);
            Assert.Contains("action_surge", actions);
            Assert.DoesNotContain("Target_MainHandAttack", actions);
            Assert.DoesNotContain("Shout_ActionSurge", actions);
            Assert.DoesNotContain("Unknown_Action_404", actions);
        }

        [Fact]
        public void SpawnCombatants_NoKnownActions_GetsOnlyBasicAttack()
        {
            var loader = new ScenarioLoader();
            loader.SetActionRegistry(CreateActionRegistryWithCanonicalIds());

            var scenario = new ScenarioDefinition
            {
                Id = "default_resolution_test",
                Name = "Default Resolution Test",
                Seed = 2,
                Units = new List<ScenarioUnit>
                {
                    new()
                    {
                        Id = "wizard_1",
                        Name = "Wizard",
                        Faction = "player",
                        HP = 30,
                        Initiative = 14,
                        KnownActions = null
                    }
                }
            };

            var turnQueue = new TurnQueueService();
            var combatants = loader.SpawnCombatants(scenario, turnQueue);

            Assert.Single(combatants);
            var actions = combatants[0].KnownActions;

            // No name-based fallback — EnsureBasicAttack provides only main_hand_attack
            Assert.Contains("main_hand_attack", actions);
            Assert.DoesNotContain("fireball", actions);
            Assert.DoesNotContain("magic_missile", actions);
            Assert.DoesNotContain("fire_bolt", actions);
        }
    }
}
