using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;
using QDND.Combat.Services;
using QDND.Combat.VFX;

namespace QDND.Tests.Unit
{
    public class VfxPatternSamplerTests
    {
        [Fact]
        public void CircleSampling_IsDeterministicForSameSeed()
        {
            var request = new VfxRequest("corr", VfxEventPhase.Area)
            {
                Pattern = VfxTargetPattern.Circle,
                CastPosition = new Vector3(3, 0, 3),
                Magnitude = 2.5f,
                Seed = 12345
            };
            var preset = new VfxPresetDefinition { Id = "area", SampleCount = 16, Radius = 2.5f };

            var first = VfxPatternSampler.Sample(request, preset, Array.Empty<Vector3>());
            var second = VfxPatternSampler.Sample(request, preset, Array.Empty<Vector3>());

            Assert.Equal(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.Equal(first[i], second[i]);
            }
        }

        [Fact]
        public void CircleSampling_ChangesWithSeed()
        {
            var preset = new VfxPresetDefinition { Id = "area", SampleCount = 8, Radius = 2f };
            var a = new VfxRequest("corr", VfxEventPhase.Area)
            {
                Pattern = VfxTargetPattern.Circle,
                CastPosition = Vector3.Zero,
                Magnitude = 2f,
                Seed = 11
            };
            var b = new VfxRequest("corr", VfxEventPhase.Area)
            {
                Pattern = VfxTargetPattern.Circle,
                CastPosition = Vector3.Zero,
                Magnitude = 2f,
                Seed = 12
            };

            var pointsA = VfxPatternSampler.Sample(a, preset, Array.Empty<Vector3>());
            var pointsB = VfxPatternSampler.Sample(b, preset, Array.Empty<Vector3>());

            Assert.NotEqual(pointsA[0], pointsB[0]);
        }

        [Fact]
        public void PerTargetSampling_UsesProvidedTargets()
        {
            var targets = new List<Vector3>
            {
                new(1, 0, 1),
                new(2, 0, 2),
                new(3, 0, 3)
            };
            var request = new VfxRequest("corr", VfxEventPhase.Impact)
            {
                Pattern = VfxTargetPattern.PerTarget
            };

            var sampled = VfxPatternSampler.Sample(request, new VfxPresetDefinition { Id = "per", SampleCount = 3 }, targets);

            Assert.Equal(targets, sampled);
        }

        [Fact]
        public void ConeSampling_StaysWithinConfiguredAngleAndRange()
        {
            var request = new VfxRequest("corr", VfxEventPhase.Area)
            {
                Pattern = VfxTargetPattern.Cone,
                SourcePosition = Vector3.Zero,
                Direction = Vector3.UnitZ,
                Magnitude = 8f,
                Seed = 999
            };
            var preset = new VfxPresetDefinition { Id = "cone", SampleCount = 20, ConeAngle = 60f };

            var points = VfxPatternSampler.Sample(request, preset, Array.Empty<Vector3>());
            float maxHalfAngle = 30f;
            foreach (var p in points)
            {
                var dir = Vector3.Normalize(new Vector3(p.X, 0, p.Z));
                float dot = Math.Clamp(Vector3.Dot(dir, Vector3.UnitZ), -1f, 1f);
                float angleDeg = MathF.Acos(dot) * (180f / MathF.PI);
                float distance = p.Length();

                Assert.True(angleDeg <= maxHalfAngle + 0.001f);
                Assert.True(distance <= 8.001f);
            }
        }

        [Fact]
        public void LineSampling_ProducesRequestedCount()
        {
            var request = new VfxRequest("corr", VfxEventPhase.Area)
            {
                Pattern = VfxTargetPattern.Line,
                SourcePosition = Vector3.Zero,
                TargetPosition = new Vector3(10, 0, 0),
                Seed = 456
            };
            var preset = new VfxPresetDefinition { Id = "line", SampleCount = 12, LineWidth = 1f };

            var points = VfxPatternSampler.Sample(request, preset, Array.Empty<Vector3>());

            Assert.Equal(12, points.Count);
            Assert.Equal(0f, points.First().X, 3);
            Assert.Equal(10f, points.Last().X, 3);
        }
    }
}
