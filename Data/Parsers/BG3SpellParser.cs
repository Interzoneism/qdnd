using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using QDND.Data.Spells;

namespace QDND.Data.Parsers
{
    /// <summary>
    /// Parser for BG3's Stats TXT spell definition files.
    /// Handles the custom format: "new entry", "data", and "using" directives.
    /// </summary>
    public class BG3SpellParser
    {
        private readonly Dictionary<string, BG3SpellData> _parsedSpells = new();
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();
        
        /// <summary>All parsing errors encountered.</summary>
        public IReadOnlyList<string> Errors => _errors;
        
        /// <summary>All parsing warnings encountered.</summary>
        public IReadOnlyList<string> Warnings => _warnings;
        
        /// <summary>
        /// Parses a single BG3 spell TXT file.
        /// </summary>
        /// <param name="filePath">Path to the spell TXT file.</param>
        /// <returns>List of parsed spells.</returns>
        public List<BG3SpellData> ParseFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _errors.Add($"File not found: {filePath}");
                return new List<BG3SpellData>();
            }
            
            var spells = new List<BG3SpellData>();
            
            try
            {
                var lines = File.ReadAllLines(filePath);
                var currentSpell = (BG3SpellData)null;
                var lineNumber = 0;
                
                foreach (var rawLine in lines)
                {
                    lineNumber++;
                    var line = rawLine.Trim();
                    
                    // Skip empty lines and comments
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;
                    
                    // New entry directive
                    if (line.StartsWith("new entry", StringComparison.OrdinalIgnoreCase))
                    {
                        // Save previous spell
                        if (currentSpell != null)
                        {
                            spells.Add(currentSpell);
                            _parsedSpells[currentSpell.Id] = currentSpell;
                        }
                        
                        // Parse entry name: new entry "SpellName"
                        var entryName = ExtractQuotedValue(line, "new entry");
                        if (string.IsNullOrEmpty(entryName))
                        {
                            _errors.Add($"{filePath}:{lineNumber} - Could not parse entry name from: {line}");
                            currentSpell = null;
                            continue;
                        }
                        
                        currentSpell = new BG3SpellData { Id = entryName };
                    }
                    // Type directive (usually "type SpellData")
                    else if (line.StartsWith("type", StringComparison.OrdinalIgnoreCase))
                    {
                        // We don't need to do anything specific with this
                        continue;
                    }
                    // Using directive (inheritance)
                    else if (line.StartsWith("using", StringComparison.OrdinalIgnoreCase))
                    {
                        if (currentSpell == null)
                        {
                            _warnings.Add($"{filePath}:{lineNumber} - 'using' directive outside of spell entry");
                            continue;
                        }
                        
                        var parentName = ExtractQuotedValue(line, "using");
                        if (!string.IsNullOrEmpty(parentName))
                        {
                            currentSpell.ParentId = parentName;
                        }
                    }
                    // Data directive
                    else if (line.StartsWith("data", StringComparison.OrdinalIgnoreCase))
                    {
                        if (currentSpell == null)
                        {
                            _warnings.Add($"{filePath}:{lineNumber} - 'data' directive outside of spell entry");
                            continue;
                        }
                        
                        var (key, value) = ParseDataLine(line);
                        if (!string.IsNullOrEmpty(key))
                        {
                            SetSpellProperty(currentSpell, key, value);
                        }
                        else
                        {
                            _warnings.Add($"{filePath}:{lineNumber} - Could not parse data line: {line}");
                        }
                    }
                }
                
                // Don't forget the last spell
                if (currentSpell != null)
                {
                    spells.Add(currentSpell);
                    _parsedSpells[currentSpell.Id] = currentSpell;
                }
            }
            catch (Exception ex)
            {
                _errors.Add($"Failed to parse {filePath}: {ex.Message}");
            }
            
