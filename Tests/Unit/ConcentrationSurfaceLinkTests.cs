using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using QDND.Combat.Statuses;
using QDND.Combat.Rules;
using QDND.Combat.Entities;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for concentration-surface link removal when concentration breaks.
    /// Tests the ConcentrationSystem's ability to clean up linked surfaces.
    /// </summary>
    public class ConcentrationSurfaceLinkTests
    {
        private RulesEngine CreateRulesEngine() => new RulesEngine(seed: 42);

        private StatusManager CreateStatusManager(RulesEngine rules)
        {
            var manager = new StatusManager(rules);
            // Register a basic concentration status for testing
            manager.RegisterStatus(new StatusDefinition
            {
                Id = "spirit_guardians_buff",
                Name = "Spirit Guardians",
                DurationType = DurationType.Turns,
                DefaultDuration = 10,
                IsConcentration = true,
                IsBuff = true,
                Stacking = StackingBehavior.Refresh
            });
            manager.RegisterStatus(new StatusDefinition
            {
                Id = "haste_buff",
                Name = "Haste",
                DurationType = DurationType.Turns,
                DefaultDuration = 10,
                IsConcentration = true,
                IsBuff = true,
                Stacking = StackingBehavior.Refresh
            });
            return manager;
        }

        [Fact]
        public void ConcentrationEffectLink_HasSurfaceInstanceId()
        {
            var link = new ConcentrationEffectLink
            {
                StatusId = "spirit_guardians_buff",
                TargetId = "cleric1",
                SurfaceInstanceId = "surface_001"
            };

            Assert.Equal("surface_001", link.SurfaceInstanceId);
        }

        [Fact]
        public void BreakConcentration_CallsRemoveSurfaceById_WhenLinked()
        {
            var rules = CreateRulesEngine();
            var statuses = CreateStatusManager(rules);
            var concentration = new ConcentrationSystem(statuses, rules);

            var removedSurfaceIds = new List<string>();
            concentration.RemoveSurfaceById = id => removedSurfaceIds.Add(id);

            // Start concentration with a surface link
            concentration.StartConcentration(new ConcentrationInfo
            {
                CombatantId = "cleric1",
                ActionId = "spirit_guardians",
                StatusId = "spirit_guardians_buff",
                TargetId = "cleric1",
                LinkedEffects = new List<ConcentrationEffectLink>
                {
                    new()
                    {
                        StatusId = "spirit_guardians_buff",
                        TargetId = "cleric1",
                        SurfaceInstanceId = "surface_spirit_guardians_001"
                    }
                }
            });

            Assert.True(concentration.IsConcentrating("cleric1"));

            // Break concentration
            concentration.BreakConcentration("cleric1", "test");

            Assert.False(concentration.IsConcentrating("cleric1"));
            Assert.Contains("surface_spirit_guardians_001", removedSurfaceIds);
        }

        [Fact]
        public void BreakConcentration_FallbackRemovesByCreator_ForKnownSurfaceActions()
        {
            var rules = CreateRulesEngine();
            var statuses = CreateStatusManager(rules);
            var concentration = new ConcentrationSystem(statuses, rules);

            var removedCreatorIds = new List<string>();
            concentration.RemoveSurfacesByCreator = id => removedCreatorIds.Add(id);

            // Start concentration for a known surface-creating action with NO explicit surface link
            concentration.StartConcentration(new ConcentrationInfo
            {
                CombatantId = "cleric1",
                ActionId = "spirit_guardians",
                StatusId = "spirit_guardians_buff",
                TargetId = "cleric1"
            });

            concentration.BreakConcentration("cleric1", "test");

            Assert.Contains("cleric1", removedCreatorIds);
        }

        [Fact]
        public void BreakConcentration_DoesNotRemoveSurfaces_ForNonSurfaceActions()
        {
            var rules = CreateRulesEngine();
            var statuses = CreateStatusManager(rules);
            var concentration = new ConcentrationSystem(statuses, rules);

            var removedCreatorIds = new List<string>();
            concentration.RemoveSurfacesByCreator = id => removedCreatorIds.Add(id);

            // Start concentration for a non-surface action (haste)
            concentration.StartConcentration(new ConcentrationInfo
            {
                CombatantId = "wizard1",
                ActionId = "haste",
                StatusId = "haste_buff",
                TargetId = "fighter1"
            });

            concentration.BreakConcentration("wizard1", "test");

            // Should NOT call RemoveSurfacesByCreator for haste (not a surface spell)
            Assert.Empty(removedCreatorIds);
        }

        [Fact]
        public void BreakConcentration_PrefersSurfaceLinks_OverCreatorFallback()
        {
            var rules = CreateRulesEngine();
            var statuses = CreateStatusManager(rules);
            var concentration = new ConcentrationSystem(statuses, rules);

            var removedSurfaceIds = new List<string>();
            var removedCreatorIds = new List<string>();
            concentration.RemoveSurfaceById = id => removedSurfaceIds.Add(id);
            concentration.RemoveSurfacesByCreator = id => removedCreatorIds.Add(id);

            // Start concentration with an explicit surface link
            concentration.StartConcentration(new ConcentrationInfo
            {
                CombatantId = "cleric1",
                ActionId = "spirit_guardians",
                StatusId = "spirit_guardians_buff",
                TargetId = "cleric1",
                LinkedEffects = new List<ConcentrationEffectLink>
                {
                    new()
                    {
                        StatusId = "spirit_guardians_buff",
                        TargetId = "cleric1",
                        SurfaceInstanceId = "surface_001"
                    }
                }
            });

            concentration.BreakConcentration("cleric1", "test");

            // Should use the explicit link, NOT the creator fallback
            Assert.Contains("surface_001", removedSurfaceIds);
            Assert.Empty(removedCreatorIds);
        }

        [Fact]
        public void BreakConcentration_NullCallbacks_DoesNotThrow()
        {
            var rules = CreateRulesEngine();
            var statuses = CreateStatusManager(rules);
            var concentration = new ConcentrationSystem(statuses, rules);

            // No callbacks set — should not throw
            concentration.StartConcentration(new ConcentrationInfo
            {
                CombatantId = "cleric1",
                ActionId = "spirit_guardians",
                StatusId = "spirit_guardians_buff",
                TargetId = "cleric1",
                LinkedEffects = new List<ConcentrationEffectLink>
                {
                    new()
                    {
                        StatusId = "spirit_guardians_buff",
                        TargetId = "cleric1",
                        SurfaceInstanceId = "surface_001"
                    }
                }
            });

            var ex = Record.Exception(() => concentration.BreakConcentration("cleric1", "test"));
            Assert.Null(ex);
        }
    }
}
