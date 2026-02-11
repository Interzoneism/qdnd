using System;
using System.Collections.Generic;
using Godot;

namespace QDND.Combat.Movement
{
    /// <summary>
    /// Tactical A* grid pathfinder for turn-based movement.
    /// Uses XZ plane for navigation and allows weighted traversal costs.
    /// </summary>
    public sealed class TacticalPathfinder
    {
        private readonly struct NeighborOffset
        {
            public readonly int X;
            public readonly int Y;
            public readonly bool IsDiagonal;

            public NeighborOffset(int x, int y, bool isDiagonal)
            {
                X = x;
                Y = y;
                IsDiagonal = isDiagonal;
            }
        }

        private static readonly NeighborOffset[] NeighborOffsets =
        {
            new NeighborOffset(1, 0, false),
            new NeighborOffset(-1, 0, false),
            new NeighborOffset(0, 1, false),
            new NeighborOffset(0, -1, false),
            new NeighborOffset(1, 1, true),
            new NeighborOffset(-1, 1, true),
            new NeighborOffset(1, -1, true),
            new NeighborOffset(-1, -1, true)
        };

        private sealed class NodeRecord
        {
            public Vector2I Cell;
            public float GScore;
            public float FScore;
        }

        public float NodeSpacing { get; set; } = 1f;
        public int SearchPaddingCells { get; set; } = 8;
        public int MaxExpandedNodes { get; set; } = 20000;

