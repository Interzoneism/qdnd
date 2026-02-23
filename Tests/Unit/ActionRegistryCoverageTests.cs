using System;
using System.IO;
using System.Linq;
using Xunit;
using QDND.Combat.Actions;
using QDND.Data.Actions;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Phase 10 parity regression: verifies that ActionRegistry loads cleanly and
    /// that every registered action satisfies structural invariants.
    /// </summary>
    public class ActionRegistryCoverageTests
    {
        // -------------------------------------------------------------------
        //  Helpers
        // -------------------------------------------------------------------

        private static string FindRepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "project.godot")))
                    return dir;
                dir = Directory.GetParent(dir)?.FullName;
            }
            // Fallback: walk up four levels from the test binary output folder
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        }

        private static ActionRegistry BuildRegistry()
        {
            var bg3DataPath = Path.Combine(FindRepoRoot(), "BG3_Data");
            var registry = new ActionRegistry();
            ActionRegistryInitializer.Initialize(registry, bg3DataPath, verboseLogging: false);
            return registry;
        }

        // -------------------------------------------------------------------
        //  Tests
        // -------------------------------------------------------------------

        [Fact]
        public void LoadAllSpells_ProducesZeroErrors()
        {
            var bg3DataPath = Path.Combine(FindRepoRoot(), "BG3_Data");
            var registry = new ActionRegistry();
            var result = ActionRegistryInitializer.Initialize(registry, bg3DataPath, verboseLogging: false);

            Assert.True(result.Success, $"Initialization failed: {result.ErrorMessage}");
            Assert.Equal(0, result.ErrorCount);
            Assert.Empty(registry.Errors);
        }

        [Fact]
        public void AllActions_HaveNonEmptyId()
        {
            var registry = BuildRegistry();
            var actions = registry.GetAllActions();

            Assert.NotEmpty(actions);
            Assert.All(actions, a =>
                Assert.False(string.IsNullOrEmpty(a.Id),
                    $"Action '{a.Name}' has a null/empty Id"));
        }

        [Fact]
        public void AllActions_HaveNonEmptyName()
        {
            var registry = BuildRegistry();
            var actions = registry.GetAllActions();

            Assert.NotEmpty(actions);
            Assert.All(actions, a =>
                Assert.False(string.IsNullOrEmpty(a.Name),
                    $"Action '{a.Id}' has a null/empty Name"));
        }

        [Fact]
        public void AllActions_HaveValidSpellLevel()
        {
            var registry = BuildRegistry();
            var actions = registry.GetAllActions();

            Assert.NotEmpty(actions);
            Assert.All(actions, a =>
                Assert.True(a.SpellLevel >= 0,
                    $"Action '{a.Id}' has negative SpellLevel: {a.SpellLevel}"));
        }

        [Fact]
        public void GetConcentrationActions_AllFlagConcentration()
        {
            var registry = BuildRegistry();
            var concentrationActions = registry.GetConcentrationActions();

            // Every action returned by GetConcentrationActions must have the flag set
            Assert.All(concentrationActions, a =>
                Assert.True(a.RequiresConcentration,
                    $"Action '{a.Id}' returned by GetConcentrationActions but RequiresConcentration is false"));
        }
    }
}
