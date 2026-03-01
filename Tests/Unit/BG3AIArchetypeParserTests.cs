using System;
using System.Collections.Generic;
using System.IO;
using QDND.Data.AI;
using Xunit;

namespace QDND.Tests.Unit
{
    public class BG3AIArchetypeParserTests
    {
        private static string FindRepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "project.godot")))
                    return dir;
                dir = Directory.GetParent(dir)?.FullName;
            }

            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        }

        [Fact]
        public void LoadFromDirectory_ResolvesInheritanceAndOperations()
        {
            var aiPath = Path.Combine(FindRepoRoot(), "BG3_Data", "AI");
            var registry = new BG3AIRegistry();

            var success = registry.LoadFromDirectory(aiPath);

            Assert.True(success, string.Join(Environment.NewLine, registry.Errors));
            Assert.True(registry.HasArchetype("melee_smart"));
            Assert.True(registry.HasArchetype("mindflayer"));
            Assert.True(registry.HasArchetype("goblin_melee"));

            Assert.True(registry.TryGetResolvedSettings("melee_smart", out var meleeSmart));
            Assert.True(meleeSmart.TryGetValue("MODIFIER_HIT_CHANCE_STUPIDITY", out var meleeSmartHitReasoning));
            Assert.Equal(0.7f, meleeSmartHitReasoning, 3);

            Assert.True(registry.TryGetResolvedSettings("goblin_melee", out var goblinMelee));
            Assert.True(goblinMelee.TryGetValue("MODIFIER_HIT_CHANCE_STUPIDITY", out var goblinHitReasoning));
            Assert.Equal(1.0f, goblinHitReasoning, 3);

            Assert.True(registry.TryGetResolvedSettings("mindflayer", out var mindflayer));
            Assert.True(mindflayer.TryGetValue("MULTIPLIER_KILL_ENEMY", out var killEnemy));
            Assert.Equal(1.70f, killEnemy, 3);
        }

        [Fact]
        public void LoadFromDirectory_UsesBaseFallbackWhenNoUsingDirective()
        {
            var aiPath = Path.Combine(FindRepoRoot(), "BG3_Data", "AI");
            var registry = new BG3AIRegistry();
            Assert.True(registry.LoadFromDirectory(aiPath), string.Join(Environment.NewLine, registry.Errors));

            Assert.True(registry.TryGetResolvedSettings("act2_TWN_nurse", out var nurse));
            Assert.True(nurse.TryGetValue("SCORE_MOD", out var scoreMod));
            Assert.Equal(100.0f, scoreMod, 3);
        }

        [Fact]
        public void LoadFromDirectory_ParsesSurfaceCombos()
        {
            var aiPath = Path.Combine(FindRepoRoot(), "BG3_Data", "AI");
            var registry = new BG3AIRegistry();
            Assert.True(registry.LoadFromDirectory(aiPath), string.Join(Environment.NewLine, registry.Errors));

            Assert.NotEmpty(registry.SurfaceCombos);
            Assert.Contains(registry.SurfaceCombos, combo =>
                combo.Type == "Surface" &&
                combo.Start == "SurfaceWater" &&
                combo.Result == "SurfaceWaterFrozen" &&
                combo.Cause == "Freeze");
        }
    }
}
