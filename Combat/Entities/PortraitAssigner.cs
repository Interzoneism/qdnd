using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace QDND.Combat.Entities
{
    /// <summary>
    /// Assigns random portrait textures to combatants from the placeholder portrait folder.
    /// TODO: Replace with proper character-specific portrait mapping once final art is available.
    /// </summary>
    public static class PortraitAssigner
    {
        /// <summary>
        /// Directory containing temporary placeholder portraits.
        /// </summary>
        private const string PortraitDirectory = "res://assets/Images/Portraits Temp";

        private static string[] _cachedPaths;

        /// <summary>
        /// Scans the portrait directory and caches available portrait paths.
        /// </summary>
        private static string[] GetAvailablePortraits()
        {
            if (_cachedPaths != null)
                return _cachedPaths;

            var paths = new List<string>();
            using var dir = DirAccess.Open(PortraitDirectory);
            if (dir != null)
            {
                dir.ListDirBegin();
                string fileName = dir.GetNext();
                while (!string.IsNullOrEmpty(fileName))
                {
                    // Only include .png files, skip .import files
                    if (!dir.CurrentIsDir() && fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        paths.Add($"{PortraitDirectory}/{fileName}");
                    }
                    fileName = dir.GetNext();
                }
                dir.ListDirEnd();
            }
            else
            {
                GD.PrintErr($"[PortraitAssigner] Could not open portrait directory: {PortraitDirectory}");
            }

            _cachedPaths = paths.ToArray();
            return _cachedPaths;
        }

        /// <summary>
        /// Assigns a random portrait to each combatant that doesn't already have one.
        /// Uses the provided RNG seed for deterministic assignment.
        /// </summary>
        // TODO: Replace random assignment with character-class or name-based portrait selection
        public static void AssignRandomPortraits(IList<Combatant> combatants, int seed = 0)
        {
            var portraits = GetAvailablePortraits();
            if (portraits.Length == 0)
            {
                GD.PrintErr("[PortraitAssigner] No portraits found — combatants will have no portraits.");
                return;
            }

            var rng = new Random(seed);

            foreach (var combatant in combatants)
            {
                if (string.IsNullOrEmpty(combatant.PortraitPath))
                {
                    combatant.PortraitPath = portraits[rng.Next(portraits.Length)];
                }
            }
        }

        /// <summary>
        /// Assigns a random portrait to a single combatant (e.g. a summon spawned mid-combat).
        /// </summary>
        // TODO: Replace random assignment with character-class or name-based portrait selection
        public static void AssignRandomPortrait(Combatant combatant)
        {
            if (!string.IsNullOrEmpty(combatant.PortraitPath))
                return;

            var portraits = GetAvailablePortraits();
            if (portraits.Length == 0)
                return;

            // Use combatant ID hash for deterministic-per-combatant assignment
            var rng = new Random(combatant.Id.GetHashCode());
            combatant.PortraitPath = portraits[rng.Next(portraits.Length)];
        }

        /// <summary>
        /// Clears the cached portrait list (useful if portraits folder changes at runtime).
        /// </summary>
        public static void ClearCache()
        {
            _cachedPaths = null;
        }
    }
}
