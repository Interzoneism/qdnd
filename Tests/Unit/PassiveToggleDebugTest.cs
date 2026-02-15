using System;
using Xunit;
using QDND.Combat.Passives;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Functors;
using QDND.Combat.Statuses;
using QDND.Data.Passives;
using QDND.Data.Statuses;

namespace QDND.Tests.Unit
{
    public class PassiveToggleDebugTest
    {
        [Fact]
        public void Debug_SimpleToggleTest()
        {
            // Setup
            var rulesEngine = new RulesEngine(seed: 42);
            var statusManager = new StatusManager(rulesEngine);
            var statusRegistry = new StatusRegistry();
            var statusIntegration = new BG3StatusIntegration(statusManager, statusRegistry);
            var passiveRegistry = new PassiveRegistry();
            var functorExecutor = new FunctorExecutor(rulesEngine, statusManager);
            
            var combatant = new Combatant("test", "Test", Faction.Player, 50, 10);
            
            // Wire resolvers
            functorExecutor.ResolveCombatant = id => id == combatant.Id ? combatant : null;
            statusManager.ResolveCombatant = id => id == combatant.Id ? combatant : null;
            
            // Register status
            statusRegistry.RegisterStatus(new BG3StatusData
            {
                StatusId = "TEST_STATUS",
                DisplayName = "Test Status",
                StatusType = BG3StatusType.BOOST
            });
            
            statusManager.RegisterStatus(new StatusDefinition
            {
                Id = "TEST_STATUS",
                Name = "Test Status",
                DurationType = DurationType.Permanent,
                Stacking = StackingBehavior.Refresh
            });
            
            // Register passive
            passiveRegistry.RegisterPassive(new BG3PassiveData
            {
                PassiveId = "TEST_PASSIVE",
                DisplayName = "Test Passive",
                Properties = "IsToggled",
                ToggleOnFunctors = "ApplyStatus(TEST_STATUS,100,-1)",
                ToggleOffFunctors = "RemoveStatus(TEST_STATUS)"
            });
            
            //Wire executor to PassiveManager
            combatant.PassiveManager.SetFunctorExecutor(functorExecutor);
            
            // Grant passive
            bool granted = combatant.PassiveManager.GrantPassive(passiveRegistry, "TEST_PASSIVE");
            Console.WriteLine($"Passive granted: {granted}");
            Assert.True(granted);
            
            // Toggle ON
            Console.WriteLine("\n=== Toggling ON ===");
            combatant.PassiveManager.SetToggleState(passiveRegistry, "TEST_PASSIVE", true);
            
            bool isToggled = combatant.PassiveManager.IsToggled("TEST_PASSIVE");
            Console.WriteLine($"Is toggled: {isToggled}");
            Assert.True(isToggled);
            
            bool hasStatus = statusManager.HasStatus(combatant.Id, "TEST_STATUS");
            Console.WriteLine($"Has status: {hasStatus}");
            
            if (!hasStatus)
            {
                Console.WriteLine("\nAll statuses:");
                foreach (var status in statusManager.GetAllStatuses())
                {
                    Console.WriteLine($"  - {status.Definition.Id} on {status.TargetId}");
                }
            }
            
            Assert.True(hasStatus, "Status should be applied when toggled ON");
        }
    }
}
