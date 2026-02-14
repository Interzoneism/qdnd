using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Data.Actions;
using QDND.Data.Parsers;
using QDND.Data.Spells;

namespace QDND.Data.Actions
{
    /// <summary>
    /// High-level API for loading action definitions from BG3 data files.
    /// Handles parsing, conversion, and error reporting.
    /// </summary>
    public class ActionDataLoader
    {
        private readonly BG3SpellParser _parser;
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();
        private int _loadedCount = 0;
        private int _failedCount = 0;

        /// <summary>
        /// Errors encountered during loading.
        /// </summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>
        /// Warnings encountered during loading.
        /// </summary>
        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>
        /// Number of actions successfully loaded.
        /// </summary>
        public int LoadedCount => _loadedCount;

        /// <summary>
        /// Number of actions that failed to load.
        /// </summary>
        public int FailedCount => _failedCount;

        public ActionDataLoader()
        {
            _parser = new BG3SpellParser();
        }

        /// <summary>
        /// Load all spells from the BG3_Data/Spells directory.
        /// </summary>
        /// <param name="bg3DataPath">Path to BG3_Data directory (e.g., "res://BG3_Data" or absolute path).</param>
        /// <param name="registry">The action registry to populate.</param>
        /// <returns>Number of actions loaded.</returns>
        public int LoadAllSpells(string bg3DataPath, ActionRegistry registry)
        {
            if (registry == null)
            {
                _errors.Add("ActionRegistry cannot be null");
                return 0;
            }

            // Convert Godot res:// path to absolute if needed
            string spellsPath = ConvertToAbsolutePath(bg3DataPath, "Spells");

            if (!Directory.Exists(spellsPath))
            {
                _errors.Add($"Spells directory not found: {spellsPath}");
                return 0;
            }

            _loadedCount = 0;
            _failedCount = 0;

            // Parse all Spell_*.txt files
            var spells = _parser.ParseDirectory(spellsPath, "Spell_*.txt");

            // Resolve inheritance
            _parser.ResolveInheritance();

            // Collect errors and warnings from parser
            foreach (var error in _parser.Errors)
                _errors.Add(error);
            foreach (var warning in _parser.Warnings)
                _warnings.Add(warning);

            // Convert and register each spell
            foreach (var spell in spells)
            {
                try
                {
                    var action = BG3ActionConverter.ConvertToAction(spell, includeRawFormulas: true);
                    
                    if (registry.RegisterAction(action, overwrite: false))
                    {
                        _loadedCount++;
                    }
                    else
                    {
                        _warnings.Add($"Failed to register action '{action.Id}' (may be duplicate)");
                        _failedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _errors.Add($"Failed to convert spell '{spell.Id}': {ex.Message}");
                    _failedCount++;
                }
            }

            // Collect registry errors
            foreach (var error in registry.Errors)
                _errors.Add(error);
            foreach (var warning in registry.Warnings)
                _warnings.Add(warning);

            return _loadedCount;
        }

        /// <summary>
        /// Load spells of a specific level.
        /// </summary>
        /// <param name="bg3DataPath">Path to BG3_Data directory.</param>
        /// <param name="registry">The action registry to populate.</param>
        /// <param name="level">Spell level to load (0 = cantrips, 1-9 = leveled spells).</param>
        /// <returns>Number of actions loaded.</returns>
        public int LoadSpellsByLevel(string bg3DataPath, ActionRegistry registry, int level)
        {
            // Load all first, then filter - BG3 spell files aren't organized by level
            int totalLoaded = LoadAllSpells(bg3DataPath, registry);
            
            // Return count of actions at requested level
            return registry.GetActionsBySpellLevel(level).Count;
        }

        /// <summary>
        /// Load only cantrips (level 0 spells).
        /// </summary>
        /// <param name="bg3DataPath">Path to BG3_Data directory.</param>
        /// <param name="registry">The action registry to populate.</param>
        /// <returns>Number of cantrips loaded.</returns>
        public int LoadCantrips(string bg3DataPath, ActionRegistry registry)
        {
            return LoadSpellsByLevel(bg3DataPath, registry, 0);
        }

        /// <summary>
        /// Load level 1 spells.
        /// </summary>
        public int LoadLevel1Spells(string bg3DataPath, ActionRegistry registry)
        {
            return LoadSpellsByLevel(bg3DataPath, registry, 1);
        }

        /// <summary>
        /// Load level 2 spells.
        /// </summary>
        public int LoadLevel2Spells(string bg3DataPath, ActionRegistry registry)
        {
            return LoadSpellsByLevel(bg3DataPath, registry, 2);
        }

        /// <summary>
        /// Load level 3 spells.
        /// </summary>
        public int LoadLevel3Spells(string bg3DataPath, ActionRegistry registry)
        {
            return LoadSpellsByLevel(bg3DataPath, registry, 3);
        }

        /// <summary>
        /// Load level 4 spells.
        /// </summary>
        public int LoadLevel4Spells(string bg3DataPath, ActionRegistry registry)
        {
            return LoadSpellsByLevel(bg3DataPath, registry, 4);
        }

        /// <summary>
        /// Load level 5 spells.
        /// </summary>
        public int LoadLevel5Spells(string bg3DataPath, ActionRegistry registry)
        {
            return LoadSpellsByLevel(bg3DataPath, registry, 5);
        }

        /// <summary>
        /// Load level 6 spells.
        /// </summary>
        public int LoadLevel6Spells(string bg3DataPath, ActionRegistry registry)
        {
            return LoadSpellsByLevel(bg3DataPath, registry, 6);
        }

        /// <summary>
        /// Load level 7 spells.
        /// </summary>
        public int LoadLevel7Spells(string bg3DataPath, ActionRegistry registry)
        {
            return LoadSpellsByLevel(bg3DataPath, registry, 7);
        }

        /// <summary>
        /// Load level 8 spells.
        /// </summary>
        public int LoadLevel8Spells(string bg3DataPath, ActionRegistry registry)
        {
            return LoadSpellsByLevel(bg3DataPath, registry, 8);
        }

        /// <summary>
        /// Load level 9 spells.
        /// </summary>
        public int LoadLevel9Spells(string bg3DataPath, ActionRegistry registry)
        {
            return LoadSpellsByLevel(bg3DataPath, registry, 9);
        }

        /// <summary>
        /// Load spells by type (Target, Projectile, Shout, Zone, etc).
        /// </summary>
        /// <param name="bg3DataPath">Path to BG3_Data directory.</param>
        /// <param name="registry">The action registry to populate.</param>
        /// <param name="spellTypes">Spell types to load.</param>
        /// <returns>Number of actions loaded.</returns>
        public int LoadSpellsByType(string bg3DataPath, ActionRegistry registry, params BG3SpellType[] spellTypes)
        {
            // Load all first
            LoadAllSpells(bg3DataPath, registry);

            // Filter by type
            var typeSet = new HashSet<string>(spellTypes.Select(t => t.ToString()));
            return registry.Query(a => typeSet.Contains(a.BG3SpellType ?? "")).Count;
        }

        /// <summary>
        /// Load damage spells only.
        /// </summary>
        /// <param name="bg3DataPath">Path to BG3_Data directory.</param>
        /// <param name="registry">The action registry to populate.</param>
        /// <returns>Number of damage spells loaded.</returns>
        public int LoadDamageSpells(string bg3DataPath, ActionRegistry registry)
        {
            LoadAllSpells(bg3DataPath, registry);
            return registry.GetDamageActions().Count;
        }

        /// <summary>
        /// Load healing spells only.
        /// </summary>
        /// <param name="bg3DataPath">Path to BG3_Data directory.</param>
        /// <param name="registry">The action registry to populate.</param>
        /// <returns>Number of healing spells loaded.</returns>
        public int LoadHealingSpells(string bg3DataPath, ActionRegistry registry)
        {
            LoadAllSpells(bg3DataPath, registry);
            return registry.GetHealingActions().Count;
        }

        /// <summary>
        /// Load spells from a specific spell school.
        /// </summary>
        /// <param name="bg3DataPath">Path to BG3_Data directory.</param>
        /// <param name="registry">The action registry to populate.</param>
        /// <param name="school">The spell school to load.</param>
        /// <returns>Number of spells loaded.</returns>
        public int LoadSpellsBySchool(string bg3DataPath, ActionRegistry registry, SpellSchool school)
        {
            LoadAllSpells(bg3DataPath, registry);
            return registry.GetActionsBySchool(school).Count;
        }

        /// <summary>
        /// Convert Godot res:// path or relative path to absolute filesystem path.
        /// </summary>
        private string ConvertToAbsolutePath(string basePath, string subPath)
        {
            // Handle Godot res:// protocol
            if (basePath.StartsWith("res://"))
            {
                // Strip res:// and convert to project-relative path
                basePath = basePath.Substring("res://".Length);
                
                // Assume running from project root or get project base path
                string projectRoot = GetProjectRoot();
                basePath = Path.Combine(projectRoot, basePath);
            }

            // Combine with sub-path
            string fullPath = Path.Combine(basePath, subPath);

            // Normalize path
            return Path.GetFullPath(fullPath);
        }

        /// <summary>
        /// Get the project root directory (where project.godot is).
        /// </summary>
        private string GetProjectRoot()
        {
            // Try to find project.godot in current or parent directories
            string current = Directory.GetCurrentDirectory();
            
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(Path.Combine(current, "project.godot")))
                    return current;
                
                var parent = Directory.GetParent(current);
                if (parent == null)
                    break;
                current = parent.FullName;
            }

