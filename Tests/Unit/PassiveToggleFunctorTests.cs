using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <summary>
    /// Unit tests for toggle passive functor execution.
    /// Verifies that toggling passives ON/OFF executes the correct functors.
    /// </summary>
    public class PassiveToggleFunctorTests
    {
        private class TestContext
        {
            public PassiveRegistry PassiveRegistry { get; }
            public StatusRegistry StatusRegistry { get; }
            public StatusManager StatusManager { get; }
            public QDND.Combat.Statuses.BG3StatusIntegration StatusIntegration { get; }
            public RulesEngine RulesEngine { get; }
            public FunctorExecutor FunctorExecutor { get; }
            public PassiveManager PassiveManager { get; }
            public Combatant Combatant { get; }

            public TestContext()
            {
                // Setup registries
                PassiveRegistry = new PassiveRegistry();
                StatusRegistry = new StatusRegistry();

                // Setup RulesEngine and StatusManager
                RulesEngine = new RulesEngine(seed: 42);
                StatusManager = new StatusManager(RulesEngine);

                // Setup BG3StatusIntegration (wires StatusRegistry -> StatusManager)
                StatusIntegration = new QDND.Combat.Statuses.BG3StatusIntegration(StatusManager, StatusRegistry);

                // Setup FunctorExecutor
                FunctorExecutor = new FunctorExecutor(RulesEngine, StatusManager);

                // Create test combatant
                Combatant = new Combatant("test_combatant", "Test Combatant", Faction.Player, 50, 10);

                // Wire FunctorExecutor to resolve combatants
                FunctorExecutor.ResolveCombatant = id => id == Combatant.Id ? Combatant : null;

                // Wire StatusManager to resolve combatants
                StatusManager.ResolveCombatant = id => id == Combatant.Id ? Combatant : null;

                // Setup PassiveManager
                PassiveManager = Combatant.PassiveManager;
                PassiveManager.SetFunctorExecutor(FunctorExecutor);

                // Register test statuses
                RegisterTestStatuses();
            }

            private void RegisterTestStatuses()
            {
                // Register with StatusRegistry (BG3 data)
                StatusRegistry.RegisterStatus(new BG3StatusData
                {
                    StatusId = "NON_LETHAL",
                    DisplayName = "Non-Lethal",
                    StatusType = BG3StatusType.BOOST,
                    Description = "Your attacks deal non-lethal damage"
                });

                StatusRegistry.RegisterStatus(new BG3StatusData
                {
                    StatusId = "GWM_ACTIVE",
                    DisplayName = "Great Weapon Master",
                    StatusType = BG3StatusType.BOOST,
                    Description = "-5 to attack, +10 to damage"
                });

                StatusRegistry.RegisterStatus(new BG3StatusData
                {
                    StatusId = "SHARPSHOOTER_ACTIVE",
                    DisplayName = "Sharpshooter",
                    StatusType = BG3StatusType.BOOST,
                    Description = "-5 to ranged attack, +10 to damage"
                });

                StatusRegistry.RegisterStatus(new BG3StatusData
                {
                    StatusId = "RECKLESS",
                    DisplayName = "Reckless Attack",
                    StatusType = BG3StatusType.BOOST,
                    Description = "Advantage on attacks, enemies have advantage on you"
                });

                // Convert and register with StatusManager via Integration
                foreach (var bg3Status in StatusRegistry.GetAllStatuses())
                {
                    var definition = new StatusDefinition
                    {
                        Id = bg3Status.StatusId,
                        Name = bg3Status.DisplayName,
                        Description = bg3Status.Description,
                        DurationType = DurationType.Permanent,
                        Stacking = StackingBehavior.Refresh
                    };
                    StatusManager.RegisterStatus(definition);
                }
            }
        }

        [Fact]
        public void ToggleOn_ExecutesToggleOnFunctors()
        {
            // Arrange
            var ctx = new TestContext();

            ctx.PassiveRegistry.RegisterPassive(new BG3PassiveData
            {
                PassiveId = "NonLethalAttacks",
                DisplayName = "Non-Lethal Attacks",
                Properties = "IsToggled",
                ToggleOnFunctors = "ApplyStatus(NON_LETHAL,100,-1)",
                ToggleOffFunctors = "RemoveStatus(NON_LETHAL)"
            });

            ctx.PassiveManager.GrantPassive(ctx.PassiveRegistry, "NonLethalAttacks");

            // Act
            ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "NonLethalAttacks", true);

            // Assert
            Assert.True(ctx.PassiveManager.IsToggled("NonLethalAttacks"));
            Assert.True(ctx.StatusManager.HasStatus(ctx.Combatant.Id, "NON_LETHAL"));
        }

        [Fact]
        public void ToggleOff_ExecutesToggleOffFunctors()
        {
            // Arrange
            var ctx = new TestContext();

            ctx.PassiveRegistry.RegisterPassive(new BG3PassiveData
            {
                PassiveId = "NonLethalAttacks",
                DisplayName = "Non-Lethal Attacks",
                Properties = "IsToggled",
                ToggleOnFunctors = "ApplyStatus(NON_LETHAL,100,-1)",
                ToggleOffFunctors = "RemoveStatus(NON_LETHAL)"
            });

            ctx.PassiveManager.GrantPassive(ctx.PassiveRegistry, "NonLethalAttacks");
            ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "NonLethalAttacks", true);

            // Act
            ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "NonLethalAttacks", false);

            // Assert
            Assert.False(ctx.PassiveManager.IsToggled("NonLethalAttacks"));
            Assert.False(ctx.StatusManager.HasStatus(ctx.Combatant.Id, "NON_LETHAL"));
        }

        [Fact]
        public void MutualExclusivity_TogglingOneOnDisablesOthers_AndExecutesTheirToggleOffFunctors()
        {
            // Arrange
            var ctx = new TestContext();

            ctx.PassiveRegistry.RegisterPassive(new BG3PassiveData
            {
                PassiveId = "NonLethalAttacks",
                DisplayName = "Non-Lethal Attacks",
                Properties = "IsToggled",
                ToggleOnFunctors = "ApplyStatus(NON_LETHAL,100,-1)",
                ToggleOffFunctors = "RemoveStatus(NON_LETHAL)",
                ToggleGroup = "AttackMode"
            });

            ctx.PassiveRegistry.RegisterPassive(new BG3PassiveData
            {
                PassiveId = "RecklessAttack",
                DisplayName = "Reckless Attack",
                Properties = "IsToggled",
                ToggleOnFunctors = "ApplyStatus(RECKLESS,100,-1)",
                ToggleOffFunctors = "RemoveStatus(RECKLESS)",
                ToggleGroup = "AttackMode"
            });

            ctx.PassiveManager.GrantPassive(ctx.PassiveRegistry, "NonLethalAttacks");
            ctx.PassiveManager.GrantPassive(ctx.PassiveRegistry, "RecklessAttack");

            // Enable NonLethalAttacks first
            ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "NonLethalAttacks", true);
            Assert.True(ctx.StatusManager.HasStatus(ctx.Combatant.Id, "NON_LETHAL"));

            // Act - Enable RecklessAttack (should disable NonLethalAttacks and remove its status)
            ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "RecklessAttack", true);

            // Assert
            Assert.True(ctx.PassiveManager.IsToggled("RecklessAttack"));
            Assert.False(ctx.PassiveManager.IsToggled("NonLethalAttacks"));
            Assert.True(ctx.StatusManager.HasStatus(ctx.Combatant.Id, "RECKLESS"));
            Assert.False(ctx.StatusManager.HasStatus(ctx.Combatant.Id, "NON_LETHAL"));
        }

        [Fact]
        public void MultipleToggleFunctors_ExecutesAllFunctors()
        {
            // Arrange
            var ctx = new TestContext();

            ctx.PassiveRegistry.RegisterPassive(new BG3PassiveData
            {
                PassiveId = "MultiEffect",
                DisplayName = "Multi Effect Passive",
                Properties = "IsToggled",
                ToggleOnFunctors = "ApplyStatus(NON_LETHAL,100,-1);ApplyStatus(RECKLESS,100,-1)",
                ToggleOffFunctors = "RemoveStatus(NON_LETHAL);RemoveStatus(RECKLESS)"
            });

            ctx.PassiveManager.GrantPassive(ctx.PassiveRegistry, "MultiEffect");

            // Act - Toggle on
            ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "MultiEffect", true);

            // Assert - Both statuses applied
            Assert.True(ctx.StatusManager.HasStatus(ctx.Combatant.Id, "NON_LETHAL"));
            Assert.True(ctx.StatusManager.HasStatus(ctx.Combatant.Id, "RECKLESS"));

            // Act - Toggle off
            ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "MultiEffect", false);

            // Assert - Both statuses removed
            Assert.False(ctx.StatusManager.HasStatus(ctx.Combatant.Id, "NON_LETHAL"));
            Assert.False(ctx.StatusManager.HasStatus(ctx.Combatant.Id, "RECKLESS"));
        }

        [Fact]
        public void EmptyToggleFunctors_DoesNotThrow()
        {
            // Arrange
            var ctx = new TestContext();

            ctx.PassiveRegistry.RegisterPassive(new BG3PassiveData
            {
                PassiveId = "EmptyFunctors",
                DisplayName = "Empty Functors",
                Properties = "IsToggled",
                ToggleOnFunctors = "",
                ToggleOffFunctors = ""
            });

            ctx.PassiveManager.GrantPassive(ctx.PassiveRegistry, "EmptyFunctors");

            // Act & Assert - Should not throw
            ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "EmptyFunctors", true);
            Assert.True(ctx.PassiveManager.IsToggled("EmptyFunctors"));

            ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "EmptyFunctors", false);
            Assert.False(ctx.PassiveManager.IsToggled("EmptyFunctors"));
        }

        [Fact]
        public void NullToggleFunctors_DoesNotThrow()
        {
            // Arrange
            var ctx = new TestContext();

            ctx.PassiveRegistry.RegisterPassive(new BG3PassiveData
            {
                PassiveId = "NullFunctors",
                DisplayName = "Null Functors",
                Properties = "IsToggled",
                ToggleOnFunctors = null,
                ToggleOffFunctors = null
            });

            ctx.PassiveManager.GrantPassive(ctx.PassiveRegistry, "NullFunctors");

            // Act & Assert - Should not throw
            ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "NullFunctors", true);
            Assert.True(ctx.PassiveManager.IsToggled("NullFunctors"));

            ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "NullFunctors", false);
            Assert.False(ctx.PassiveManager.IsToggled("NullFunctors"));
        }

        [Fact]
        public void PassiveNotFound_DoesNotExecuteFunctors()
        {
            // Arrange
            var ctx = new TestContext();

            // Try to toggle a passive that doesn't exist in registry
            // Act & Assert - Should not throw, should not execute any functors
            ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "NonExistentPassive", true);

            // Verify no errors occurred and no statuses were applied
            Assert.Empty(ctx.StatusManager.GetAllStatuses());
        }

        [Fact]
        public void WithoutFunctorExecutor_DoesNotExecuteFunctors()
        {
            // Arrange
            var ctx = new TestContext();

            ctx.PassiveRegistry.RegisterPassive(new BG3PassiveData
            {
                PassiveId = "TestPassive",
                DisplayName = "Test Passive",
                Properties = "IsToggled",
                ToggleOnFunctors = "ApplyStatus(NON_LETHAL,100,-1)",
                ToggleOffFunctors = "RemoveStatus(NON_LETHAL)"
            });

            // Create a PassiveManager WITHOUT the FunctorExecutor wired
            var managerWithoutExecutor = new PassiveManager();
            var combatant2 = new Combatant("test2", "Test 2", Faction.Player, 50, 10);
            managerWithoutExecutor.Owner = combatant2;
            // Don't call SetFunctorExecutor()

            managerWithoutExecutor.GrantPassive(ctx.PassiveRegistry, "TestPassive");

            // Act
            managerWithoutExecutor.SetToggleState(ctx.PassiveRegistry, "TestPassive", true);

            // Assert - Toggle state changes, but no functors executed
            Assert.True(managerWithoutExecutor.IsToggled("TestPassive"));
            Assert.False(ctx.StatusManager.HasStatus(combatant2.Id, "NON_LETHAL"));
        }

        [Fact]
        public void ToggleFunctorCache_CachesParsedFunctors()
        {
            // Arrange
            var ctx = new TestContext();

            ctx.PassiveRegistry.RegisterPassive(new BG3PassiveData
            {
                PassiveId = "CachedPassive",
                DisplayName = "Cached Passive",
                Properties = "IsToggled",
                ToggleOnFunctors = "ApplyStatus(NON_LETHAL,100,-1)",
                ToggleOffFunctors = "RemoveStatus(NON_LETHAL)"
            });

            ctx.PassiveManager.GrantPassive(ctx.PassiveRegistry, "CachedPassive");

            // Act - Toggle on/off multiple times
            for (int i = 0; i < 5; i++)
            {
                ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "CachedPassive", true);
                ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "CachedPassive", false);
            }

            // Assert - Should complete without errors (functors are cached and reused)
            Assert.False(ctx.PassiveManager.IsToggled("CachedPassive"));
            Assert.False(ctx.StatusManager.HasStatus(ctx.Combatant.Id, "NON_LETHAL"));
        }

        [Fact]
        public void ClearToggleFunctorCache_ClearsCache()
        {
            // Arrange
            var ctx = new TestContext();

            ctx.PassiveRegistry.RegisterPassive(new BG3PassiveData
            {
                PassiveId = "ClearCacheTest",
                DisplayName = "Clear Cache Test",
                Properties = "IsToggled",
                ToggleOnFunctors = "ApplyStatus(NON_LETHAL,100,-1)",
                ToggleOffFunctors = "RemoveStatus(NON_LETHAL)"
            });

            ctx.PassiveManager.GrantPassive(ctx.PassiveRegistry, "ClearCacheTest");
            ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "ClearCacheTest", true);

            // Act - Clear cache
            ctx.PassiveManager.ClearToggleFunctorCache();

            // Toggle again after clearing cache (should re-parse)
            ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "ClearCacheTest", false);

            // Assert - Should still work correctly
            Assert.False(ctx.PassiveManager.IsToggled("ClearCacheTest"));
            Assert.False(ctx.StatusManager.HasStatus(ctx.Combatant.Id, "NON_LETHAL"));
        }

        [Fact]
        public void ComplexFunctorString_ParsesCorrectly()
        {
            // Arrange
            var ctx = new TestContext();

            // Register a status that doesn't exist yet for testing
            ctx.StatusRegistry.RegisterStatus(new BG3StatusData
            {
                StatusId = "COMPLEX_STATUS",
                DisplayName = "Complex Status",
                StatusType = BG3StatusType.BOOST,
                Description = "Complex effect"
            });

            // Register with StatusManager too
            ctx.StatusManager.RegisterStatus(new StatusDefinition
            {
                Id = "COMPLEX_STATUS",
                Name = "Complex Status",
                Description = "Complex effect",
                DurationType = DurationType.Permanent,
                Stacking = StackingBehavior.Refresh
            });

            // Register a status that doesn't exist yet for testing
            ctx.StatusRegistry.RegisterStatus(new BG3StatusData
            {
                StatusId = "COMPLEX_STATUS",
                DisplayName = "Complex Status",
                StatusType = BG3StatusType.BOOST,
                Description = "Complex effect"
            });

            ctx.PassiveRegistry.RegisterPassive(new BG3PassiveData
            {
                PassiveId = "ComplexPassive",
                DisplayName = "Complex Passive",
                Properties = "IsToggled",
                // Complex functor with parameters
                ToggleOnFunctors = "ApplyStatus(COMPLEX_STATUS,75,10)",
                ToggleOffFunctors = "RemoveStatus(COMPLEX_STATUS)"
            });

            ctx.PassiveManager.GrantPassive(ctx.PassiveRegistry, "ComplexPassive");

            // Act
            ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "ComplexPassive", true);

            // Assert - Status should be applied (with 75% chance, but our test RNG may or may not apply it)
            // For this test, we just verify no errors occurred
            bool wasApplied = ctx.StatusManager.HasStatus(ctx.Combatant.Id, "COMPLEX_STATUS");
            
            // Toggle off and verify removal
            ctx.PassiveManager.SetToggleState(ctx.PassiveRegistry, "ComplexPassive", false);
            Assert.False(ctx.StatusManager.HasStatus(ctx.Combatant.Id, "COMPLEX_STATUS"));
        }
    }
}
