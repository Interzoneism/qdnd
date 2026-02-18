using System;
using System.Collections.Generic;
using Godot;

namespace QDND.Combat.Movement
{
    /// <summary>
    /// Result of planning a jump trajectory through 3D space.
    /// </summary>
    public sealed class JumpPathResult
    {
        public bool Success { get; set; }
        public string FailureReason { get; set; }
        public float TotalLength { get; set; }
        public List<Vector3> Waypoints { get; } = new();
    }

    /// <summary>
    /// 3D jump pathfinder that plans collision-free jump trajectories.
    /// </summary>
    public sealed class JumpPathfinder3D
    {
        private readonly struct NeighborOffset
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Z;
            public readonly float DistanceScale;

            public NeighborOffset(int x, int y, int z, float distanceScale)
            {
                X = x;
                Y = y;
                Z = z;
                DistanceScale = distanceScale;
            }
        }

        private sealed class NodeRecord
        {
            public Vector3I Cell;
            public float GScore;
            public float FScore;
        }

        private static readonly NeighborOffset[] NeighborOffsets = BuildNeighborOffsets();

        public float NodeSpacing { get; set; } = 0.9f;
        public float CollisionProbeRadius { get; set; } = 0.35f;
        public float SampleSpacing { get; set; } = 0.3f;
        public float MinimumArcHeight { get; set; } = 2.0f;
        public float HorizontalPadding { get; set; } = 3.0f;
        public float VerticalPadding { get; set; } = 4.0f;
        public int MaxExpandedNodes { get; set; } = 22000;
        public int ArcHeightRetries { get; set; } = 5;
        public float ArcHeightStep { get; set; } = 0.8f;

