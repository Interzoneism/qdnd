using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Tracks weapons dropped on the ground during combat (from death, disarm, etc.).
    /// Combat-scoped: created at combat start, cleared at combat end.
    /// </summary>
    public class GroundItemService
    {
        /// <summary>
        /// Represents a weapon lying on the ground, available for pickup.
        /// </summary>
        public class GroundWeapon
        {
            /// <summary>Unique ID for this ground weapon instance.</summary>
            public string Id { get; set; }

            /// <summary>Display name.</summary>
            public string Name { get; set; }

            /// <summary>World position on the ground.</summary>
            public Vector3 Position { get; set; }

            /// <summary>Average damage per hit (used for scoring).</summary>
            public float AverageDamage { get; set; }

            /// <summary>True if this is a ranged weapon.</summary>
            public bool IsRanged { get; set; }

            /// <summary>Combatant ID of who dropped it (null = unknown).</summary>
            public string PreviousOwnerId { get; set; }

            /// <summary>Faction name of the previous owner (for party-ally check).</summary>
            public string PreviousOwnerFaction { get; set; }

            /// <summary>Whether the weapon requires proficiency to use effectively.</summary>
            public bool RequiresProficiency { get; set; } = true;

            /// <summary>Weapon type string for proficiency lookup (e.g., "Longsword", "Longbow").</summary>
            public string WeaponType { get; set; }

            /// <summary>Weapon category (Simple / Martial) for proficiency lookup.</summary>
            public string WeaponCategory { get; set; }
        }

        private readonly List<GroundWeapon> _weapons = new();

        /// <summary>Register a weapon dropped on the ground.</summary>
        public void DropWeapon(GroundWeapon weapon)
        {
            if (weapon == null) throw new ArgumentNullException(nameof(weapon));
            if (string.IsNullOrEmpty(weapon.Id))
                throw new ArgumentException("GroundWeapon must have a non-empty Id.", nameof(weapon));

            // Prevent duplicate IDs — replace if already registered (e.g., double-fired death event)
            var existingIdx = _weapons.FindIndex(w => w.Id == weapon.Id);
            if (existingIdx >= 0)
                _weapons[existingIdx] = weapon;
            else
                _weapons.Add(weapon);
        }

        /// <summary>Remove a weapon that was picked up. Returns true if found and removed.</summary>
        public bool PickupWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return false;

            var index = _weapons.FindIndex(w => w.Id == weaponId);
            if (index < 0) return false;

            _weapons.RemoveAt(index);
            return true;
        }

        /// <summary>Get all weapons within radius of a position (XZ distance only, ignoring Y).</summary>
        public List<GroundWeapon> GetWeaponsInRadius(Vector3 center, float radius)
        {
            if (radius <= 0f) return new List<GroundWeapon>();

            float radiusSq = radius * radius;
            return _weapons.Where(w =>
            {
                float dx = w.Position.X - center.X;
                float dz = w.Position.Z - center.Z;
                return (dx * dx + dz * dz) <= radiusSq;
            }).ToList();
        }

        /// <summary>Get all weapons currently on the ground.</summary>
        public IReadOnlyList<GroundWeapon> GetAllWeapons() => _weapons.AsReadOnly();

        /// <summary>Clear all ground weapons (called at combat end).</summary>
        public void Clear() => _weapons.Clear();
    }
}
