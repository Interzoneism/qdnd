using System;
using System.Collections.Generic;
using Godot;

namespace QDND.Combat.Environment
{
    /// <summary>
    /// Discrete 2D cell coordinate on the XZ surface grid.
    /// </summary>
    public readonly struct SurfaceCell : IEquatable<SurfaceCell>
    {
        public int X { get; }
        public int Z { get; }

        public SurfaceCell(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(SurfaceCell other) => X == other.X && Z == other.Z;

        public override bool Equals(object obj)
        {
            return obj is SurfaceCell other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Z);
        }

        public static bool operator ==(SurfaceCell left, SurfaceCell right) => left.Equals(right);

        public static bool operator !=(SurfaceCell left, SurfaceCell right) => !left.Equals(right);

        public override string ToString() => $"({X},{Z})";
    }

    /// <summary>
    /// Active surface instance in the world.
    /// Cell occupancy is authoritative for gameplay and rendering.
    /// </summary>
    public class SurfaceInstance
    {
        private readonly HashSet<SurfaceCell> _cells = new();

        public string InstanceId { get; } = Guid.NewGuid().ToString("N")[..8];
        public SurfaceDefinition Definition { get; }
        public float CellSize { get; }

        /// <summary>
        /// Weighted centroid of occupied cells (for diagnostics, VFX placement, and heuristics).
        /// </summary>
        public Vector3 Position { get; private set; }

        /// <summary>
        /// Approximate enclosing radius in meters from centroid to occupied area.
        /// </summary>
        public float Radius { get; private set; }

        public IReadOnlyCollection<SurfaceCell> Cells => _cells;
        public int CellCount => _cells.Count;

