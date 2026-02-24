using System;
using System.Collections.Generic;
using Godot;

namespace QDND.Combat.Environment
{
    public sealed class SurfaceBlob
    {
        public Vector3 Center { get; set; }
        public float Radius { get; set; }
    }

    /// <summary>
    /// Active surface instance in the world.
    /// </summary>
    public class SurfaceInstance
    {
        private readonly List<SurfaceBlob> _blobs = new();

        public string InstanceId { get; } = Guid.NewGuid().ToString("N")[..8];
        public SurfaceDefinition Definition { get; }
        public Vector3 Position { get; private set; }
        public float Radius { get; private set; }
        public IReadOnlyList<SurfaceBlob> Blobs => _blobs;
        public string CreatorId { get; set; }
        public int RemainingDuration { get; set; }
        public long CreatedAt { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public bool IsPermanent => Definition.DefaultDuration == 0 || RemainingDuration == 0;
        public bool IsDepleted => _blobs.Count == 0;

        public SurfaceInstance(SurfaceDefinition definition)
        {
            Definition = definition;
            RemainingDuration = definition.DefaultDuration;
        }

        public void InitializeGeometry(Vector3 center, float radius)
        {
            _blobs.Clear();
            AddBlob(center, radius);
        }

        public void AddBlob(Vector3 center, float radius)
        {
            _blobs.Add(new SurfaceBlob
            {
                Center = center,
                Radius = Mathf.Max(0.1f, radius)
            });
            RecalculateBounds();
        }

        public void MergeGeometryFrom(SurfaceInstance other)
        {
            if (other == null)
                return;

            foreach (var blob in other._blobs)
            {
                _blobs.Add(new SurfaceBlob
                {
                    Center = blob.Center,
                    Radius = blob.Radius
                });
            }
            RecalculateBounds();
        }

        public bool SubtractArea(Vector3 center, float radius, float minBlobRadius = 0.35f)
        {
            if (_blobs.Count == 0 || radius <= 0.01f)
                return false;

            bool changed = false;
            minBlobRadius = Mathf.Max(0.1f, minBlobRadius);

            for (int i = _blobs.Count - 1; i >= 0; i--)
            {
                var blob = _blobs[i];
                float distance = blob.Center.DistanceTo(center);
                float overlap = blob.Radius + radius - distance;
                if (overlap <= 0f)
                    continue;

                changed = true;
                if (radius >= distance + blob.Radius - 0.05f)
                {
                    _blobs.RemoveAt(i);
                    continue;
                }

                float shrink = overlap * 0.55f;
                float newRadius = blob.Radius - shrink;
                if (newRadius < minBlobRadius)
                {
                    _blobs.RemoveAt(i);
                    continue;
                }

                Vector3 pushDir = (blob.Center - center).Normalized();
                if (pushDir.LengthSquared() < 0.0001f)
                    pushDir = Vector3.Right;

                blob.Radius = newRadius;
                blob.Center += pushDir * (shrink * 0.2f);
            }

            if (changed)
                RecalculateBounds();
            return changed;
        }

        public bool ContainsPosition(Vector3 pos)
        {
            foreach (var blob in _blobs)
            {
                if (blob.Center.DistanceTo(pos) <= blob.Radius)
                    return true;
            }
            return false;
        }

        public bool IntersectsArea(Vector3 center, float radius)
        {
            foreach (var blob in _blobs)
            {
                if (blob.Center.DistanceTo(center) <= blob.Radius + radius)
                    return true;
            }
            return false;
        }

        public bool Overlaps(SurfaceInstance other)
        {
            if (other == null)
                return false;

            foreach (var a in _blobs)
            {
                foreach (var b in other._blobs)
                {
                    if (a.Center.DistanceTo(b.Center) <= a.Radius + b.Radius)
                        return true;
                }
            }
            return false;
        }

        public bool Tick()
        {
            if (IsPermanent)
                return true;
            RemainingDuration--;
            return RemainingDuration > 0;
        }

        public override string ToString()
        {
            string duration = IsPermanent ? "permanent" : $"{RemainingDuration} rounds";
            return $"[Surface:{Definition.Name}] at {Position}, radius {Radius}, blobs={_blobs.Count}, {duration}";
        }

        private void RecalculateBounds()
        {
            if (_blobs.Count == 0)
            {
                Position = Vector3.Zero;
                Radius = 0f;
                return;
            }

            float totalWeight = 0f;
            Vector3 weightedCenter = Vector3.Zero;
            foreach (var blob in _blobs)
            {
                float weight = Mathf.Pi * blob.Radius * blob.Radius;
                weightedCenter += blob.Center * weight;
                totalWeight += weight;
            }
            Position = totalWeight > 0.001f ? (weightedCenter / totalWeight) : _blobs[0].Center;

            float maxExtent = 0f;
            foreach (var blob in _blobs)
            {
                float extent = Position.DistanceTo(blob.Center) + blob.Radius;
                if (extent > maxExtent)
                    maxExtent = extent;
            }
            Radius = Mathf.Max(0.1f, maxExtent);
        }
    }
}