        public PathfindingResult FindPath(
            Vector3 start,
            Vector3 goal,
            Func<Vector3, bool> isBlocked,
            Func<Vector3, float> getTraversalMultiplier,
            float? maxCostBudget = null,
            int? maxSearchRadiusCells = null)
        {
            var result = new PathfindingResult
            {
                StraightLineDistance = start.DistanceTo(goal)
            };

            float spacing = Mathf.Max(0.25f, NodeSpacing);
            var startCell = WorldToCell(start, spacing);
            var goalCell = WorldToCell(goal, spacing);

            if (startCell == goalCell)
            {
                result.Success = true;
                result.Waypoints.Add(start);
                if (goal.DistanceTo(start) > 0.001f)
                {
                    result.Waypoints.Add(goal);
                }
                result.TotalCost = 0f;
                return result;
            }

            int straightCells = Mathf.CeilToInt(result.StraightLineDistance / spacing);
            int radiusCells = maxSearchRadiusCells
                ?? (maxCostBudget.HasValue
                    ? Mathf.CeilToInt(maxCostBudget.Value / spacing) + SearchPaddingCells
                    : (straightCells * 3) + SearchPaddingCells + 12);
            radiusCells = Mathf.Clamp(radiusCells, straightCells + SearchPaddingCells + 2, 256);

            int minX = Math.Min(startCell.X, goalCell.X) - radiusCells;
            int maxX = Math.Max(startCell.X, goalCell.X) + radiusCells;
            int minY = Math.Min(startCell.Y, goalCell.Y) - radiusCells;
            int maxY = Math.Max(startCell.Y, goalCell.Y) + radiusCells;

            bool InBounds(Vector2I cell) =>
                cell.X >= minX && cell.X <= maxX && cell.Y >= minY && cell.Y <= maxY;

            var blockedCache = new Dictionary<Vector2I, bool>();
            var multiplierCache = new Dictionary<Vector2I, float>();

            bool IsCellBlocked(Vector2I cell)
            {
                if (cell == startCell)
                {
                    return false;
                }

                if (blockedCache.TryGetValue(cell, out bool cached))
                {
                    return cached;
                }

                var world = CellToWorld(cell, start.Y, spacing);
                bool blocked = isBlocked?.Invoke(world) == true;
                blockedCache[cell] = blocked;
                return blocked;
            }

            float GetMultiplier(Vector2I cell)
            {
                if (multiplierCache.TryGetValue(cell, out float cached))
                {
                    return cached;
                }

                var world = CellToWorld(cell, start.Y, spacing);
                float multiplier = getTraversalMultiplier?.Invoke(world) ?? 1f;
                multiplier = Math.Max(1f, multiplier);
                multiplierCache[cell] = multiplier;
                return multiplier;
            }

            if (IsCellBlocked(goalCell))
            {
                result.Success = false;
                result.FailureReason = "Destination blocked by obstacle";
                return result;
            }

            var openQueue = new PriorityQueue<NodeRecord, float>();
            var cameFrom = new Dictionary<Vector2I, Vector2I>();
            var gScore = new Dictionary<Vector2I, float>
            {
                [startCell] = 0f
            };

            float Heuristic(Vector2I cell)
            {
                float dx = (goalCell.X - cell.X) * spacing;
                float dz = (goalCell.Y - cell.Y) * spacing;
                return Mathf.Sqrt((dx * dx) + (dz * dz));
            }

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

                foreach (var offset in NeighborOffsets)
                {
                    var neighbor = new Vector2I(current.Cell.X + offset.X, current.Cell.Y + offset.Y);
                    if (!InBounds(neighbor))
                    {
                        continue;
                    }

                    if (IsCellBlocked(neighbor))
                    {
                        continue;
                    }

                    if (offset.IsDiagonal)
                    {
                        // Prevent diagonal corner-cutting through blocked orthogonal cells.
                        var orthA = new Vector2I(current.Cell.X + offset.X, current.Cell.Y);
                        var orthB = new Vector2I(current.Cell.X, current.Cell.Y + offset.Y);
                        if (IsCellBlocked(orthA) || IsCellBlocked(orthB))
                        {
                            continue;
                        }
                    }

                    float baseStep = spacing * (offset.IsDiagonal ? 1.41421356f : 1f);
                    float multiplier = GetMultiplier(neighbor);
                    float stepCost = baseStep * multiplier;
                    float tentativeG = current.GScore + stepCost;

                    if (maxCostBudget.HasValue && tentativeG > maxCostBudget.Value + (spacing * 1.5f))
                    {
                        continue;
                    }

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

            if (!found || !gScore.TryGetValue(goalCell, out float finalCost))
            {
                result.Success = false;
                result.FailureReason = "No traversable path around obstacles";
                return result;
            }

            result.Success = true;
            result.TotalCost = finalCost;

            var cells = ReconstructCells(cameFrom, startCell, goalCell);
            if (cells.Count == 0)
            {
                result.Success = false;
                result.FailureReason = "Path reconstruction failed";
                return result;
            }

            cells = RemoveCollinear(cells);

            result.Waypoints.Add(start);
            for (int i = 1; i < cells.Count - 1; i++)
            {
                result.Waypoints.Add(CellToWorld(cells[i], start.Y, spacing));
            }
            result.Waypoints.Add(goal);

            foreach (var cell in cells)
            {
                float multiplier = GetMultiplier(cell);
                if (multiplier > 1f)
                {
                    result.HasDifficultTerrain = true;
                }
                if (multiplier > result.MaxCostMultiplier)
                {
                    result.MaxCostMultiplier = multiplier;
                }
            }

            return result;
        }

        private static Vector2I WorldToCell(Vector3 world, float spacing)
        {
            int x = Mathf.RoundToInt(world.X / spacing);
            int y = Mathf.RoundToInt(world.Z / spacing);
            return new Vector2I(x, y);
        }

        private static Vector3 CellToWorld(Vector2I cell, float y, float spacing)
            => new Vector3(cell.X * spacing, y, cell.Y * spacing);

        private static List<Vector2I> ReconstructCells(
            Dictionary<Vector2I, Vector2I> cameFrom,
            Vector2I start,
            Vector2I goal)
        {
            var path = new List<Vector2I> { goal };
            var current = goal;
            int guard = 0;

            while (current != start && guard < 100000)
            {
                guard++;
                if (!cameFrom.TryGetValue(current, out var prev))
                {
                    return new List<Vector2I>();
                }

                path.Add(prev);
                current = prev;
            }

            path.Reverse();
            return path;
        }

        private static List<Vector2I> RemoveCollinear(List<Vector2I> cells)
        {
            if (cells.Count <= 2)
            {
                return cells;
            }

            var output = new List<Vector2I> { cells[0] };
            for (int i = 1; i < cells.Count - 1; i++)
            {
                var a = output[output.Count - 1];
                var b = cells[i];
                var c = cells[i + 1];

                int abX = b.X - a.X;
                int abY = b.Y - a.Y;
                int bcX = c.X - b.X;
                int bcY = c.Y - b.Y;

                if ((abX * bcY) == (abY * bcX))
                {
                    continue;
                }

                output.Add(b);
            }

            output.Add(cells[cells.Count - 1]);
            return output;
        }
    }

    public sealed class PathfindingResult
    {
        public bool Success { get; set; }
        public string FailureReason { get; set; }
        public float TotalCost { get; set; }
        public float StraightLineDistance { get; set; }
        public float MaxCostMultiplier { get; set; } = 1f;
        public bool HasDifficultTerrain { get; set; }
        public List<Vector3> Waypoints { get; } = new();
    }
}