        public string CreatorId { get; set; }
        public int RemainingDuration { get; set; }
        public long CreatedAt { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public bool IsPermanent => Definition.DefaultDuration == 0 || RemainingDuration == 0;
        public bool IsDepleted => _cells.Count == 0;

        /// <summary>
        /// Rounds since last growth step. Reset to 0 after each growth.
        /// </summary>
        public int RoundsSinceLastGrowth { get; set; }

        public SurfaceInstance(SurfaceDefinition definition, float cellSize = 0.5f)
        {
            Definition = definition;
            RemainingDuration = definition.DefaultDuration;
            CellSize = Mathf.Max(0.01f, cellSize);
        }

        /// <summary>
        /// Initialize with a circular area in world space.
        /// </summary>
        public void InitializeGeometry(Vector3 center, float radius)
        {
            SetVerticalPosition(center.Y);
            _cells.Clear();
            AddBlob(center, radius);
        }

        /// <summary>
        /// Adds a circular area to this surface.
        /// Kept for backward compatibility with older call sites.
        /// </summary>
        public void AddBlob(Vector3 center, float radius)
        {
            if (_cells.Count == 0)
            {
                SetVerticalPosition(center.Y);
            }
            AddCells(EnumerateCellsInCircle(center, Mathf.Max(0.01f, radius)));
        }

        /// <summary>
        /// Adds explicit grid cells.
        /// </summary>
        public void AddCells(IEnumerable<SurfaceCell> cells)
        {
            bool changed = false;
            foreach (var cell in cells)
            {
                changed |= _cells.Add(cell);
            }

            if (changed)
            {
                RecalculateBounds();
            }
        }

        /// <summary>
        /// Replaces occupancy with explicit cells.
        /// </summary>
        public void SetCells(IEnumerable<SurfaceCell> cells)
        {
            _cells.Clear();
            foreach (var cell in cells)
            {
                _cells.Add(cell);
            }
            RecalculateBounds();
        }

        public void SetVerticalPosition(float y)
        {
            Position = new Vector3(Position.X, y, Position.Z);
        }

        public void MergeGeometryFrom(SurfaceInstance other)
        {
            if (other == null)
                return;

            AddCells(other._cells);
        }

        public bool SubtractArea(Vector3 center, float radius, float minBlobRadius = 0.35f)
        {
            if (_cells.Count == 0 || radius <= 0.01f)
                return false;

            var toRemove = new List<SurfaceCell>();
            float expandedRadius = radius + GetCellHalfDiagonal();
            float expandedRadiusSq = expandedRadius * expandedRadius;

            foreach (var cell in _cells)
            {
                var world = CellToWorld(cell);
                float dx = world.X - center.X;
                float dz = world.Z - center.Z;
                if (dx * dx + dz * dz <= expandedRadiusSq)
                {
                    toRemove.Add(cell);
                }
            }

            if (toRemove.Count == 0)
                return false;

            foreach (var cell in toRemove)
            {
                _cells.Remove(cell);
            }

            RecalculateBounds();
            return true;
        }

        public bool ContainsPosition(Vector3 pos)
        {
            return _cells.Contains(WorldToCell(pos));
        }

        public bool IntersectsArea(Vector3 center, float radius)
        {
            if (_cells.Count == 0 || radius <= 0f)
                return false;

            float expandedRadius = radius + GetCellHalfDiagonal();
            float expandedRadiusSq = expandedRadius * expandedRadius;
            foreach (var cell in _cells)
            {
                var world = CellToWorld(cell);
                float dx = world.X - center.X;
                float dz = world.Z - center.Z;
                if (dx * dx + dz * dz <= expandedRadiusSq)
                {
                    return true;
                }
            }

            return false;
        }

        public bool Overlaps(SurfaceInstance other)
        {
            if (other == null || _cells.Count == 0 || other._cells.Count == 0)
                return false;

            var smaller = _cells.Count <= other._cells.Count ? _cells : other._cells;
            var larger = smaller == _cells ? other._cells : _cells;
            foreach (var cell in smaller)
            {
                if (larger.Contains(cell))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Expand occupied area by one or more cell rings.
        /// </summary>
        public void GrowBlobs(float radiusIncrease, float maxRadius = 0f)
        {
            if (radiusIncrease <= 0f || _cells.Count == 0)
                return;

            int ringCount = Mathf.Max(1, Mathf.CeilToInt(radiusIncrease / CellSize));
            var cardinalAndDiagonal = new[]
            {
                new SurfaceCell(-1, -1),
                new SurfaceCell(-1, 0),
                new SurfaceCell(-1, 1),
                new SurfaceCell(0, -1),
                new SurfaceCell(0, 1),
                new SurfaceCell(1, -1),
                new SurfaceCell(1, 0),
                new SurfaceCell(1, 1)
            };

            for (int ring = 0; ring < ringCount; ring++)
            {
                if (maxRadius > 0f && Radius >= maxRadius - 0.001f)
                    break;

                var additions = new HashSet<SurfaceCell>();
                Vector3 center = Position;
                foreach (var cell in _cells)
                {
                    foreach (var delta in cardinalAndDiagonal)
                    {
                        var candidate = new SurfaceCell(cell.X + delta.X, cell.Z + delta.Z);
                        if (_cells.Contains(candidate))
                            continue;

                        if (maxRadius > 0f)
                        {
                            var world = CellToWorld(candidate);
                            if (world.DistanceTo(center) > maxRadius)
                                continue;
                        }

                        additions.Add(candidate);
                    }
                }

                if (additions.Count == 0)
                    break;

                foreach (var added in additions)
                {
                    _cells.Add(added);
                }

                RecalculateBounds();
            }
        }

        public bool Tick()
        {
            if (IsPermanent)
                return true;
            RemainingDuration--;
            return RemainingDuration > 0;
        }

        /// <summary>
        /// Converts a world XZ position to a discrete cell index.
        /// </summary>
        public SurfaceCell WorldToCell(Vector3 worldPosition)
        {
            int x = Mathf.FloorToInt(worldPosition.X / CellSize);
            int z = Mathf.FloorToInt(worldPosition.Z / CellSize);
            return new SurfaceCell(x, z);
        }

        /// <summary>
        /// Converts a discrete cell index to world-space center point.
        /// </summary>
        public Vector3 CellToWorld(SurfaceCell cell)
        {
            return new Vector3(
                (cell.X + 0.5f) * CellSize,
                Position.Y,
                (cell.Z + 0.5f) * CellSize);
        }

        /// <summary>
        /// Enumerate cells whose centers are inside a circle.
        /// </summary>
        public IEnumerable<SurfaceCell> EnumerateCellsInCircle(Vector3 center, float radius)
        {
            if (radius <= 0f)
                yield break;

            int minX = Mathf.FloorToInt((center.X - radius) / CellSize);
            int maxX = Mathf.FloorToInt((center.X + radius) / CellSize);
            int minZ = Mathf.FloorToInt((center.Z - radius) / CellSize);
            int maxZ = Mathf.FloorToInt((center.Z + radius) / CellSize);
            float radiusSq = radius * radius;

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    float cx = (x + 0.5f) * CellSize;
                    float cz = (z + 0.5f) * CellSize;
                    float dx = cx - center.X;
                    float dz = cz - center.Z;
                    if (dx * dx + dz * dz <= radiusSq)
                    {
                        yield return new SurfaceCell(x, z);
                    }
                }
            }
        }

        public override string ToString()
        {
            string duration = IsPermanent ? "permanent" : $"{RemainingDuration} rounds";
            return $"[Surface:{Definition.Name}] at {Position}, radius {Radius:0.##}, cells={_cells.Count}, {duration}";
        }

        private void RecalculateBounds()
        {
            if (_cells.Count == 0)
            {
                Position = Vector3.Zero;
                Radius = 0f;
                return;
            }

            float sumX = 0f;
            float sumZ = 0f;
            float y = Position.Y;
            foreach (var cell in _cells)
            {
                sumX += (cell.X + 0.5f) * CellSize;
                sumZ += (cell.Z + 0.5f) * CellSize;
            }

            float invCount = 1f / _cells.Count;
            Position = new Vector3(sumX * invCount, y, sumZ * invCount);

            float maxExtent = 0f;
            float halfDiagonal = GetCellHalfDiagonal();
            foreach (var cell in _cells)
            {
                float cx = (cell.X + 0.5f) * CellSize;
                float cz = (cell.Z + 0.5f) * CellSize;
                float dx = cx - Position.X;
                float dz = cz - Position.Z;
                float extent = Mathf.Sqrt(dx * dx + dz * dz) + halfDiagonal;
                if (extent > maxExtent)
                {
                    maxExtent = extent;
                }
            }

            Radius = Mathf.Max(CellSize * 0.5f, maxExtent);
        }

        private float GetCellHalfDiagonal()
        {
            return CellSize * 0.70710677f;
        }
    }
}
