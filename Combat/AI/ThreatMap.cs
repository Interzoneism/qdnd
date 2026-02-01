using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;

namespace QDND.Combat.AI
{
    /// <summary>
    /// Threat level at a position.
    /// </summary>
    public struct ThreatInfo
    {
        public float TotalThreat;
        public int MeleeThreats;
        public int RangedThreats;
        public float NearestEnemyDistance;
        public bool IsInMeleeRange;
        public bool IsExposed;  // No cover
    }

    /// <summary>
    /// Calculated threat/value for a specific cell.
    /// </summary>
    public class ThreatCell
    {
        public Vector3I GridPosition { get; set; }
        public Vector3 WorldPosition { get; set; }
        public float Threat { get; set; }
        public float Value { get; set; }       // Tactical value (high ground, cover, etc)
        public float NetScore { get; set; }    // Value - Threat
        public ThreatInfo ThreatInfo { get; set; }
        public bool IsReachable { get; set; }
        public float MoveCost { get; set; }
    }

    /// <summary>
    /// Calculates threat levels across the battlefield.
    /// </summary>
    public class ThreatMap
    {
        private readonly Dictionary<Vector3I, ThreatCell> _cells = new();
        private readonly float _cellSize;
        private readonly float _meleeRange = 5f;
        private readonly float _rangedRange = 30f;

        public ThreatMap(float cellSize = 5f)
        {
            _cellSize = cellSize;
        }

        /// <summary>
        /// Build threat map from current combat state.
        /// </summary>
        public void Calculate(IEnumerable<Combatant> enemies, Vector3 center, float radius)
        {
            _cells.Clear();
            
            var enemyList = enemies.Where(e => e.Resources?.CurrentHP > 0).ToList();
            
            // Generate grid cells within radius
            int cellRadius = (int)(radius / _cellSize);
            
            for (int x = -cellRadius; x <= cellRadius; x++)
            {
                for (int z = -cellRadius; z <= cellRadius; z++)
                {
                    var gridPos = new Vector3I(x, 0, z);
                    var worldPos = center + new Vector3(x * _cellSize, 0, z * _cellSize);
                    
                    if (worldPos.DistanceTo(center) > radius)
                        continue;

                    var cell = new ThreatCell
                    {
                        GridPosition = gridPos,
                        WorldPosition = worldPos,
                        ThreatInfo = CalculateThreatAt(worldPos, enemyList),
                        IsReachable = true // Would check pathfinding
                    };
                    
                    cell.Threat = cell.ThreatInfo.TotalThreat;
                    _cells[gridPos] = cell;
                }
            }
        }

        /// <summary>
        /// Calculate threat at a specific position.
        /// </summary>
        public ThreatInfo CalculateThreatAt(Vector3 position, IEnumerable<Combatant> enemies)
        {
            var info = new ThreatInfo
            {
                NearestEnemyDistance = float.MaxValue,
                IsExposed = true
            };

            foreach (var enemy in enemies)
            {
                if (enemy.Resources?.CurrentHP <= 0)
                    continue;

                float distance = position.DistanceTo(enemy.Position);
                
                if (distance < info.NearestEnemyDistance)
                    info.NearestEnemyDistance = distance;

                if (distance <= _meleeRange)
                {
                    info.MeleeThreats++;
                    info.IsInMeleeRange = true;
                    info.TotalThreat += 3f; // Melee is dangerous
                }
                else if (distance <= _rangedRange)
                {
                    info.RangedThreats++;
                    info.TotalThreat += 1f;
                }
            }

            return info;
        }

        /// <summary>
        /// Get threat at a world position.
        /// </summary>
        public float GetThreat(Vector3 worldPosition)
        {
            var gridPos = WorldToGrid(worldPosition);
            return _cells.TryGetValue(gridPos, out var cell) ? cell.Threat : 0f;
        }

        /// <summary>
        /// Get the cell at a world position.
        /// </summary>
        public ThreatCell GetCell(Vector3 worldPosition)
        {
            var gridPos = WorldToGrid(worldPosition);
            return _cells.TryGetValue(gridPos, out var cell) ? cell : null;
        }

        /// <summary>
        /// Get all calculated cells.
        /// </summary>
        public IEnumerable<ThreatCell> GetAllCells() => _cells.Values;

        /// <summary>
        /// Find lowest threat cells within movement range.
        /// </summary>
        public IEnumerable<ThreatCell> GetSafestCells(Vector3 from, float maxMovement, int count = 5)
        {
            return _cells.Values
                .Where(c => c.WorldPosition.DistanceTo(from) <= maxMovement)
                .OrderBy(c => c.Threat)
                .Take(count);
        }

        /// <summary>
        /// Find highest value cells (good tactical positions).
        /// </summary>
        public IEnumerable<ThreatCell> GetBestTacticalCells(Vector3 from, float maxMovement, int count = 5)
        {
            return _cells.Values
                .Where(c => c.WorldPosition.DistanceTo(from) <= maxMovement)
                .OrderByDescending(c => c.NetScore)
                .Take(count);
        }

        private Vector3I WorldToGrid(Vector3 worldPos)
        {
            return new Vector3I(
                (int)(worldPos.X / _cellSize),
                0,
                (int)(worldPos.Z / _cellSize)
            );
        }

        /// <summary>
        /// Set tactical value for a cell (used by height/cover systems).
        /// </summary>
        public void SetCellValue(Vector3 worldPos, float value)
        {
            var gridPos = WorldToGrid(worldPos);
            if (_cells.TryGetValue(gridPos, out var cell))
            {
                cell.Value = value;
                cell.NetScore = cell.Value - cell.Threat;
            }
        }
    }
}