        public JumpPathResult FindPath(Vector3 start, Vector3 goal, Func<Vector3, float, bool> isBlocked)
        {
            var result = new JumpPathResult();
            if (isBlocked == null)
            {
                result.FailureReason = "No collision probe available";
                return result;
            }

            float spacing = Mathf.Max(0.4f, NodeSpacing);
            float directDistance = start.DistanceTo(goal);
            if (directDistance < 0.05f)
            {
                result.Success = true;
                result.Waypoints.Add(start);
                result.Waypoints.Add(goal);
                result.TotalLength = 0f;
                return result;
            }

            // Fast path: try direct arc first.
            if (TryBuildArcFromBasePath(new List<Vector3> { start, goal }, isBlocked, out var directArc, out float directLength))
            {
                result.Success = true;
                result.Waypoints.AddRange(directArc);
                result.TotalLength = directLength;
                return result;
            }

            float horizontalDistance = new Vector2(goal.X - start.X, goal.Z - start.Z).Length();
            float horizontalPadding = HorizontalPadding + (horizontalDistance * 0.15f);
            float verticalPadding = Mathf.Max(
                VerticalPadding,
                Mathf.Max(MinimumArcHeight + 1.0f, horizontalDistance * 0.35f));

            float minX = Mathf.Min(start.X, goal.X) - horizontalPadding;
            float maxX = Mathf.Max(start.X, goal.X) + horizontalPadding;
            float minY = Mathf.Min(start.Y, goal.Y) - 1.5f;
            float maxY = Mathf.Max(start.Y, goal.Y) + verticalPadding;
            float minZ = Mathf.Min(start.Z, goal.Z) - horizontalPadding;
            float maxZ = Mathf.Max(start.Z, goal.Z) + horizontalPadding;

            int maxCellX = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / spacing));
            int maxCellY = Mathf.Max(1, Mathf.CeilToInt((maxY - minY) / spacing));
            int maxCellZ = Mathf.Max(1, Mathf.CeilToInt((maxZ - minZ) / spacing));

            bool InBounds(Vector3I cell) =>
                cell.X >= 0 && cell.X <= maxCellX &&
                cell.Y >= 0 && cell.Y <= maxCellY &&
                cell.Z >= 0 && cell.Z <= maxCellZ;

            Vector3I WorldToCell(Vector3 world)
            {
                return new Vector3I(
                    Mathf.RoundToInt((world.X - minX) / spacing),
                    Mathf.RoundToInt((world.Y - minY) / spacing),
                    Mathf.RoundToInt((world.Z - minZ) / spacing));
            }

            Vector3 CellToWorld(Vector3I cell)
            {
                return new Vector3(
                    minX + (cell.X * spacing),
                    minY + (cell.Y * spacing),
                    minZ + (cell.Z * spacing));
            }

            var startCell = WorldToCell(start);
            var goalCell = WorldToCell(goal);
            if (!InBounds(startCell) || !InBounds(goalCell))
            {
                result.FailureReason = "Jump target outside pathfinding bounds";
                return result;
            }

            if (isBlocked(goal, CollisionProbeRadius))
            {
                result.FailureReason = "Landing point is blocked";
                return result;
            }

            var blockedCache = new Dictionary<Vector3I, bool>();
            bool IsBlockedCell(Vector3I cell)
            {
                if (cell == startCell)
                {
                    return false;
                }

                if (blockedCache.TryGetValue(cell, out bool cached))
                {
                    return cached;
                }

                bool blocked = isBlocked(CellToWorld(cell), CollisionProbeRadius);
                blockedCache[cell] = blocked;
                return blocked;
            }

            float Heuristic(Vector3I cell)
            {
                var world = CellToWorld(cell);
                return world.DistanceTo(goal);
            }

            var openQueue = new PriorityQueue<NodeRecord, float>();
            var cameFrom = new Dictionary<Vector3I, Vector3I>();
            var gScore = new Dictionary<Vector3I, float>
            {
                [startCell] = 0f
            };

            var startRecord = new NodeRecord
            {
                Cell = startCell,
                GScore = 0f,
                FScore = Heuristic(startCell)
            };
            openQueue.Enqueue(startRecord, startRecord.FScore);

            int expanded = 0;
            bool found = false;

            while (openQueue.Count > 0)
            {
                var current = openQueue.Dequeue();
                if (!gScore.TryGetValue(current.Cell, out float bestKnown) || current.GScore > bestKnown + 0.0001f)
                {
                    continue;
                }

                expanded++;
                if (expanded > MaxExpandedNodes)
                {
                    break;
                }

                if (current.Cell == goalCell)
                {
                    found = true;
                    break;
                }

                Vector3 currentWorld = CellToWorld(current.Cell);
                foreach (var offset in NeighborOffsets)
                {
                    var neighbor = new Vector3I(
                        current.Cell.X + offset.X,
                        current.Cell.Y + offset.Y,
                        current.Cell.Z + offset.Z);

                    if (!InBounds(neighbor) || IsBlockedCell(neighbor))
                    {
                        continue;
                    }

                    Vector3 neighborWorld = CellToWorld(neighbor);
                    if (!IsSegmentClear(currentWorld, neighborWorld, isBlocked))
                    {
                        continue;
                    }

                    float tentativeG = current.GScore + (spacing * offset.DistanceScale);
                    if (!gScore.TryGetValue(neighbor, out float existingG) || tentativeG < existingG - 0.0001f)
                    {
                        gScore[neighbor] = tentativeG;
                        cameFrom[neighbor] = current.Cell;

                        float f = tentativeG + Heuristic(neighbor);
                        openQueue.Enqueue(
                            new NodeRecord
                            {
                                Cell = neighbor,
                                GScore = tentativeG,
                                FScore = f
                            },
                            f);
                    }
                }
            }

            if (!found || !gScore.ContainsKey(goalCell))
            {
                result.FailureReason = "No collision-free jump path found";
                return result;
            }

            var cellPath = ReconstructCells(cameFrom, startCell, goalCell);
            if (cellPath.Count < 2)
            {
                result.FailureReason = "Jump path reconstruction failed";
                return result;
            }

            cellPath = RemoveRedundantCells(cellPath);

            var basePath = new List<Vector3> { start };
            for (int i = 1; i < cellPath.Count - 1; i++)
            {
                basePath.Add(CellToWorld(cellPath[i]));
            }
            basePath.Add(goal);

            var smoothed = SmoothByLineOfSight(basePath, isBlocked);
            if (!TryBuildArcFromBasePath(smoothed, isBlocked, out var arcedPath, out float totalLength))
            {
                result.FailureReason = "No collision-free jump arc found";
                return result;
            }

            result.Success = true;
            result.TotalLength = totalLength;
            result.Waypoints.AddRange(arcedPath);
            return result;
        }

        private static NeighborOffset[] BuildNeighborOffsets()
        {
            var offsets = new List<NeighborOffset>(26);
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        if (x == 0 && y == 0 && z == 0)
                        {
                            continue;
                        }

                        float scale = Mathf.Sqrt((x * x) + (y * y) + (z * z));
                        offsets.Add(new NeighborOffset(x, y, z, scale));
                    }
                }
            }
            return offsets.ToArray();
        }

        private List<Vector3I> ReconstructCells(
            Dictionary<Vector3I, Vector3I> cameFrom,
            Vector3I start,
            Vector3I goal)
        {
            var path = new List<Vector3I> { goal };
            var current = goal;
            int guard = 0;

            while (current != start && guard < 200000)
            {
                guard++;
                if (!cameFrom.TryGetValue(current, out var previous))
                {
                    return new List<Vector3I>();
                }

                path.Add(previous);
                current = previous;
            }

            path.Reverse();
            return path;
        }

        private static List<Vector3I> RemoveRedundantCells(List<Vector3I> cells)
        {
            if (cells.Count <= 2)
            {
                return cells;
            }

            var output = new List<Vector3I> { cells[0] };
            for (int i = 1; i < cells.Count - 1; i++)
            {
                var a = output[output.Count - 1];
                var b = cells[i];
                var c = cells[i + 1];

                var dirAB = new Vector3I(
                    Math.Sign(b.X - a.X),
                    Math.Sign(b.Y - a.Y),
                    Math.Sign(b.Z - a.Z));
                var dirBC = new Vector3I(
                    Math.Sign(c.X - b.X),
                    Math.Sign(c.Y - b.Y),
                    Math.Sign(c.Z - b.Z));

                if (dirAB == dirBC)
                {
                    continue;
                }

                output.Add(b);
            }

            output.Add(cells[cells.Count - 1]);
            return output;
        }

        private List<Vector3> SmoothByLineOfSight(List<Vector3> points, Func<Vector3, float, bool> isBlocked)
        {
            if (points.Count <= 2)
            {
                return points;
            }

            var output = new List<Vector3> { points[0] };
            int anchor = 0;

            while (anchor < points.Count - 1)
            {
                int chosen = anchor + 1;
                for (int candidate = points.Count - 1; candidate > anchor + 1; candidate--)
                {
                    if (IsSegmentClear(points[anchor], points[candidate], isBlocked))
                    {
                        chosen = candidate;
                        break;
                    }
                }

                output.Add(points[chosen]);
                anchor = chosen;
            }

            return output;
        }

        private bool TryBuildArcFromBasePath(
            List<Vector3> basePath,
            Func<Vector3, float, bool> isBlocked,
            out List<Vector3> arcedPath,
            out float totalLength)
        {
            arcedPath = new List<Vector3>();
            totalLength = 0f;

            if (basePath == null || basePath.Count < 2)
            {
                return false;
            }

            var sampled = SamplePolyline(basePath, Mathf.Max(0.15f, SampleSpacing));
            for (int attempt = 0; attempt <= ArcHeightRetries; attempt++)
            {
                float arcHeight = MinimumArcHeight + (attempt * ArcHeightStep);
                var candidateArc = ApplyArcEnvelope(sampled, arcHeight);

                if (IsPolylineClear(candidateArc, isBlocked))
                {
                    arcedPath = candidateArc;
                    totalLength = ComputePolylineLength(candidateArc);
                    return true;
                }
            }

            return false;
        }

        private bool IsPolylineClear(List<Vector3> points, Func<Vector3, float, bool> isBlocked)
        {
            if (points == null || points.Count < 2)
            {
                return false;
            }

            for (int i = 1; i < points.Count - 1; i++)
            {
                if (isBlocked(points[i], CollisionProbeRadius))
                {
                    return false;
                }
            }

            for (int i = 1; i < points.Count; i++)
            {
                if (!IsSegmentClear(points[i - 1], points[i], isBlocked))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsSegmentClear(Vector3 from, Vector3 to, Func<Vector3, float, bool> isBlocked)
        {
            float distance = from.DistanceTo(to);
            if (distance < 0.0001f)
            {
                return true;
            }

            float step = Mathf.Max(0.1f, SampleSpacing * 0.5f);
            int samples = Mathf.Max(1, Mathf.CeilToInt(distance / step));

            for (int i = 1; i < samples; i++)
            {
                float t = i / (float)samples;
                Vector3 point = from.Lerp(to, t);
                if (isBlocked(point, CollisionProbeRadius))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<Vector3> SamplePolyline(IReadOnlyList<Vector3> waypoints, float spacing)
        {
            var output = new List<Vector3>();
            if (waypoints == null || waypoints.Count == 0)
            {
                return output;
            }

            output.Add(waypoints[0]);
            for (int i = 1; i < waypoints.Count; i++)
            {
                Vector3 from = waypoints[i - 1];
                Vector3 to = waypoints[i];
                float distance = from.DistanceTo(to);
                if (distance <= 0.0001f)
                {
                    continue;
                }

                int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(0.1f, spacing)));
                for (int s = 1; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    output.Add(from.Lerp(to, t));
                }
            }

            return output;
        }

        private static List<Vector3> ApplyArcEnvelope(IReadOnlyList<Vector3> sampledPath, float arcHeight)
        {
            var output = new List<Vector3>(sampledPath.Count);
            if (sampledPath.Count == 0)
            {
                return output;
            }

            float totalLength = ComputePolylineLength(sampledPath);
            if (totalLength <= 0.0001f)
            {
                output.AddRange(sampledPath);
                return output;
            }

            output.Add(sampledPath[0]);
            float travelled = 0f;
            for (int i = 1; i < sampledPath.Count; i++)
            {
                travelled += sampledPath[i - 1].DistanceTo(sampledPath[i]);
                float t = Mathf.Clamp(travelled / totalLength, 0f, 1f);
                float offset = 4f * t * (1f - t) * arcHeight;
                var p = sampledPath[i];
                p.Y += offset;
                output.Add(p);
            }

            return output;
        }

        private static float ComputePolylineLength(IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count < 2)
            {
                return 0f;
            }

            float total = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                total += points[i - 1].DistanceTo(points[i]);
            }

            return total;
        }
    }
}
