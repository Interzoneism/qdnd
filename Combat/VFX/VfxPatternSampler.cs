using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using QDND.Combat.Services;

namespace QDND.Combat.VFX
{
    public static class VfxPatternSampler
    {
        public static IReadOnlyList<Vector3> Sample(
            VfxRequest request,
            VfxPresetDefinition preset,
            IReadOnlyList<Vector3> targetPositions)
        {
            if (request == null)
                return Array.Empty<Vector3>();

            int sampleCount = Math.Max(1, preset?.SampleCount ?? 1);
            int seed = request.Seed != 0 ? request.Seed : 1337;

            switch (request.Pattern)
            {
                case VfxTargetPattern.PerTarget:
                    if (targetPositions != null && targetPositions.Count > 0)
                        return SamplePerTarget(targetPositions);
                    return request.TargetPosition.HasValue
                        ? SamplePoint(request.TargetPosition.Value)
                        : request.SourcePosition.HasValue
                            ? SamplePoint(request.SourcePosition.Value)
                            : Array.Empty<Vector3>();

                case VfxTargetPattern.Circle:
                    {
                        var center = request.CastPosition
                            ?? request.TargetPosition
                            ?? request.SourcePosition
                            ?? Vector3.Zero;
                        float radius = preset?.Radius > 0f
                            ? preset.Radius
                            : MathF.Max(0.5f, request.Magnitude);
                        return SampleCircle(center, radius, sampleCount, seed);
                    }

                case VfxTargetPattern.Cone:
                    {
                        var origin = request.SourcePosition ?? request.CastPosition ?? Vector3.Zero;
                        var direction = request.Direction ?? DirectionTo(origin, request.TargetPosition ?? (origin + Vector3.UnitZ));
                        float length = request.Magnitude > 0f
                            ? request.Magnitude
                            : Vector3.Distance(origin, request.TargetPosition ?? (origin + direction * 4f));
                        float angle = preset?.ConeAngle > 0f ? preset.ConeAngle : 60f;
                        return SampleCone(origin, direction, length, angle, sampleCount, seed);
                    }

                case VfxTargetPattern.Line:
                    {
                        var start = request.SourcePosition ?? request.CastPosition ?? Vector3.Zero;
                        var end = request.TargetPosition ?? (start + Normalize(request.Direction ?? Vector3.UnitZ) * MathF.Max(1f, request.Magnitude));
                        float width = preset?.LineWidth > 0f ? preset.LineWidth : 0f;
                        return SampleLine(start, end, width, sampleCount, seed);
                    }

                case VfxTargetPattern.Path:
                    {
                        var start = request.SourcePosition ?? request.CastPosition ?? Vector3.Zero;
                        var end = request.TargetPosition ?? (start + Normalize(request.Direction ?? Vector3.UnitZ) * MathF.Max(1f, request.Magnitude));
                        return SamplePath(new[] { start, end }, sampleCount);
                    }

                case VfxTargetPattern.SourceAura:
                    {
                        var center = request.SourcePosition ?? Vector3.Zero;
                        float radius = preset?.Radius > 0f ? preset.Radius : 0.75f;
                        return SampleCircle(center, radius, sampleCount, seed);
                    }

                case VfxTargetPattern.TargetAura:
                    {
                        var center = request.TargetPosition
                            ?? (targetPositions != null && targetPositions.Count > 0 ? targetPositions[0] : request.SourcePosition ?? Vector3.Zero);
                        float radius = preset?.Radius > 0f ? preset.Radius : 0.75f;
                        return SampleCircle(center, radius, sampleCount, seed);
                    }

                case VfxTargetPattern.Point:
                default:
                    {
                        var point = request.TargetPosition
                            ?? request.CastPosition
                            ?? request.SourcePosition
                            ?? Vector3.Zero;
                        return SamplePoint(point);
                    }
            }
        }

        public static IReadOnlyList<Vector3> SamplePoint(Vector3 point)
            => new[] { point };

        public static IReadOnlyList<Vector3> SamplePerTarget(IReadOnlyList<Vector3> targets)
            => targets?.ToArray() ?? Array.Empty<Vector3>();

