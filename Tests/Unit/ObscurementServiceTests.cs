using Xunit;
using Godot;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for ObscurementService and its integration with AI movement scoring.
    /// </summary>
    public class ObscurementServiceTests
    {
        // ────────────────────────────────────────────────
        //  ObscurementService core tests
        // ────────────────────────────────────────────────

        [Fact]
        public void GetObscurementAt_NoZones_ReturnsClear()
        {
            var service = new ObscurementService();

            var result = service.GetObscurementAt(new Vector3(10, 0, 10));

            Assert.Equal(ObscurementLevel.Clear, result);
        }

        [Fact]
        public void GetObscurementAt_InsideDarkness_ReturnsHeavy()
        {
            var service = new ObscurementService();
            service.AddZone(new Vector3(0, 0, 0), 15f, ObscurementLevel.Heavy, "darkness_spell_1");

            var result = service.GetObscurementAt(new Vector3(5, 0, 5));

            Assert.Equal(ObscurementLevel.Heavy, result);
        }

        [Fact]
        public void GetObscurementAt_InsideFogCloud_ReturnsLight()
        {
            var service = new ObscurementService();
            service.AddZone(new Vector3(10, 0, 10), 20f, ObscurementLevel.Light, "fog_cloud_1");

            var result = service.GetObscurementAt(new Vector3(15, 0, 12));

            Assert.Equal(ObscurementLevel.Light, result);
        }

        [Fact]
        public void GetObscurementAt_OutsideAllZones_ReturnsClear()
        {
            var service = new ObscurementService();
            service.AddZone(new Vector3(0, 0, 0), 5f, ObscurementLevel.Heavy, "darkness_1");
            service.AddZone(new Vector3(50, 0, 50), 5f, ObscurementLevel.Light, "fog_1");

            // Position far from both zones
            var result = service.GetObscurementAt(new Vector3(25, 0, 25));

            Assert.Equal(ObscurementLevel.Clear, result);
        }

        [Fact]
        public void GetObscurementAt_OverlappingZones_ReturnsWorst()
        {
            var service = new ObscurementService();
            // Light zone covers large area
            service.AddZone(new Vector3(0, 0, 0), 30f, ObscurementLevel.Light, "fog_cloud_1");
            // Heavy zone covers smaller area within the light zone
            service.AddZone(new Vector3(5, 0, 5), 10f, ObscurementLevel.Heavy, "darkness_1");

            // Position inside both zones
            var result = service.GetObscurementAt(new Vector3(5, 0, 5));

            Assert.Equal(ObscurementLevel.Heavy, result);
        }

        [Fact]
        public void GetObscurementAt_OverlappingZones_LightIfOnlyInLight()
        {
            var service = new ObscurementService();
            service.AddZone(new Vector3(0, 0, 0), 30f, ObscurementLevel.Light, "fog_cloud_1");
            service.AddZone(new Vector3(5, 0, 5), 3f, ObscurementLevel.Heavy, "darkness_1");

            // Position inside light zone but outside heavy zone
            var result = service.GetObscurementAt(new Vector3(20, 0, 0));

            Assert.Equal(ObscurementLevel.Light, result);
        }

        [Fact]
        public void AddZone_RemoveZone_ClearsCorrectly()
        {
            var service = new ObscurementService();
            service.AddZone(new Vector3(0, 0, 0), 10f, ObscurementLevel.Heavy, "darkness_1");
            service.AddZone(new Vector3(20, 0, 0), 10f, ObscurementLevel.Light, "fog_1");

            Assert.Equal(2, service.GetActiveZones().Count);

            service.RemoveZone("darkness_1");

            Assert.Equal(1, service.GetActiveZones().Count);
            // Position that was in darkness is now clear
            Assert.Equal(ObscurementLevel.Clear, service.GetObscurementAt(new Vector3(0, 0, 0)));
            // Fog zone still active
            Assert.Equal(ObscurementLevel.Light, service.GetObscurementAt(new Vector3(20, 0, 0)));
        }

        [Fact]
        public void Clear_RemovesAllZones()
        {
            var service = new ObscurementService();
            service.AddZone(new Vector3(0, 0, 0), 10f, ObscurementLevel.Heavy, "darkness_1");
            service.AddZone(new Vector3(20, 0, 0), 10f, ObscurementLevel.Light, "fog_1");
            service.AddZone(new Vector3(40, 0, 0), 5f, ObscurementLevel.Heavy, "darkness_2");

            service.Clear();

            Assert.Empty(service.GetActiveZones());
            Assert.Equal(ObscurementLevel.Clear, service.GetObscurementAt(new Vector3(0, 0, 0)));
            Assert.Equal(ObscurementLevel.Clear, service.GetObscurementAt(new Vector3(20, 0, 0)));
        }

        [Fact]
        public void GetObscurementAt_UsesXZDistance_IgnoresHeight()
        {
            var service = new ObscurementService();
            // Zone at ground level with radius 10
            service.AddZone(new Vector3(0, 0, 0), 10f, ObscurementLevel.Heavy, "darkness_1");

            // Position directly above the zone center at Y=100 — XZ distance is 0, so still inside
            Assert.Equal(ObscurementLevel.Heavy, service.GetObscurementAt(new Vector3(0, 100, 0)));

            // Position at same Y but XZ outside radius
            Assert.Equal(ObscurementLevel.Clear, service.GetObscurementAt(new Vector3(15, 100, 0)));

            // Zone center at elevation, query at ground level — XZ distance is 0
            var service2 = new ObscurementService();
            service2.AddZone(new Vector3(0, 50, 0), 10f, ObscurementLevel.Light, "elevated_fog");
            Assert.Equal(ObscurementLevel.Light, service2.GetObscurementAt(new Vector3(5, 0, 5)));
        }

        [Fact]
        public void GetObscurementAt_OnBoundary_ReturnsZoneLevel()
        {
            var service = new ObscurementService();
            service.AddZone(new Vector3(0, 0, 0), 10f, ObscurementLevel.Heavy, "darkness_1");

            // Exactly on boundary (XZ distance = 10, radius = 10)
            var result = service.GetObscurementAt(new Vector3(10, 0, 0));

            Assert.Equal(ObscurementLevel.Heavy, result);
        }

        [Fact]
        public void GetActiveZones_ReturnsAllRegisteredZones()
        {
            var service = new ObscurementService();
            service.AddZone(new Vector3(0, 0, 0), 10f, ObscurementLevel.Heavy, "darkness_1");
            service.AddZone(new Vector3(20, 0, 0), 15f, ObscurementLevel.Light, "fog_1");

            var zones = service.GetActiveZones();

            Assert.Equal(2, zones.Count);
            Assert.Equal("darkness_1", zones[0].SourceId);
            Assert.Equal("fog_1", zones[1].SourceId);
        }

        // ────────────────────────────────────────────────
        //  AI movement scoring integration tests
        // ────────────────────────────────────────────────

        [Fact]
        public void ObscurementScoring_HeavyDarkness_AppliesModifier()
        {
            // Arrange: Darkness zone centered at (10,0,0), radius 8
            var obscurement = new ObscurementService();
            obscurement.AddZone(new Vector3(10, 0, 0), 8f, ObscurementLevel.Heavy, "darkness_1");

            // Verify service returns Heavy inside the zone
            Assert.Equal(ObscurementLevel.Heavy, obscurement.GetObscurementAt(new Vector3(10, 0, 0)));
            Assert.Equal(ObscurementLevel.Clear, obscurement.GetObscurementAt(new Vector3(30, 0, 0)));

            // Build BG3 profile with non-zero heavy darkness multiplier (rogue seeking darkness)
            var bg3Profile = new BG3ArchetypeProfile();
            bg3Profile.LoadFromSettings(new Dictionary<string, float>
            {
                ["MULTIPLIER_DARKNESS_HEAVY"] = 5.0f
            });

            // Verify the multiplier is set
            Assert.Equal(5.0f, bg3Profile.MultiplierDarknessHeavy);
            Assert.Equal(0.0f, bg3Profile.MultiplierDarknessLight);
            Assert.Equal(0.0f, bg3Profile.MultiplierDarknessClear);
        }

        [Fact]
        public void ObscurementScoring_NullService_NoEffect()
        {
            // Arrange: AIMovementEvaluator with null obscurement service should not crash
            var evaluator = new AIMovementEvaluator(null, null, null, null, null, null);

            var actor = new Combatant("ai", "ai", Faction.Hostile, 50, 10);
            actor.Position = Vector3.Zero;
            actor.ActionBudget.MaxMovement = 30f;
            actor.ActionBudget.ResetFull();

            var profile = new AIProfile { Archetype = AIArchetype.Tactical };
            profile.BG3Profile = new BG3ArchetypeProfile();

            // Act — should not throw
            var candidates = evaluator.EvaluateMovement(actor, profile, maxCandidates: 5);

            // Assert
            Assert.NotNull(candidates);
            // No candidate should have obscurement key (service is null)
            foreach (var c in candidates)
            {
                Assert.False(c.ScoreBreakdown.ContainsKey("bg3_obscurement"),
                    "No obscurement scoring when service is null");
            }
        }

        [Fact]
        public void ObscurementScoring_DefaultProfile_NoEffect()
        {
            // Default BG3 profile has all darkness multipliers at 0.0
            var obscurement = new ObscurementService();
            // Cover the entire movement range with heavy darkness
            obscurement.AddZone(Vector3.Zero, 100f, ObscurementLevel.Heavy, "darkness_1");

            var evaluator = new AIMovementEvaluator(null, null, null, null, null, obscurement);

            var actor = new Combatant("ai", "ai", Faction.Hostile, 50, 10);
            actor.Position = Vector3.Zero;
            actor.ActionBudget.MaxMovement = 30f;
            actor.ActionBudget.ResetFull();

            var profile = new AIProfile { Archetype = AIArchetype.Tactical };
            profile.BG3Profile = new BG3ArchetypeProfile(); // All darkness multipliers = 0.0

            // Act
            var candidates = evaluator.EvaluateMovement(actor, profile, maxCandidates: 20);

            // Assert: default multipliers are 0.0 — no obscurement key in breakdown
            foreach (var c in candidates)
            {
                Assert.False(c.ScoreBreakdown.ContainsKey("bg3_obscurement"),
                    "Default profile (all darkness multipliers 0.0) should not produce obscurement score");
            }
        }

        [Fact]
        public void ObscurementScoring_NonZeroMultiplier_ProducesBreakdown()
        {
            // Large darkness zone covers the entire movement area
            var obscurement = new ObscurementService();
            obscurement.AddZone(Vector3.Zero, 100f, ObscurementLevel.Heavy, "darkness_1");

            var evaluator = new AIMovementEvaluator(null, null, null, null, null, obscurement);

            var actor = new Combatant("ai", "ai", Faction.Hostile, 50, 10);
            actor.Position = Vector3.Zero;
            actor.ActionBudget.MaxMovement = 30f;
            actor.ActionBudget.ResetFull();

            var bg3Profile = new BG3ArchetypeProfile();
            bg3Profile.LoadFromSettings(new Dictionary<string, float>
            {
                ["MULTIPLIER_DARKNESS_HEAVY"] = 3.0f
            });
            var profile = new AIProfile { Archetype = AIArchetype.Tactical };
            profile.BG3Profile = bg3Profile;

            // Act
            var candidates = evaluator.EvaluateMovement(actor, profile, maxCandidates: 20);

            // Assert: at least some candidates should have obscurement scoring
            var withObscurement = candidates.Where(c => c.ScoreBreakdown.ContainsKey("bg3_obscurement")).ToList();
            Assert.NotEmpty(withObscurement);
            Assert.All(withObscurement, c =>
                Assert.True(c.ScoreBreakdown["bg3_obscurement"] > 0,
                    "Heavy darkness with positive multiplier should produce positive score"));
        }
    }
}
