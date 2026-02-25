using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using QDND.Data.Stats;

namespace QDND.Data.Parsers
{
    /// <summary>
    /// Parser for BG3's Character.txt, Weapon.txt, and Armor.txt stat definition files.
    /// Handles the shared "new entry" / "data" / "using" TXT format and resolves
    /// inheritance chains so each entry contains its fully-resolved property set.
    /// </summary>
    public class BG3StatsParser
    {
        // Pre-compiled regex for performance on large files
        private static readonly Regex DataLineRegex =
            new(@"^data\s+""([^""]+)""\s+""(.*)""$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex EntryNameRegex =
            new(@"^new\s+entry\s+""([^""]+)""$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex UsingRegex =
            new(@"^using\s+""([^""]+)""$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        /// <summary>All parsing errors encountered across all parse calls.</summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>All parsing warnings encountered across all parse calls.</summary>
        public IReadOnlyList<string> Warnings => _warnings;

        // ── Characters ───────────────────────────────────────────────────────

        /// <summary>
        /// Parses a BG3 Character.txt file and returns a dictionary of fully-resolved
        /// character stat blocks keyed by entry name.
        /// </summary>
        /// <param name="filePath">Absolute or relative path to Character.txt.</param>
        /// <returns>Dictionary mapping entry name → <see cref="BG3CharacterData"/>.</returns>
        public Dictionary<string, BG3CharacterData> ParseCharacters(string filePath)
        {
            var raw = ParseRawEntries(filePath, "Character");
            var dict = new Dictionary<string, BG3CharacterData>(raw.Count, StringComparer.Ordinal);

            foreach (var (name, entry) in raw)
            {
                var ch = new BG3CharacterData { Name = name, ParentId = entry.ParentId };
                foreach (var (k, v) in entry.Properties)
                    ch.RawProperties[k] = v;
                MapCharacterProperties(ch);
                dict[name] = ch;
            }

            ResolveInheritance(dict, (child, parent) => MergeCharacter(child, parent));
            return dict;
        }

        // ── Weapons ──────────────────────────────────────────────────────────

        /// <summary>
        /// Parses a BG3 Weapon.txt file and returns a dictionary of fully-resolved
        /// weapon definitions keyed by entry name.
        /// </summary>
        /// <param name="filePath">Absolute or relative path to Weapon.txt.</param>
        /// <returns>Dictionary mapping entry name → <see cref="BG3WeaponData"/>.</returns>
        public Dictionary<string, BG3WeaponData> ParseWeapons(string filePath)
        {
            var raw = ParseRawEntries(filePath, "Weapon");
            var dict = new Dictionary<string, BG3WeaponData>(raw.Count, StringComparer.Ordinal);

            foreach (var (name, entry) in raw)
            {
                var wpn = new BG3WeaponData { Name = name, ParentId = entry.ParentId };
                foreach (var (k, v) in entry.Properties)
                    wpn.RawProperties[k] = v;
                MapWeaponProperties(wpn);
                dict[name] = wpn;
            }

            ResolveInheritance(dict, (child, parent) => MergeWeapon(child, parent));
            return dict;
        }

        // ── Objects ───────────────────────────────────────────────────────

        /// <summary>
        /// Parses a BG3 Object.txt file and returns a dictionary of fully-resolved
        /// object definitions keyed by entry name.
        /// </summary>
        /// <param name="filePath">Absolute or relative path to Object.txt.</param>
        /// <returns>Dictionary mapping entry name → <see cref="BG3ObjectData"/>.</returns>
        public Dictionary<string, BG3ObjectData> ParseObjects(string filePath)
        {
            var raw = ParseRawEntries(filePath, "Object");
            var dict = new Dictionary<string, BG3ObjectData>(raw.Count, StringComparer.Ordinal);

            foreach (var (name, entry) in raw)
            {
                var obj = new BG3ObjectData { Name = name, ParentId = entry.ParentId };
                foreach (var (k, v) in entry.Properties)
                    obj.RawProperties[k] = v;
                MapObjectProperties(obj);
                dict[name] = obj;
            }

            ResolveInheritance(dict, (child, parent) => MergeObject(child, parent));
            return dict;
        }

        // ── Armor ────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses a BG3 Armor.txt file and returns a dictionary of fully-resolved
        /// armor definitions keyed by entry name.
        /// </summary>
        /// <param name="filePath">Absolute or relative path to Armor.txt.</param>
        /// <returns>Dictionary mapping entry name → <see cref="BG3ArmorData"/>.</returns>
        public Dictionary<string, BG3ArmorData> ParseArmors(string filePath)
        {
            var raw = ParseRawEntries(filePath, "Armor");
            var dict = new Dictionary<string, BG3ArmorData>(raw.Count, StringComparer.Ordinal);

            foreach (var (name, entry) in raw)
            {
                var arm = new BG3ArmorData { Name = name, ParentId = entry.ParentId };
                foreach (var (k, v) in entry.Properties)
                    arm.RawProperties[k] = v;
                MapArmorProperties(arm);
                dict[name] = arm;
            }

            ResolveInheritance(dict, (child, parent) => MergeArmor(child, parent));
            return dict;
        }

        /// <summary>
        /// Resets accumulated errors and warnings.
        /// </summary>
        public void Clear()
        {
            _errors.Clear();
            _warnings.Clear();
        }

        /// <summary>
        /// Prints a summary of parsing statistics to the console.
        /// </summary>
        public void PrintStatistics()
        {
            Console.WriteLine($"[BG3StatsParser] Errors: {_errors.Count}, Warnings: {_warnings.Count}");
            if (_errors.Count > 0)
            {
                Console.WriteLine("=== Errors ===");
                foreach (var e in _errors)
                    Console.Error.WriteLine($"  {e}");
            }
            if (_warnings.Count > 0)
            {
                Console.WriteLine("=== Warnings (first 20) ===");
                foreach (var w in _warnings.Take(20))
                    Console.WriteLine($"  {w}");
                if (_warnings.Count > 20)
                    Console.WriteLine($"  ... and {_warnings.Count - 20} more");
            }
        }

        // =====================================================================
        //  Generic TXT parser
        // =====================================================================

        /// <summary>
        /// Intermediate container for one raw "new entry" block.
        /// </summary>
        private sealed class RawEntry
        {
            public string ParentId;
            public readonly Dictionary<string, string> Properties = new(StringComparer.Ordinal);
        }

        /// <summary>
        /// Reads a BG3 stats TXT file and returns a dictionary of raw entries
        /// (before inheritance resolution), filtered to the given <paramref name="expectedType"/>.
        /// </summary>
        private Dictionary<string, RawEntry> ParseRawEntries(string filePath, string expectedType)
        {
            var result = new Dictionary<string, RawEntry>(StringComparer.Ordinal);

            if (!File.Exists(filePath))
            {
                _errors.Add($"File not found: {filePath}");
                return result;
            }

            try
            {
                var lines = File.ReadAllLines(filePath);
                string currentName = null;
                RawEntry currentEntry = null;
                var lineNumber = 0;

                foreach (var rawLine in lines)
                {
                    lineNumber++;
                    var line = rawLine.Trim();

                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                        continue;

                    // ── new entry ─────────────────────────────────
                    if (line.StartsWith("new entry", StringComparison.OrdinalIgnoreCase))
                    {
                        // Commit previous
                        if (currentName != null && currentEntry != null)
                            result[currentName] = currentEntry;

                        var m = EntryNameRegex.Match(line);
                        if (!m.Success)
                        {
                            _errors.Add($"{filePath}:{lineNumber} - bad entry line: {line}");
                            currentName = null;
                            currentEntry = null;
                            continue;
                        }

                        currentName = m.Groups[1].Value;
                        currentEntry = new RawEntry();
                        continue;
                    }

                    // ── type ──────────────────────────────────────
                    if (line.StartsWith("type", StringComparison.OrdinalIgnoreCase))
                    {
                        // Optionally validate type matches expectedType; just skip for now.
                        continue;
                    }

                    // ── using ─────────────────────────────────────
                    if (line.StartsWith("using", StringComparison.OrdinalIgnoreCase))
                    {
                        if (currentEntry == null)
                        {
                            _warnings.Add($"{filePath}:{lineNumber} - 'using' outside entry");
                            continue;
                        }

                        var m = UsingRegex.Match(line);
                        if (m.Success)
                            currentEntry.ParentId = m.Groups[1].Value;
                        continue;
                    }

                    // ── data ──────────────────────────────────────
                    if (line.StartsWith("data", StringComparison.OrdinalIgnoreCase))
                    {
                        if (currentEntry == null)
                        {
                            _warnings.Add($"{filePath}:{lineNumber} - 'data' outside entry");
                            continue;
                        }

                        var m = DataLineRegex.Match(line);
                        if (m.Success)
                        {
                            currentEntry.Properties[m.Groups[1].Value] = m.Groups[2].Value;
                        }
                        else
                        {
                            _warnings.Add($"{filePath}:{lineNumber} - unparseable data: {line}");
                        }
                    }
                }

                // Commit last entry
                if (currentName != null && currentEntry != null)
                    result[currentName] = currentEntry;
            }
            catch (Exception ex)
            {
                _errors.Add($"Failed to parse {filePath}: {ex.Message}");
            }

            return result;
        }

        // =====================================================================
        //  Generic inheritance resolution
        // =====================================================================

        /// <summary>
        /// Interface for types that carry a <c>ParentId</c> and <c>RawProperties</c>.
        /// </summary>
        private interface IHasParent
        {
            string ParentId { get; }
            Dictionary<string, string> RawProperties { get; }
        }

        /// <summary>
        /// Resolves "using" inheritance for every entry in <paramref name="dict"/>.
        /// The <paramref name="merge"/> delegate copies missing properties from parent to child.
        /// </summary>
        private void ResolveInheritance<T>(
            Dictionary<string, T> dict,
            Action<T, T> merge) where T : class
        {
            // Track resolution state to detect cycles
            var resolved = new HashSet<string>(StringComparer.Ordinal);
            var resolving = new HashSet<string>(StringComparer.Ordinal);

            foreach (var name in dict.Keys.ToList())
            {
                Resolve(name, dict, merge, resolved, resolving);
            }
        }

        private void Resolve<T>(
            string name,
            Dictionary<string, T> dict,
            Action<T, T> merge,
            HashSet<string> resolved,
            HashSet<string> resolving) where T : class
        {
            if (resolved.Contains(name))
                return;

            if (resolving.Contains(name))
            {
                _warnings.Add($"Inheritance cycle detected at '{name}'");
                return;
            }

            resolving.Add(name);

            if (!dict.TryGetValue(name, out var entry))
                return;

            var parentId = GetParentId(entry);
            if (!string.IsNullOrEmpty(parentId))
            {
                if (dict.TryGetValue(parentId, out var parent))
                {
                    // Ensure parent is resolved first
                    Resolve(parentId, dict, merge, resolved, resolving);
                    merge(entry, parent);
                }
                else
                {
                    _warnings.Add($"Entry '{name}' references unknown parent '{parentId}'");
                }
            }

            resolving.Remove(name);
            resolved.Add(name);
        }

        /// <summary>
        /// Extracts ParentId from any of our data model types via duck-typing.
        /// </summary>
        private static string GetParentId<T>(T entry)
        {
            return entry switch
            {
                BG3CharacterData c => c.ParentId,
                BG3WeaponData w => w.ParentId,
                BG3ArmorData a => a.ParentId,
                BG3ObjectData o => o.ParentId,
                _ => null
            };
        }

        // =====================================================================
        //  Character property mapping & merging
        // =====================================================================

        private static void MapCharacterProperties(BG3CharacterData ch)
        {
            foreach (var (key, value) in ch.RawProperties)
            {
                switch (key)
                {
                    case "Level": ch.Level = ParseInt(value); break;
                    case "Strength": ch.Strength = ParseInt(value); break;
                    case "Dexterity": ch.Dexterity = ParseInt(value); break;
                    case "Constitution": ch.Constitution = ParseInt(value); break;
                    case "Intelligence": ch.Intelligence = ParseInt(value); break;
                    case "Wisdom": ch.Wisdom = ParseInt(value); break;
                    case "Charisma": ch.Charisma = ParseInt(value); break;
                    case "Armor": ch.Armor = ParseInt(value); break;
                    case "ArmorType": ch.ArmorType = value; break;
                    case "Vitality": ch.Vitality = ParseInt(value); break;
                    case "Initiative": ch.Initiative = ParseInt(value); break;
                    case "ActionResources": ch.ActionResources = value; break;
                    case "Passives": ch.Passives = value; break;
                    case "DefaultBoosts": ch.DefaultBoosts = value; break;
                    case "AcidResistance": ch.AcidResistance = value; break;
                    case "BludgeoningResistance": ch.BludgeoningResistance = value; break;
                    case "ColdResistance": ch.ColdResistance = value; break;
                    case "FireResistance": ch.FireResistance = value; break;
                    case "ForceResistance": ch.ForceResistance = value; break;
                    case "LightningResistance": ch.LightningResistance = value; break;
                    case "NecroticResistance": ch.NecroticResistance = value; break;
                    case "PiercingResistance": ch.PiercingResistance = value; break;
                    case "PoisonResistance": ch.PoisonResistance = value; break;
                    case "PsychicResistance": ch.PsychicResistance = value; break;
                    case "RadiantResistance": ch.RadiantResistance = value; break;
                    case "SlashingResistance": ch.SlashingResistance = value; break;
                    case "ThunderResistance": ch.ThunderResistance = value; break;
                    case "Proficiency Group": ch.ProficiencyGroup = value; break;
                    case "ProficiencyBonus": ch.ProficiencyBonus = ParseInt(value); break;
                    case "SpellCastingAbility": ch.SpellCastingAbility = value; break;
                    case "UnarmedAttackAbility": ch.UnarmedAttackAbility = value; break;
                    case "UnarmedRangedAttackAbility": ch.UnarmedRangedAttackAbility = value; break;
                    case "Class": ch.Class = value; break;
                    case "Progressions": ch.Progressions = value; break;
                    case "Progression Type": ch.ProgressionType = value; break;
                    case "DifficultyStatuses": ch.DifficultyStatuses = value; break;
                    case "PersonalStatusImmunities": ch.PersonalStatusImmunities = value; break;
                    case "Sight": ch.Sight = ParseInt(value); break;
                    case "Hearing": ch.Hearing = ParseInt(value); break;
                    case "FOV": ch.FOV = ParseInt(value); break;
                    case "DarkvisionRange": ch.DarkvisionRange = value; break;
                    case "MinimumDetectionRange": ch.MinimumDetectionRange = value; break;
                    case "Weight": ch.Weight = ParseFloat(value); break;
                    case "XPReward": ch.XPReward = value; break;
                    case "Flags": ch.Flags = value; break;
                    case "StepsType": ch.StepsType = value; break;
                    case "PathInfluence": ch.PathInfluence = value; break;
                    case "ProficiencyBonusScaling": ch.ProficiencyBonusScaling = value; break;
                }
            }
        }

        /// <summary>
        /// Copies any properties from <paramref name="parent"/> that are not already
        /// explicitly set on <paramref name="child"/>.
        /// </summary>
        private static void MergeCharacter(BG3CharacterData child, BG3CharacterData parent)
        {
            // Raw properties: parent supplies defaults, child overrides
            foreach (var (k, v) in parent.RawProperties)
            {
                if (!child.RawProperties.ContainsKey(k))
                    child.RawProperties[k] = v;
            }

            // Re-map from the now-complete raw properties
            MapCharacterProperties(child);
        }

        // =====================================================================
        //  Weapon property mapping & merging
        // =====================================================================

        private static void MapWeaponProperties(BG3WeaponData wpn)
        {
            foreach (var (key, value) in wpn.RawProperties)
            {
                switch (key)
                {
                    case "Damage": wpn.Damage = value; break;
                    case "Damage Type": wpn.DamageType = value; break;
                    case "VersatileDamage": wpn.VersatileDamage = value; break;
                    case "Damage Range": wpn.DamageRange = ParseInt(value); break;
                    case "Weapon Properties": wpn.WeaponProperties = value; break;
                    case "Weapon Group": wpn.WeaponGroup = value; break;
                    case "Proficiency Group": wpn.ProficiencyGroup = value; break;
                    case "BoostsOnEquipMainHand": wpn.BoostsOnEquipMainHand = value; break;
                    case "BoostsOnEquipOffHand": wpn.BoostsOnEquipOffHand = value; break;
                    case "Boosts": wpn.Boosts = value; break;
                    case "DefaultBoosts": wpn.DefaultBoosts = value; break;
                    case "PassivesMainHand": wpn.PassivesMainHand = value; break;
                    case "PassivesOffHand": wpn.PassivesOffHand = value; break;
                    case "PassivesOnEquip": wpn.PassivesOnEquip = value; break;
                    case "StatusOnEquip": wpn.StatusOnEquip = value; break;
                    case "Rarity": wpn.Rarity = value; break;
                    case "Slot": wpn.Slot = value; break;
                    case "Weight": wpn.Weight = ParseFloat(value); break;
                    case "WeaponRange": wpn.WeaponRange = ParseInt(value); break;
                    case "Level": wpn.Level = ParseInt(value); break;
                    case "UseCosts": wpn.UseCosts = value; break;
                    case "ValueLevel": wpn.ValueLevel = ParseInt(value); break;
                    case "ValueScale": wpn.ValueScale = ParseFloat(value); break;
                    case "ValueRounding": wpn.ValueRounding = ParseInt(value); break;
                    case "ValueOverride": wpn.ValueOverride = ParseInt(value); break;
                    case "ValueUUID": wpn.ValueUUID = value; break;
                    case "PersonalStatusImmunities": wpn.PersonalStatusImmunities = value; break;
                    case "ItemGroup": wpn.ItemGroup = value; break;
                    case "InventoryTab": wpn.InventoryTab = value; break;
                    case "ItemColor": wpn.ItemColor = value; break;
                    case "WeaponFunctors": wpn.WeaponFunctors = value; break;
                    case "Spells": wpn.Spells = value; break;
                    case "Charges": wpn.Charges = ParseInt(value); break;
                    case "MaxCharges": wpn.MaxCharges = ParseInt(value); break;
                    case "NeedsIdentification": wpn.NeedsIdentification = value; break;
                    case "Unique": wpn.Unique = ParseInt(value); break;
                    case "ExtraProperties": wpn.ExtraProperties = value; break;
                    case "Flags": wpn.Flags = value; break;
                }
            }
        }

        private static void MergeWeapon(BG3WeaponData child, BG3WeaponData parent)
        {
            foreach (var (k, v) in parent.RawProperties)
            {
                if (!child.RawProperties.ContainsKey(k))
                    child.RawProperties[k] = v;
            }
            MapWeaponProperties(child);
        }

        // =====================================================================
        //  Armor property mapping & merging
        // =====================================================================

        private static void MapArmorProperties(BG3ArmorData arm)
        {
            foreach (var (key, value) in arm.RawProperties)
            {
                switch (key)
                {
                    case "ArmorClass": arm.ArmorClass = ParseInt(value); break;
                    case "ArmorType": arm.ArmorType = value; break;
                    case "Shield": arm.Shield = value; break;
                    case "Armor Class Ability": arm.ArmorClassAbility = value; break;
                    case "Ability Modifier Cap": arm.AbilityModifierCap = ParseInt(value); break;
                    case "Proficiency Group": arm.ProficiencyGroup = value; break;
                    case "Boosts": arm.Boosts = value; break;
                    case "DefaultBoosts": arm.DefaultBoosts = value; break;
                    case "StatusOnEquip": arm.StatusOnEquip = value; break;
                    case "PassivesOnEquip": arm.PassivesOnEquip = value; break;
                    case "Spells": arm.Spells = value; break;
                    case "Rarity": arm.Rarity = value; break;
                    case "Slot": arm.Slot = value; break;
                    case "Weight": arm.Weight = ParseFloat(value); break;
                    case "Level": arm.Level = ParseInt(value); break;
                    case "UseCosts": arm.UseCosts = value; break;
                    case "ValueLevel": arm.ValueLevel = ParseInt(value); break;
                    case "ValueScale": arm.ValueScale = ParseFloat(value); break;
                    case "ValueRounding": arm.ValueRounding = ParseInt(value); break;
                    case "ValueOverride": arm.ValueOverride = ParseInt(value); break;
                    case "ValueUUID": arm.ValueUUID = value; break;
                    case "PersonalStatusImmunities": arm.PersonalStatusImmunities = value; break;
                    case "MinAmount": arm.MinAmount = ParseInt(value); break;
                    case "MaxAmount": arm.MaxAmount = ParseInt(value); break;
                    case "Priority": arm.Priority = ParseInt(value); break;
                    case "MinLevel": arm.MinLevel = ParseInt(value); break;
                    case "MaxLevel": arm.MaxLevel = ParseInt(value); break;
                    case "InventoryTab": arm.InventoryTab = value; break;
                    case "ComboCategory": arm.ComboCategory = value; break;
                    case "Charges": arm.Charges = ParseInt(value); break;
                    case "Durability": arm.Durability = ParseInt(value); break;
                    case "NeedsIdentification": arm.NeedsIdentification = value; break;
                    case "Unique": arm.Unique = ParseInt(value); break;
                    case "Flags": arm.Flags = value; break;
                    case "ExtraProperties": arm.ExtraProperties = value; break;
                }
            }
        }

        private static void MergeArmor(BG3ArmorData child, BG3ArmorData parent)
        {
            foreach (var (k, v) in parent.RawProperties)
            {
                if (!child.RawProperties.ContainsKey(k))
                    child.RawProperties[k] = v;
            }
            MapArmorProperties(child);
        }

        // =====================================================================
        //  Object property mapping & merging
        // =====================================================================

        private static void MapObjectProperties(BG3ObjectData obj)
        {
            foreach (var (key, value) in obj.RawProperties)
            {
                switch (key)
                {
                    case "Vitality": obj.Vitality = ParseInt(value); break;
                    case "Weight": obj.Weight = ParseFloat(value); break;
                    case "ValueLevel": obj.ValueLevel = ParseInt(value); break;
                    case "ValueOverride": obj.ValueOverride = ParseInt(value); break;
                    case "SupplyValue": obj.SupplyValue = ParseInt(value); break;
                    case "Rarity": obj.Rarity = value; break;
                    case "ObjectCategory": obj.ObjectCategory = value; break;
                    case "Flags": obj.Flags = value; break;
                    case "InventoryTab": obj.InventoryTab = value; break;
                    case "RootTemplate": obj.RootTemplate = value; break;
                    case "Slot": obj.Slot = value; break;
                    case "PhysicalResistance": obj.PhysicalResistance = value; break;
                    case "AcidResistance": obj.AcidResistance = value; break;
                    case "BludgeoningResistance": obj.BludgeoningResistance = value; break;
                    case "ColdResistance": obj.ColdResistance = value; break;
                    case "FireResistance": obj.FireResistance = value; break;
                    case "ForceResistance": obj.ForceResistance = value; break;
                    case "LightningResistance": obj.LightningResistance = value; break;
                    case "NecroticResistance": obj.NecroticResistance = value; break;
                    case "PiercingResistance": obj.PiercingResistance = value; break;
                    case "PoisonResistance": obj.PoisonResistance = value; break;
                    case "PsychicResistance": obj.PsychicResistance = value; break;
                    case "RadiantResistance": obj.RadiantResistance = value; break;
                    case "SlashingResistance": obj.SlashingResistance = value; break;
                    case "ThunderResistance": obj.ThunderResistance = value; break;
                    case "GameSize": obj.GameSize = value; break;
                    case "Level": obj.Level = ParseInt(value); break;
                    case "UseCosts": obj.UseCosts = value; break;
                    case "Spells": obj.Spells = value; break;
                    case "Boosts": obj.Boosts = value; break;
                    case "PassivesOnEquip": obj.PassivesOnEquip = value; break;
                    case "StatusOnEquip": obj.StatusOnEquip = value; break;
                    case "ExtraProperties": obj.ExtraProperties = value; break;
                    case "NeedsIdentification": obj.NeedsIdentification = value; break;
                    case "Unique": obj.Unique = ParseInt(value); break;
                    case "MinAmount": obj.MinAmount = ParseInt(value); break;
                    case "MaxAmount": obj.MaxAmount = ParseInt(value); break;
                    case "Priority": obj.Priority = ParseInt(value); break;
                    case "MinLevel": obj.MinLevel = ParseInt(value); break;
                    case "MaxLevel": obj.MaxLevel = ParseInt(value); break;
                    case "PersonalStatusImmunities": obj.PersonalStatusImmunities = value; break;
                    case "DefaultBoosts": obj.DefaultBoosts = value; break;
                    case "Armor":
                        if (int.TryParse(value, out var armor))
                            obj.Armor = armor;
                        break;
                    case "ItemUseType": obj.ItemUseType = value; break;
                    case "UseConditions": obj.UseConditions = value; break;
                }
            }
        }

        private static void MergeObject(BG3ObjectData child, BG3ObjectData parent)
        {
            foreach (var (k, v) in parent.RawProperties)
            {
                if (!child.RawProperties.ContainsKey(k))
                    child.RawProperties[k] = v;
            }
            MapObjectProperties(child);
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static int ParseInt(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0;
        }

        private static float ParseFloat(string value)
        {
            return float.TryParse(value, NumberStyles.Float | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var f) ? f : 0f;
        }
    }
}