            return spells;
        }
        
        /// <summary>
        /// Parses multiple spell files from a directory.
        /// </summary>
        /// <param name="directoryPath">Directory containing spell TXT files.</param>
        /// <param name="pattern">File pattern (default: "Spell_*.txt").</param>
        /// <returns>List of all parsed spells.</returns>
        public List<BG3SpellData> ParseDirectory(string directoryPath, string pattern = "Spell_*.txt")
        {
            if (!Directory.Exists(directoryPath))
            {
                _errors.Add($"Directory not found: {directoryPath}");
                return new List<BG3SpellData>();
            }
            
            var allSpells = new List<BG3SpellData>();
            var files = Directory.GetFiles(directoryPath, pattern);
            
            Console.WriteLine($"[BG3SpellParser] Found {files.Length} spell files in {directoryPath}");
            
            foreach (var file in files)
            {
                var spells = ParseFile(file);
                allSpells.AddRange(spells);
                Console.WriteLine($"[BG3SpellParser] Parsed {spells.Count} spells from {Path.GetFileName(file)}");
            }
            
            return allSpells;
        }
        
        /// <summary>
        /// Resolves inheritance by copying properties from parent spells.
        /// Call this after parsing all files.
        /// </summary>
        public void ResolveInheritance()
        {
            foreach (var spell in _parsedSpells.Values)
            {
                if (!string.IsNullOrEmpty(spell.ParentId))
                {
                    ApplyInheritance(spell);
                }
            }
        }
        
        /// <summary>
        /// Gets a spell by ID (after parsing).
        /// </summary>
        public BG3SpellData GetSpell(string id)
        {
            _parsedSpells.TryGetValue(id, out var spell);
            return spell;
        }
        
        /// <summary>
        /// Gets all parsed spells.
        /// </summary>
        public IReadOnlyDictionary<string, BG3SpellData> GetAllSpells()
        {
            return _parsedSpells;
        }
        
        /// <summary>
        /// Clears all parsed data and errors.
        /// </summary>
        public void Clear()
        {
            _parsedSpells.Clear();
            _errors.Clear();
            _warnings.Clear();
        }
        
        // --- Private Helpers ---
        
        private void ApplyInheritance(BG3SpellData spell)
        {
            if (string.IsNullOrEmpty(spell.ParentId))
                return;
            
            if (!_parsedSpells.TryGetValue(spell.ParentId, out var parent))
            {
                _warnings.Add($"Spell '{spell.Id}' references unknown parent '{spell.ParentId}'");
                return;
            }
            
            // Recursively resolve parent's inheritance first
            if (!string.IsNullOrEmpty(parent.ParentId))
            {
                ApplyInheritance(parent);
            }
            
            // Copy properties from parent if not set in child
            // (child properties take precedence)
            
            if (string.IsNullOrEmpty(spell.DisplayName) && !string.IsNullOrEmpty(parent.DisplayName))
                spell.DisplayName = parent.DisplayName;
            
            if (string.IsNullOrEmpty(spell.Description) && !string.IsNullOrEmpty(parent.Description))
                spell.Description = parent.Description;
            
            if (string.IsNullOrEmpty(spell.Icon) && !string.IsNullOrEmpty(parent.Icon))
                spell.Icon = parent.Icon;
            
            if (spell.SpellType == BG3SpellType.Unknown && parent.SpellType != BG3SpellType.Unknown)
                spell.SpellType = parent.SpellType;
            
            if (spell.Level == 0 && parent.Level > 0)
                spell.Level = parent.Level;
            
            if (string.IsNullOrEmpty(spell.SpellSchool) && !string.IsNullOrEmpty(parent.SpellSchool))
                spell.SpellSchool = parent.SpellSchool;
            
            if (string.IsNullOrEmpty(spell.SpellProperties) && !string.IsNullOrEmpty(parent.SpellProperties))
                spell.SpellProperties = parent.SpellProperties;
            
            if (string.IsNullOrEmpty(spell.SpellRoll) && !string.IsNullOrEmpty(parent.SpellRoll))
                spell.SpellRoll = parent.SpellRoll;
            
            if (string.IsNullOrEmpty(spell.SpellSuccess) && !string.IsNullOrEmpty(parent.SpellSuccess))
                spell.SpellSuccess = parent.SpellSuccess;
            
            if (string.IsNullOrEmpty(spell.SpellFail) && !string.IsNullOrEmpty(parent.SpellFail))
                spell.SpellFail = parent.SpellFail;
            
            if (string.IsNullOrEmpty(spell.TargetRadius) && !string.IsNullOrEmpty(parent.TargetRadius))
                spell.TargetRadius = parent.TargetRadius;
            
            if (string.IsNullOrEmpty(spell.AreaRadius) && !string.IsNullOrEmpty(parent.AreaRadius))
                spell.AreaRadius = parent.AreaRadius;
            
            // Zone-specific shape fields
            if (string.IsNullOrEmpty(spell.ZoneShape) && !string.IsNullOrEmpty(parent.ZoneShape))
                spell.ZoneShape = parent.ZoneShape;
            if (string.IsNullOrEmpty(spell.ZoneAngle) && !string.IsNullOrEmpty(parent.ZoneAngle))
                spell.ZoneAngle = parent.ZoneAngle;
            if (string.IsNullOrEmpty(spell.ZoneBase) && !string.IsNullOrEmpty(parent.ZoneBase))
                spell.ZoneBase = parent.ZoneBase;
            if (string.IsNullOrEmpty(spell.ZoneFrontOffset) && !string.IsNullOrEmpty(parent.ZoneFrontOffset))
                spell.ZoneFrontOffset = parent.ZoneFrontOffset;
            if (string.IsNullOrEmpty(spell.ZoneRange) && !string.IsNullOrEmpty(parent.ZoneRange))
                spell.ZoneRange = parent.ZoneRange;
            
            if (string.IsNullOrEmpty(spell.TargetConditions) && !string.IsNullOrEmpty(parent.TargetConditions))
                spell.TargetConditions = parent.TargetConditions;
            
            if (string.IsNullOrEmpty(spell.SpellFlags) && !string.IsNullOrEmpty(parent.SpellFlags))
                spell.SpellFlags = parent.SpellFlags;
            
            if (string.IsNullOrEmpty(spell.WeaponTypes) && !string.IsNullOrEmpty(parent.WeaponTypes))
                spell.WeaponTypes = parent.WeaponTypes;
            
            if (string.IsNullOrEmpty(spell.VerbalIntent) && !string.IsNullOrEmpty(parent.VerbalIntent))
                spell.VerbalIntent = parent.VerbalIntent;
            
            if (spell.UseCosts == null && parent.UseCosts != null)
                spell.UseCosts = parent.UseCosts;
            
            if (string.IsNullOrEmpty(spell.Cooldown) && !string.IsNullOrEmpty(parent.Cooldown))
                spell.Cooldown = parent.Cooldown;
            
            // Copy any raw properties not present in child
            foreach (var (key, value) in parent.RawProperties)
            {
                if (!spell.RawProperties.ContainsKey(key))
                {
                    spell.RawProperties[key] = value;
                }
            }
        }
        
        private (string key, string value) ParseDataLine(string line)
        {
            // Format: data "Key" "Value"
            var match = Regex.Match(line, @"data\s+""([^""]+)""\s+""(.*)""", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return (match.Groups[1].Value, match.Groups[2].Value);
            }
            
            return (null, null);
        }
        
        private string ExtractQuotedValue(string line, string prefix)
        {
            // Extract value from: prefix "Value"
            var pattern = $@"{Regex.Escape(prefix)}\s+""([^""]+)""";
            var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }
        
        private void SetSpellProperty(BG3SpellData spell, string key, string value)
        {
            // Store in raw properties first
            spell.RawProperties[key] = value;
            
            // Map to strongly-typed properties
            switch (key)
            {
                case "DisplayName":
                    spell.DisplayName = value;
                    break;
                case "Description":
                    spell.Description = value;
                    break;
                case "Icon":
                    spell.Icon = value;
                    break;
                case "SpellType":
                    spell.SpellType = ParseSpellType(value);
                    break;
                case "Level":
                    if (int.TryParse(value, out var level))
                        spell.Level = level;
                    break;
                case "SpellSchool":
                    spell.SpellSchool = value;
                    break;
                case "SpellProperties":
                    spell.SpellProperties = value;
                    break;
                case "SpellRoll":
                    spell.SpellRoll = value;
                    break;
                case "SpellSuccess":
                    spell.SpellSuccess = value;
                    break;
                case "SpellFail":
                    spell.SpellFail = value;
                    break;
                case "SpellSaveDC":
                    spell.SpellSaveDC = value;
                    break;
                case "TargetRadius":
                    spell.TargetRadius = value;
                    break;
                case "AreaRadius":
                    spell.AreaRadius = value;
                    break;
                case "TargetConditions":
                    spell.TargetConditions = value;
                    break;
                case "RequirementConditions":
                    spell.RequirementConditions = value;
                    break;
                case "ExtraProjectileTargetConditions":
                    spell.ExtraProjectileTargetConditions = value;
                    break;
                case "TargetCeiling":
                    spell.TargetCeiling = value;
                    break;
                case "TargetFloor":
                    spell.TargetFloor = value;
                    break;
                case "ProjectileCount":
                    if (int.TryParse(value, out var count))
                        spell.ProjectileCount = count;
                    break;
                case "Trajectories":
                    spell.Trajectories = value;
                    break;
                case "UseCosts":
                    spell.UseCosts = ParseUseCosts(value);
                    break;
                case "DualWieldingUseCosts":
                    spell.DualWieldingUseCosts = ParseUseCosts(value);
                    break;
                case "MemoryCost":
                    if (int.TryParse(value, out var memoryCost))
                        spell.MemoryCost = memoryCost;
                    break;
                case "Cooldown":
                    spell.Cooldown = value;
                    break;
                case "TooltipDamageList":
                    spell.TooltipDamageList = value;
                    break;
                case "TooltipAttackSave":
                    spell.TooltipAttackSave = value;
                    break;
                case "TooltipStatusApply":
                    spell.TooltipStatusApply = value;
                    break;
                case "DescriptionParams":
                    spell.DescriptionParams = value;
                    break;
                case "SpellFlags":
                    spell.SpellFlags = value;
                    break;
                case "WeaponTypes":
                    spell.WeaponTypes = value;
                    break;
                case "VerbalIntent":
                    spell.VerbalIntent = value;
                    break;
                case "Sheathing":
                    spell.Sheathing = value;
                    break;
                case "SpellSoundMagnitude":
                    spell.SpellSoundMagnitude = value;
                    break;
                case "SpellStyleGroup":
                    spell.SpellStyleGroup = value;
                    break;
                case "SpellAnimation":
                    spell.SpellAnimation = value;
                    break;
                case "SpellAnimationIntentType":
                    spell.SpellAnimationIntentType = value;
                    break;
                case "HitAnimationType":
                    spell.HitAnimationType = value;
                    break;
                case "CastTextEvent":
                    spell.CastTextEvent = value;
                    break;
                case "AlternativeCastTextEvents":
                    spell.AlternativeCastTextEvents = value;
                    break;
                case "DamageType":
                    spell.DamageType = value;
                    break;
                case "Damage":
                    spell.Damage = value;
                    break;
                // Zone-specific shape fields
                case "Shape":
                    spell.ZoneShape = value;
                    break;
                case "Angle":
                    spell.ZoneAngle = value;
                    break;
                case "Base":
                    spell.ZoneBase = value;
                    break;
                case "FrontOffset":
                    spell.ZoneFrontOffset = value;
                    break;
                case "Range":
                    spell.ZoneRange = value;
                    break;
            }
        }
        
        private BG3SpellType ParseSpellType(string value)
        {
            if (string.IsNullOrEmpty(value))
                return BG3SpellType.Unknown;
            
            return value.ToLowerInvariant() switch
            {
                "target" => BG3SpellType.Target,
                "projectile" => BG3SpellType.Projectile,
                "shout" => BG3SpellType.Shout,
                "zone" => BG3SpellType.Zone,
                "rush" => BG3SpellType.Rush,
                "teleportation" => BG3SpellType.Teleportation,
                "throw" => BG3SpellType.Throw,
                "projectilestrike" => BG3SpellType.ProjectileStrike,
                _ => BG3SpellType.Unknown
            };
        }
        
        private SpellUseCost ParseUseCosts(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            
            var cost = new SpellUseCost { RawCost = value };
            
            // Parse semicolon-separated costs: "ActionPoint:1;BonusActionPoint:1"
            var parts = value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;
                
                var colonIndex = trimmed.IndexOf(':');
                if (colonIndex < 0)
                    continue;
                
                var resourceType = trimmed.Substring(0, colonIndex).Trim();
                var costValue = trimmed.Substring(colonIndex + 1).Trim();
                
                switch (resourceType.ToLowerInvariant())
                {
                    case "actionpoint":
                        if (int.TryParse(costValue, out var ap))
                            cost.ActionPoint = ap;
                        break;
                    case "bonusactionpoint":
                        if (int.TryParse(costValue, out var bap))
                            cost.BonusActionPoint = bap;
                        break;
                    case "reactionactionpoint":
                        if (int.TryParse(costValue, out var rap))
                            cost.ReactionActionPoint = rap;
                        break;
                    case "movement":
                        if (float.TryParse(costValue, out var movement))
                            cost.Movement = movement;
                        break;
                    case "spellslot":
                        // Format can be "SpellSlot:1:1" (level:count)
                        var slotParts = costValue.Split(':');
                        if (slotParts.Length >= 1 && int.TryParse(slotParts[0], out var slotLevel))
                        {
                            cost.SpellSlotLevel = slotLevel;
                            if (slotParts.Length >= 2 && int.TryParse(slotParts[1], out var slotCount))
                                cost.SpellSlotCount = slotCount;
                            else
                                cost.SpellSlotCount = 1;
                        }
                        break;
                    case "spellslotsgroup":
                        // Format: SpellSlotsGroup:GroupID:Count:Level → costValue = "GroupID:Count:Level"
                        var groupParts = costValue.Split(':');
                        if (groupParts.Length >= 3 && int.TryParse(groupParts[2], out var groupSlotLevel))
                        {
                            cost.SpellSlotLevel = groupSlotLevel;
                            if (int.TryParse(groupParts[1], out var groupSlotCount))
                                cost.SpellSlotCount = groupSlotCount;
                            else
                                cost.SpellSlotCount = 1;
                        }
                        break;
                    default:
                        // Custom resource (KiPoint, RageCharge, etc)
                        if (int.TryParse(costValue, out var customCost))
                            cost.CustomResources[resourceType] = customCost;
                        break;
                }
            }
            
            return cost;
        }
        
        /// <summary>
        /// Prints parsing statistics.
        /// </summary>
        public void PrintStatistics()
        {
            Console.WriteLine($"[BG3SpellParser] Parsed {_parsedSpells.Count} spells");
            Console.WriteLine($"[BG3SpellParser] Errors: {_errors.Count}, Warnings: {_warnings.Count}");
            
            if (_errors.Count > 0)
            {
                Console.WriteLine("\n=== Errors ===");
                foreach (var error in _errors)
                {
                    Console.Error.WriteLine($"  {error}");
                }
            }
            
            if (_warnings.Count > 0)
            {
                Console.WriteLine("\n=== Warnings ===");
                foreach (var warning in _warnings.Take(20)) // Limit to first 20
                {
                    Console.WriteLine($"  {warning}");
                }
                if (_warnings.Count > 20)
                {
                    Console.WriteLine($"  ... and {_warnings.Count - 20} more warnings");
                }
            }
            
            // Print spell type distribution
            var typeStats = _parsedSpells.Values
                .GroupBy(s => s.SpellType)
                .OrderByDescending(g => g.Count())
                .ToList();
            
            Console.WriteLine("\n=== Spell Type Distribution ===");
            foreach (var group in typeStats)
            {
                Console.WriteLine($"  {group.Key}: {group.Count()}");
            }
        }
    }
}
