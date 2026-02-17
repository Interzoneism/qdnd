using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Xunit;
using Xunit.Abstractions;
using QDND.Combat.Environment;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Tests for the complete surface interaction matrix and cloud surface types.
    /// Uses the real SurfaceManager to verify BG3-accurate surface interactions.
    /// </summary>
    public class SurfaceInteractionMatrixTests
    {
        private readonly ITestOutputHelper _output;

        public SurfaceInteractionMatrixTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // =================================================================
        //  Helper: Create manager and track transformations
        // =================================================================

        private (SurfaceManager manager, List<(string oldId, string newId)> transforms) CreateTrackedManager()
        {
            var manager = new SurfaceManager();
            var transforms = new List<(string oldId, string newId)>();
            manager.OnSurfaceTransformed += (old, @new) =>
            {
                transforms.Add((old.Definition.Id, @new.Definition.Id));
            };
            return (manager, transforms);
        }

        // =================================================================
        //  Fire Interactions
        // =================================================================

        [Fact]
        public void Fire_Plus_Water_Creates_Steam()
        {
            var (mgr, txs) = CreateTrackedManager();
            mgr.CreateSurface("fire", new Vector3(0, 0, 0), 5f);
            mgr.CreateSurface("water", new Vector3(0, 0, 0), 5f);

            Assert.Single(txs);
            Assert.Equal("fire", txs[0].oldId);
            Assert.Equal("steam", txs[0].newId);
        }

        [Fact]
        public void Water_Plus_Fire_Creates_Steam()
        {
            var (mgr, txs) = CreateTrackedManager();
            mgr.CreateSurface("water", new Vector3(0, 0, 0), 5f);
            mgr.CreateSurface("fire", new Vector3(0, 0, 0), 5f);

            Assert.Single(txs);
            Assert.Equal("steam", txs[0].newId);
        }

        [Fact]
        public void Fire_Plus_Oil_Creates_Fire()
        {
            var (mgr, txs) = CreateTrackedManager();
            mgr.CreateSurface("oil", new Vector3(0, 0, 0), 5f);
            mgr.CreateSurface("fire", new Vector3(0, 0, 0), 5f);

            Assert.Single(txs);
            Assert.Equal("oil", txs[0].oldId);
            Assert.Equal("fire", txs[0].newId);
        }

        [Fact]
        public void Fire_Plus_Grease_Creates_Fire()
        {
            var (mgr, txs) = CreateTrackedManager();
            mgr.CreateSurface("grease", new Vector3(0, 0, 0), 5f);
            mgr.CreateSurface("fire", new Vector3(0, 0, 0), 5f);

            Assert.Single(txs);
            Assert.Equal("grease", txs[0].oldId);
            Assert.Equal("fire", txs[0].newId);
        }

        [Fact]
        public void Fire_Plus_Web_Creates_Fire()
        {
            var (mgr, txs) = CreateTrackedManager();
            mgr.CreateSurface("web", new Vector3(0, 0, 0), 5f);
            mgr.CreateSurface("fire", new Vector3(0, 0, 0), 5f);

            Assert.Single(txs);
            Assert.Equal("web", txs[0].oldId);
            Assert.Equal("fire", txs[0].newId);
        }

        [Fact]
        public void Fire_Plus_Ice_Creates_Water()
        {
            var (mgr, txs) = CreateTrackedManager();
            mgr.CreateSurface("ice", new Vector3(0, 0, 0), 5f);
            mgr.CreateSurface("fire", new Vector3(1, 0, 0), 5f);

            Assert.Single(txs);
            Assert.Equal("ice", txs[0].oldId);
            Assert.Equal("water", txs[0].newId);
        }

        [Fact]
        public void Fire_Plus_Poison_Ignites_Poison()
        {
            var (mgr, txs) = CreateTrackedManager();
            mgr.CreateSurface("poison", new Vector3(0, 0, 0), 5f);
            mgr.CreateSurface("fire", new Vector3(0, 0, 0), 5f);

            Assert.Single(txs);
            Assert.Equal("poison", txs[0].oldId);
            Assert.Equal("fire", txs[0].newId);
        }

        // =================================================================
        //  Water / Ice Interactions
        // =================================================================

        [Fact]
        public void Water_Plus_Lightning_Creates_ElectrifiedWater()
        {
            var (mgr, txs) = CreateTrackedManager();
            mgr.CreateSurface("water", new Vector3(0, 0, 0), 5f);
            mgr.CreateSurface("lightning", new Vector3(0, 0, 0), 5f);

            Assert.Single(txs);
            Assert.Equal("water", txs[0].oldId);
            Assert.Equal("electrified_water", txs[0].newId);
        }

        [Fact]
        public void Lightning_Plus_Water_Creates_ElectrifiedWater()
        {
            var (mgr, txs) = CreateTrackedManager();
            mgr.CreateSurface("lightning", new Vector3(0, 0, 0), 5f);
            mgr.CreateSurface("water", new Vector3(0, 0, 0), 5f);

            // Lightning's interaction dict maps water → electrified_water
            Assert.Single(txs);
            Assert.Equal("electrified_water", txs[0].newId);
        }

        [Fact]
        public void Water_Plus_Ice_Freezes_Water()
        {
            var (mgr, txs) = CreateTrackedManager();
            mgr.CreateSurface("water", new Vector3(0, 0, 0), 5f);
            mgr.CreateSurface("ice", new Vector3(0, 0, 0), 5f);

            Assert.Single(txs);
            Assert.Equal("water", txs[0].oldId);
            Assert.Equal("ice", txs[0].newId);
        }

        // =================================================================
        //  Acid Interactions
        // =================================================================

        [Fact]
        public void Acid_Plus_Water_Dilutes_Acid()
        {
            var (mgr, txs) = CreateTrackedManager();
            mgr.CreateSurface("acid", new Vector3(0, 0, 0), 5f);
            mgr.CreateSurface("water", new Vector3(0, 0, 0), 5f);

            Assert.Single(txs);
            Assert.Equal("acid", txs[0].oldId);
            Assert.Equal("water", txs[0].newId);
        }

        // =================================================================
        //  Steam / Cloud Interactions
        // =================================================================

        [Fact]
        public void Steam_Plus_Lightning_Creates_ElectrifiedSteam()
        {
            var (mgr, txs) = CreateTrackedManager();
            mgr.CreateSurface("steam", new Vector3(0, 0, 0), 5f);
            mgr.CreateSurface("lightning", new Vector3(0, 0, 0), 5f);

            Assert.Single(txs);
            Assert.Equal("steam", txs[0].oldId);
            Assert.Equal("electrified_steam", txs[0].newId);
        }

        // =================================================================
        //  No-Interaction Cases (surfaces should NOT interact)
        // =================================================================

        [Fact]
        public void NonOverlapping_Surfaces_DontInteract()
        {
            var (mgr, txs) = CreateTrackedManager();
            mgr.CreateSurface("fire", new Vector3(0, 0, 0), 2f);
            mgr.CreateSurface("water", new Vector3(20, 0, 0), 2f);

            Assert.Empty(txs);
        }

        [Fact]
        public void Darkness_Plus_Fire_NoInteraction()
        {
            var (mgr, txs) = CreateTrackedManager();
            mgr.CreateSurface("darkness", new Vector3(0, 0, 0), 5f);
            mgr.CreateSurface("fire", new Vector3(0, 0, 0), 5f);

            Assert.Empty(txs);
        }

        [Fact]
        public void Silence_Plus_Water_NoInteraction()
        {
            var (mgr, txs) = CreateTrackedManager();
            mgr.CreateSurface("silence", new Vector3(0, 0, 0), 5f);
            mgr.CreateSurface("water", new Vector3(0, 0, 0), 5f);

            Assert.Empty(txs);
        }

        // =================================================================
        //  Cloud Surface Type Registration
        // =================================================================

        [Theory]
        [InlineData("fog", "Fog Cloud")]
        [InlineData("stinking_cloud", "Stinking Cloud")]
        [InlineData("cloudkill", "Cloudkill")]
        [InlineData("electrified_steam", "Electrified Steam")]
        public void CloudSurface_IsRegistered(string surfaceId, string expectedName)
        {
            var mgr = new SurfaceManager();
            var surface = mgr.CreateSurface(surfaceId, new Vector3(0, 0, 0), 5f);

            Assert.NotNull(surface);
            Assert.Equal(expectedName, surface.Definition.Name);
        }

        [Theory]
        [InlineData("fog")]
        [InlineData("stinking_cloud")]
        [InlineData("cloudkill")]
        [InlineData("steam")]
        [InlineData("darkness")]
        [InlineData("hunger_of_hadar")]
        [InlineData("electrified_steam")]
        public void ObscuringSurface_HasObscureTag(string surfaceId)
        {
            var mgr = new SurfaceManager();
            var surface = mgr.CreateSurface(surfaceId, new Vector3(0, 0, 0), 5f);

            Assert.NotNull(surface);
            Assert.Contains("obscure", surface.Definition.Tags);
        }

        [Fact]
        public void StinkingCloud_AppliesNauseousStatus()
        {
            var mgr = new SurfaceManager();
            var surface = mgr.CreateSurface("stinking_cloud", new Vector3(0, 0, 0), 5f);

            Assert.NotNull(surface);
            Assert.Equal("nauseous", surface.Definition.AppliesStatusId);
        }

        [Fact]
        public void Cloudkill_DealsPoisonDamage()
        {
            var mgr = new SurfaceManager();
            var surface = mgr.CreateSurface("cloudkill", new Vector3(0, 0, 0), 5f);

            Assert.NotNull(surface);
            Assert.Equal(5, surface.Definition.DamagePerTrigger);
            Assert.Equal("poison", surface.Definition.DamageType);
        }

        [Fact]
        public void ElectrifiedSteam_DealsLightningDamage()
        {
            var mgr = new SurfaceManager();
            var surface = mgr.CreateSurface("electrified_steam", new Vector3(0, 0, 0), 5f);

            Assert.NotNull(surface);
            Assert.Equal(4, surface.Definition.DamagePerTrigger);
            Assert.Equal("lightning", surface.Definition.DamageType);
        }

        // =================================================================
        //  Surface Property Verification
        // =================================================================

        [Theory]
        [InlineData("fire", 5, "fire")]
        [InlineData("poison", 3, "poison")]
        [InlineData("acid", 3, "acid")]
        [InlineData("lightning", 4, "lightning")]
        [InlineData("electrified_water", 4, "lightning")]
        [InlineData("electrified_steam", 4, "lightning")]
        [InlineData("cloudkill", 5, "poison")]
        public void DamageSurface_HasCorrectDamageValues(string surfaceId, float expectedDamage, string expectedType)
        {
            var mgr = new SurfaceManager();
            var surface = mgr.CreateSurface(surfaceId, new Vector3(0, 0, 0), 5f);

            Assert.NotNull(surface);
            Assert.Equal(expectedDamage, surface.Definition.DamagePerTrigger);
            Assert.Equal(expectedType, surface.Definition.DamageType);
        }

        [Theory]
        [InlineData("oil", 1.5f)]
        [InlineData("ice", 2f)]
        [InlineData("grease", 2f)]
        [InlineData("web", 2f)]
        [InlineData("spike_growth", 2f)]
        public void DifficultSurface_HasMovementCostMultiplier(string surfaceId, float expectedMultiplier)
        {
            var mgr = new SurfaceManager();
            var surface = mgr.CreateSurface(surfaceId, new Vector3(0, 0, 0), 5f);

            Assert.NotNull(surface);
            Assert.Equal(expectedMultiplier, surface.Definition.MovementCostMultiplier);
        }

        // =================================================================
        //  Full Interaction Matrix Summary
        // =================================================================

        [Fact]
        public void InteractionMatrix_CoverageSummary()
        {
            var mgr = new SurfaceManager();
            var allSurfaceIds = new[]
            {
                "fire", "water", "poison", "oil", "ice", "steam", "lightning",
                "electrified_water", "acid", "grease", "web", "darkness",
                "moonbeam", "silence", "hunger_of_hadar", "spike_growth",
                "daggers", "fog", "stinking_cloud", "cloudkill", "electrified_steam"
            };

            _output.WriteLine("=== Surface Interaction Matrix ===");
            int totalInteractions = 0;

            foreach (var id in allSurfaceIds)
            {
                var surface = mgr.CreateSurface(id, new Vector3(50 + totalInteractions * 100, 0, 0), 2f); // far apart
                if (surface == null) continue;

                if (surface.Definition.Interactions?.Count > 0)
                {
                    foreach (var (target, result) in surface.Definition.Interactions)
                    {
                        _output.WriteLine($"  {id} + {target} → {result}");
                        totalInteractions++;
                    }
                }
            }

            _output.WriteLine($"\nTotal registered surfaces: {allSurfaceIds.Length}");
            _output.WriteLine($"Total interaction rules: {totalInteractions}");
            _output.WriteLine($"Surfaces with damage: {allSurfaceIds.Count(id => { var s = mgr.CreateSurface(id, new Vector3(500, 500, 500), 1f); return s?.Definition.DamagePerTrigger > 0; })}");
            _output.WriteLine($"Surfaces with status: {allSurfaceIds.Count(id => { var s = mgr.CreateSurface(id, new Vector3(600, 600, 600), 1f); return !string.IsNullOrEmpty(s?.Definition.AppliesStatusId); })}");

            // We should have at least 12 distinct interaction rules
            Assert.True(totalInteractions >= 12, 
                $"Expected at least 12 surface interaction rules, got {totalInteractions}");
        }

        // =================================================================
        //  LOSService Surface Obscuration
        // =================================================================

        [Fact]
        public void LOSService_FogSurface_SetsObscured()
        {
            var mgr = new SurfaceManager();
            var los = new LOSService();
            los.SetSurfaceManager(mgr);

            // Create fog between the two check points
            mgr.CreateSurface("fog", new Vector3(5, 0, 5), 5f);

            // Check LOS through the fog
            var result = los.CheckLOS(
                new Vector3(0, 0, 0),
                new Vector3(10, 0, 10)
            );

            Assert.True(result.IsObscured, "Line through fog surface should be obscured");
        }

        [Fact]
        public void LOSService_DarknessSurface_SetsObscured()
        {
            var mgr = new SurfaceManager();
            var los = new LOSService();
            los.SetSurfaceManager(mgr);

            mgr.CreateSurface("darkness", new Vector3(5, 0, 5), 5f);

            var result = los.CheckLOS(
                new Vector3(0, 0, 0),
                new Vector3(10, 0, 10)
            );

            Assert.True(result.IsObscured);
        }

        [Fact]
        public void LOSService_NoObscuringSurface_NotObscured()
        {
            var mgr = new SurfaceManager();
            var los = new LOSService();
            los.SetSurfaceManager(mgr);

            // Fire surface is not obscuring
            mgr.CreateSurface("fire", new Vector3(5, 0, 5), 5f);

            var result = los.CheckLOS(
                new Vector3(0, 0, 0),
                new Vector3(10, 0, 10)
            );

            Assert.False(result.IsObscured);
        }

        [Fact]
        public void LOSService_ObscuringSurfaceNotOnLine_NotObscured()
        {
            var mgr = new SurfaceManager();
            var los = new LOSService();
            los.SetSurfaceManager(mgr);

            // Fog surface far from the line
            mgr.CreateSurface("fog", new Vector3(50, 0, 50), 3f);

            var result = los.CheckLOS(
                new Vector3(0, 0, 0),
                new Vector3(10, 0, 0)
            );

            Assert.False(result.IsObscured);
        }
    }
}