            // Fallback to current directory
            return Directory.GetCurrentDirectory();
        }

        /// <summary>
        /// Clear all accumulated errors and warnings.
        /// </summary>
        public void ClearDiagnostics()
        {
            _errors.Clear();
            _warnings.Clear();
            _loadedCount = 0;
            _failedCount = 0;
        }

        /// <summary>
        /// Get a summary report of the loading operation.
        /// </summary>
        /// <returns>Multi-line summary string.</returns>
        public string GetLoadingSummary()
        {
            var lines = new List<string>
            {
                "=== Action Data Loading Summary ===",
                $"Successfully loaded: {_loadedCount}",
                $"Failed to load: {_failedCount}",
                $"Errors: {_errors.Count}",
                $"Warnings: {_warnings.Count}",
            };

            if (_errors.Count > 0)
            {
                lines.Add("");
                lines.Add("Recent Errors:");
                foreach (var error in _errors.TakeLast(5))
                {
                    lines.Add($"  - {error}");
                }
                if (_errors.Count > 5)
                    lines.Add($"  ... and {_errors.Count - 5} more");
            }

            if (_warnings.Count > 0)
            {
                lines.Add("");
                lines.Add("Recent Warnings:");
                foreach (var warning in _warnings.TakeLast(5))
                {
                    lines.Add($"  - {warning}");
                }
                if (_warnings.Count > 5)
                    lines.Add($"  ... and {_warnings.Count - 5} more");
            }

            return string.Join("\n", lines);
        }
    }
}