        public static IReadOnlyList<Vector3> SampleCircle(Vector3 center, float radius, int count, int seed)
        {
            var points = new List<Vector3>(Math.Max(1, count));
            var rng = new Random(seed);
            int sampleCount = Math.Max(1, count);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (i + rng.NextSingle() * 0.5f) / sampleCount;
                float angle = t * MathF.Tau;
                float r = radius * (0.85f + rng.NextSingle() * 0.15f);
                points.Add(center + new Vector3(MathF.Cos(angle) * r, 0f, MathF.Sin(angle) * r));
            }

            return points;
        }

        public static IReadOnlyList<Vector3> SampleCone(Vector3 origin, Vector3 direction, float length, float angleDeg, int count, int seed)
        {
            var points = new List<Vector3>(Math.Max(1, count));
            var rng = new Random(seed);
            int sampleCount = Math.Max(1, count);
            var dir = Normalize(direction);
            float halfAngleRad = MathF.Max(1f, angleDeg) * 0.5f * (MathF.PI / 180f);

            var right = new Vector3(-dir.Z, 0f, dir.X);
            if (right.LengthSquared() < 1e-5f)
                right = Vector3.UnitX;
            right = Normalize(right);

            for (int i = 0; i < sampleCount; i++)
            {
                float dist = (i + 1f) / sampleCount * MathF.Max(0.1f, length);
                float offsetAngle = (rng.NextSingle() * 2f - 1f) * halfAngleRad;
                var rotated = Normalize(dir * MathF.Cos(offsetAngle) + right * MathF.Sin(offsetAngle));
                points.Add(origin + rotated * dist);
            }

            return points;
        }

        public static IReadOnlyList<Vector3> SampleLine(Vector3 start, Vector3 end, float width, int count, int seed)
        {
            var points = new List<Vector3>(Math.Max(1, count));
            var rng = new Random(seed);
            int sampleCount = Math.Max(1, count);
            var dir = end - start;
            var normal = Normalize(new Vector3(-dir.Z, 0f, dir.X));
            if (normal.LengthSquared() < 1e-5f)
                normal = Vector3.UnitX;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = sampleCount == 1 ? 1f : (float)i / (sampleCount - 1);
                var point = Vector3.Lerp(start, end, t);
                if (width > 0f)
                {
                    float offset = (rng.NextSingle() * 2f - 1f) * (width * 0.5f);
                    point += normal * offset;
                }

                points.Add(point);
            }

            return points;
        }

        public static IReadOnlyList<Vector3> SamplePath(IReadOnlyList<Vector3> path, int count)
        {
            if (path == null || path.Count == 0)
                return Array.Empty<Vector3>();

            if (path.Count == 1)
                return new[] { path[0] };

            int sampleCount = Math.Max(2, count);
            float total = 0f;
            var lengths = new float[path.Count - 1];
            for (int i = 1; i < path.Count; i++)
            {
                float len = Vector3.Distance(path[i - 1], path[i]);
                lengths[i - 1] = len;
                total += len;
            }

            if (total <= 1e-5f)
                return path.ToArray();

            var sampled = new List<Vector3>(sampleCount);
            for (int i = 0; i < sampleCount; i++)
            {
                float targetDistance = total * (i / (float)(sampleCount - 1));
                float walked = 0f;
                for (int seg = 0; seg < lengths.Length; seg++)
                {
                    float segLen = lengths[seg];
                    if (walked + segLen >= targetDistance)
                    {
                        float t = segLen <= 1e-5f ? 0f : (targetDistance - walked) / segLen;
                        sampled.Add(Vector3.Lerp(path[seg], path[seg + 1], t));
                        break;
                    }

                    walked += segLen;
                }
            }

            return sampled;
        }

        private static Vector3 DirectionTo(Vector3 from, Vector3 to)
            => Normalize(to - from);

        private static Vector3 Normalize(Vector3 vector)
        {
            if (vector.LengthSquared() <= 1e-8f)
                return Vector3.UnitZ;
            return Vector3.Normalize(vector);
        }
    }
}
