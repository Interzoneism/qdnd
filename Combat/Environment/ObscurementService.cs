using System;
using System.Collections.Generic;
using Godot;

namespace QDND.Combat.Environment
{
    /// <summary>
    /// Level of visual obscurement at a position.
    /// Maps to D&D 5e/BG3 obscurement categories.
    /// </summary>
    public enum ObscurementLevel
    {
        /// <summary>No obscurement — full visibility.</summary>
        Clear = 0,

        /// <summary>Lightly obscured (dim light, patchy fog) — disadvantage on Perception checks.</summary>
        Light = 1,

        /// <summary>Heavily obscured (darkness, dense fog, Fog Cloud) — effectively blinded.</summary>
        Heavy = 2
    }

    /// <summary>
    /// Lightweight environment service that tracks darkness/fog zones and exposes
    /// queries for AI movement and action scoring.
    /// Combat-scoped: created at combat start, cleared at combat end.
    /// </summary>
    public class ObscurementService
    {
        /// <summary>
        /// A spatial zone of obscurement created by a spell or environmental effect.
        /// </summary>
        public readonly struct ObscurementZone
        {
            public readonly Vector3 Center;
            public readonly float Radius;
            public readonly ObscurementLevel Level;
            public readonly string SourceId;

            public ObscurementZone(Vector3 center, float radius, ObscurementLevel level, string sourceId)
            {
                Center = center;
                Radius = radius;
                Level = level;
                SourceId = sourceId;
            }
        }

        private readonly List<ObscurementZone> _zones = new();

        /// <summary>
        /// Register a new obscurement zone.
        /// TODO: Called by spell effects (Darkness, Fog Cloud, etc.) when they are cast.
        /// </summary>
        public void AddZone(Vector3 center, float radius, ObscurementLevel level, string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId))
                throw new ArgumentException("SourceId must not be null or empty.", nameof(sourceId));
            if (radius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be positive.");

            _zones.Add(new ObscurementZone(center, radius, level, sourceId));
        }

        /// <summary>
        /// Remove a zone by source identifier.
        /// TODO: Called when the spell ends or concentration drops.
        /// </summary>
        public void RemoveZone(string sourceId)
        {
            _zones.RemoveAll(z => z.SourceId == sourceId);
        }

        /// <summary>
        /// Query the worst (highest) obscurement level at a world position.
        /// Uses 2D XZ distance — vertical position is irrelevant for fog/darkness.
        /// Returns <see cref="ObscurementLevel.Clear"/> if no zones cover the position.
        /// </summary>
        public ObscurementLevel GetObscurementAt(Vector3 position)
        {
            var worst = ObscurementLevel.Clear;

            for (int i = 0; i < _zones.Count; i++)
            {
                var zone = _zones[i];

                // 2D XZ distance (height doesn't matter for fog/darkness)
                float dx = position.X - zone.Center.X;
                float dz = position.Z - zone.Center.Z;
                float distSq = dx * dx + dz * dz;

                if (distSq <= zone.Radius * zone.Radius && zone.Level > worst)
                {
                    worst = zone.Level;
                    if (worst == ObscurementLevel.Heavy)
                        return worst; // Can't get worse — early out
                }
            }

            return worst;
        }

        /// <summary>
        /// Get all active zones (for debugging/display).
        /// </summary>
        public IReadOnlyList<ObscurementZone> GetActiveZones() => _zones;

        /// <summary>
        /// Clear all zones. Called at combat end.
        /// </summary>
        public void Clear()
        {
            _zones.Clear();
        }
    }
}
