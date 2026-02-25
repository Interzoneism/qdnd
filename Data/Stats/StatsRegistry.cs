using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QDND.Data.Parsers;

namespace QDND.Data.Stats
{
    /// <summary>
    /// Central registry for all loaded BG3 stat definitions (characters, weapons, armor).
    /// Loads and indexes data from BG3_Data/Stats/ directory and provides typed lookups
    /// and query methods for each category.
    /// </summary>
    public class StatsRegistry
    {
        private readonly Dictionary<string, BG3CharacterData> _characters = new(StringComparer.Ordinal);
        private readonly Dictionary<string, BG3WeaponData> _weapons = new(StringComparer.Ordinal);
        private readonly Dictionary<string, BG3ArmorData> _armors = new(StringComparer.Ordinal);
        private readonly Dictionary<string, BG3ObjectData> _objects = new(StringComparer.Ordinal);

        // Secondary indexes
        private readonly Dictionary<string, List<string>> _charactersByClass = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _weaponsByGroup = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _armorsByType = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _armorsByProficiency = new(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        /// <summary>Total number of registered characters.</summary>
        public int CharacterCount => _characters.Count;

        /// <summary>Total number of registered weapons.</summary>
        public int WeaponCount => _weapons.Count;

        /// <summary>Total number of registered armors.</summary>
        public int ArmorCount => _armors.Count;

        /// <summary>Total number of registered objects.</summary>
        public int ObjectCount => _objects.Count;

        /// <summary>Total entries across all categories.</summary>
        public int TotalCount => _characters.Count + _weapons.Count + _armors.Count + _objects.Count;

        /// <summary>Errors encountered during loading or registration.</summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>Warnings encountered during loading or registration.</summary>
        public IReadOnlyList<string> Warnings => _warnings;

        // =====================================================================
        //  Loading
        // =====================================================================

        /// <summary>
        /// Loads all stat files from the given directory.
        /// Expects Character.txt, Weapon.txt, and Armor.txt inside <paramref name="statsDirectory"/>.
        /// </summary>
        /// <param name="statsDirectory">
        /// Path to the BG3_Data/Stats/ directory (or equivalent).
        /// </param>
        public void LoadFromDirectory(string statsDirectory)
        {
            if (!Directory.Exists(statsDirectory))
            {
                _errors.Add($"Stats directory not found: {statsDirectory}");
                return;
            }

            var parser = new BG3StatsParser();

            // ── Characters ───────────────────────────────────
            var charFile = Path.Combine(statsDirectory, "Character.txt");
            if (File.Exists(charFile))
            {
                var chars = parser.ParseCharacters(charFile);
                foreach (var (name, data) in chars)
                    RegisterCharacter(data);
                Console.WriteLine($"[StatsRegistry] Loaded {chars.Count} characters from Character.txt");
            }
            else
            {
                _warnings.Add($"Character.txt not found in {statsDirectory}");
            }

            // ── Weapons ──────────────────────────────────────
            var wpnFile = Path.Combine(statsDirectory, "Weapon.txt");
            if (File.Exists(wpnFile))
            {
                var wpns = parser.ParseWeapons(wpnFile);
                foreach (var (name, data) in wpns)
                    RegisterWeapon(data);
                Console.WriteLine($"[StatsRegistry] Loaded {wpns.Count} weapons from Weapon.txt");
            }
            else
            {
                _warnings.Add($"Weapon.txt not found in {statsDirectory}");
            }

            // ── Armor ────────────────────────────────────────
            var armFile = Path.Combine(statsDirectory, "Armor.txt");
            if (File.Exists(armFile))
            {
                var arms = parser.ParseArmors(armFile);
                foreach (var (name, data) in arms)
                    RegisterArmor(data);
                Console.WriteLine($"[StatsRegistry] Loaded {arms.Count} armors from Armor.txt");
            }
            else
            {
                _warnings.Add($"Armor.txt not found in {statsDirectory}");
            }

            // ── Objects ──────────────────────────────────────
            var objFile = Path.Combine(statsDirectory, "Object.txt");
            if (File.Exists(objFile))
            {
                var objs = parser.ParseObjects(objFile);
                foreach (var (name, data) in objs)
                    RegisterObject(data);
                Console.WriteLine($"[StatsRegistry] Loaded {objs.Count} objects from Object.txt");
            }
            else
            {
                _warnings.Add($"Object.txt not found in {statsDirectory}");
            }

            // Propagate parser diagnostics
            _errors.AddRange(parser.Errors);
            _warnings.AddRange(parser.Warnings);
        }

        // =====================================================================
        //  Registration
        // =====================================================================

        /// <summary>
        /// Registers a character stat block, building secondary indexes.
        /// </summary>
        /// <param name="character">Character data to register.</param>
        /// <param name="overwrite">If true, replaces an existing entry with the same name.</param>
        /// <returns>True if registration succeeded.</returns>
        public bool RegisterCharacter(BG3CharacterData character, bool overwrite = false)
        {
            if (character == null || string.IsNullOrEmpty(character.Name))
            {
                _errors.Add("Cannot register null or unnamed character");
                return false;
            }

            if (_characters.ContainsKey(character.Name) && !overwrite)
                return false;

            _characters[character.Name] = character;

            // Index by class
            if (!string.IsNullOrEmpty(character.Class))
            {
                if (!_charactersByClass.TryGetValue(character.Class, out var list))
                {
                    list = new List<string>();
                    _charactersByClass[character.Class] = list;
                }
                if (!list.Contains(character.Name))
                    list.Add(character.Name);
            }

            return true;
        }

        /// <summary>
        /// Registers a weapon definition, building secondary indexes.
        /// </summary>
        /// <param name="weapon">Weapon data to register.</param>
        /// <param name="overwrite">If true, replaces an existing entry with the same name.</param>
        /// <returns>True if registration succeeded.</returns>
        public bool RegisterWeapon(BG3WeaponData weapon, bool overwrite = false)
        {
            if (weapon == null || string.IsNullOrEmpty(weapon.Name))
            {
                _errors.Add("Cannot register null or unnamed weapon");
                return false;
            }

            if (_weapons.ContainsKey(weapon.Name) && !overwrite)
                return false;

            _weapons[weapon.Name] = weapon;

            // Index by weapon group
            if (!string.IsNullOrEmpty(weapon.WeaponGroup))
            {
                if (!_weaponsByGroup.TryGetValue(weapon.WeaponGroup, out var list))
                {
                    list = new List<string>();
                    _weaponsByGroup[weapon.WeaponGroup] = list;
                }
                if (!list.Contains(weapon.Name))
                    list.Add(weapon.Name);
            }

            return true;
        }

        /// <summary>
        /// Registers an armor definition, building secondary indexes.
        /// </summary>
        /// <param name="armor">Armor data to register.</param>
        /// <param name="overwrite">If true, replaces an existing entry with the same name.</param>
        /// <returns>True if registration succeeded.</returns>
        public bool RegisterArmor(BG3ArmorData armor, bool overwrite = false)
        {
            if (armor == null || string.IsNullOrEmpty(armor.Name))
            {
                _errors.Add("Cannot register null or unnamed armor");
                return false;
            }

            if (_armors.ContainsKey(armor.Name) && !overwrite)
                return false;

            _armors[armor.Name] = armor;

            // Index by armor type
            if (!string.IsNullOrEmpty(armor.ArmorType))
            {
                if (!_armorsByType.TryGetValue(armor.ArmorType, out var list))
                {
                    list = new List<string>();
                    _armorsByType[armor.ArmorType] = list;
                }
                if (!list.Contains(armor.Name))
                    list.Add(armor.Name);
            }

            // Index by proficiency group
            if (!string.IsNullOrEmpty(armor.ProficiencyGroup))
            {
                if (!_armorsByProficiency.TryGetValue(armor.ProficiencyGroup, out var list))
                {
                    list = new List<string>();
                    _armorsByProficiency[armor.ProficiencyGroup] = list;
                }
                if (!list.Contains(armor.Name))
                    list.Add(armor.Name);
            }

            return true;
        }

        /// <summary>
        /// Registers an object definition.
        /// </summary>
        /// <param name="obj">Object data to register.</param>
        /// <param name="overwrite">If true, replaces an existing entry with the same name.</param>
        /// <returns>True if registration succeeded.</returns>
        public bool RegisterObject(BG3ObjectData obj, bool overwrite = false)
        {
            if (obj == null || string.IsNullOrEmpty(obj.Name))
            {
                _errors.Add("Cannot register null or unnamed object");
                return false;
            }

            if (_objects.ContainsKey(obj.Name) && !overwrite)
                return false;

            _objects[obj.Name] = obj;
            return true;
        }

        // =====================================================================
        //  Lookups
        // =====================================================================

        /// <summary>
        /// Gets a character stat block by exact entry name.
        /// Returns null if not found.
        /// </summary>
        public BG3CharacterData GetCharacter(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            _characters.TryGetValue(name, out var result);
            return result;
        }

        /// <summary>
        /// Gets a weapon definition by exact entry name.
        /// Returns null if not found.
        /// </summary>
        public BG3WeaponData GetWeapon(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            _weapons.TryGetValue(name, out var result);
            return result;
        }

        /// <summary>
        /// Gets an armor definition by exact entry name.
        /// Returns null if not found.
        /// </summary>
        public BG3ArmorData GetArmor(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            _armors.TryGetValue(name, out var result);
            return result;
        }

        /// <summary>
        /// Gets an object definition by exact entry name.
        /// Returns null if not found.
        /// </summary>
        public BG3ObjectData GetObject(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            _objects.TryGetValue(name, out var result);
            return result;
        }

        // =====================================================================
        //  Queries
        // =====================================================================

        /// <summary>
        /// Returns all registered character stat blocks.
        /// </summary>
        public IReadOnlyDictionary<string, BG3CharacterData> GetAllCharacters() => _characters;

        /// <summary>
        /// Returns all registered weapon definitions.
        /// </summary>
        public IReadOnlyDictionary<string, BG3WeaponData> GetAllWeapons() => _weapons;

        /// <summary>
        /// Returns all registered armor definitions.
        /// </summary>
        public IReadOnlyDictionary<string, BG3ArmorData> GetAllArmors() => _armors;

        /// <summary>
        /// Returns all registered object definitions.
        /// </summary>
        public IReadOnlyDictionary<string, BG3ObjectData> GetAllObjects() => _objects;

        /// <summary>
        /// Gets all character entry names that have the specified class.
        /// </summary>
        /// <param name="className">Class name (e.g., "Fighter", "Wizard").</param>
        /// <returns>List of character entry names, or empty list if none found.</returns>
        public IReadOnlyList<string> GetCharactersByClass(string className)
        {
            if (string.IsNullOrEmpty(className)) return Array.Empty<string>();
            return _charactersByClass.TryGetValue(className, out var list)
                ? list
                : (IReadOnlyList<string>)Array.Empty<string>();
        }

        /// <summary>
        /// Gets all weapon entry names in the specified weapon group.
        /// </summary>
        /// <param name="group">Weapon group (e.g., "MartialMeleeWeapon", "SimpleRangedWeapon").</param>
        /// <returns>List of weapon entry names, or empty list if none found.</returns>
        public IReadOnlyList<string> GetWeaponsByGroup(string group)
        {
            if (string.IsNullOrEmpty(group)) return Array.Empty<string>();
            return _weaponsByGroup.TryGetValue(group, out var list)
                ? list
                : (IReadOnlyList<string>)Array.Empty<string>();
        }

        /// <summary>
        /// Gets all armor entry names of the specified armor type.
        /// </summary>
        /// <param name="armorType">Armor type (e.g., "Leather", "Plate", "ChainMail").</param>
        /// <returns>List of armor entry names, or empty list if none found.</returns>
        public IReadOnlyList<string> GetArmorsByType(string armorType)
        {
            if (string.IsNullOrEmpty(armorType)) return Array.Empty<string>();
            return _armorsByType.TryGetValue(armorType, out var list)
                ? list
                : (IReadOnlyList<string>)Array.Empty<string>();
        }

        /// <summary>
        /// Gets all armor entry names requiring the specified proficiency.
        /// </summary>
        /// <param name="proficiency">Proficiency group (e.g., "LightArmor", "HeavyArmor", "Shields").</param>
        /// <returns>List of armor entry names, or empty list if none found.</returns>
        public IReadOnlyList<string> GetArmorsByProficiency(string proficiency)
        {
            if (string.IsNullOrEmpty(proficiency)) return Array.Empty<string>();
            return _armorsByProficiency.TryGetValue(proficiency, out var list)
                ? list
                : (IReadOnlyList<string>)Array.Empty<string>();
        }

        /// <summary>
        /// Finds all weapons whose <see cref="BG3WeaponData.DamageType"/> matches <paramref name="damageType"/>.
        /// </summary>
        public IReadOnlyList<BG3WeaponData> FindWeaponsByDamageType(string damageType)
        {
            if (string.IsNullOrEmpty(damageType)) return Array.Empty<BG3WeaponData>();
            return _weapons.Values
                .Where(w => string.Equals(w.DamageType, damageType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Finds all weapons that contain the specified property flag
        /// (e.g., "Finesse", "Heavy", "Versatile", "Thrown").
        /// </summary>
        public IReadOnlyList<BG3WeaponData> FindWeaponsByProperty(string property)
        {
            if (string.IsNullOrEmpty(property)) return Array.Empty<BG3WeaponData>();
            return _weapons.Values
                .Where(w => !string.IsNullOrEmpty(w.WeaponProperties)
                    && w.WeaponProperties.Split(';')
                        .Any(p => string.Equals(p.Trim(), property, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        /// <summary>
        /// Finds all characters whose entry name contains the given substring (case-insensitive).
        /// </summary>
        public IReadOnlyList<BG3CharacterData> SearchCharacters(string nameSubstring)
        {
            if (string.IsNullOrEmpty(nameSubstring)) return Array.Empty<BG3CharacterData>();
            return _characters.Values
                .Where(c => c.Name.IndexOf(nameSubstring, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        /// <summary>
        /// Finds all characters that have a resistance to the specified damage type.
        /// </summary>
        /// <param name="damageType">Damage type name (e.g., "Fire", "Poison", "Slashing").</param>
        /// <returns>Characters that have any non-empty resistance value for that type.</returns>
        public IReadOnlyList<BG3CharacterData> FindCharactersWithResistance(string damageType)
        {
            if (string.IsNullOrEmpty(damageType)) return Array.Empty<BG3CharacterData>();

            var key = damageType + "Resistance";
            return _characters.Values
                .Where(c => c.RawProperties.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                .ToList();
        }

        // =====================================================================
        //  Diagnostics
        // =====================================================================

        /// <summary>
        /// Prints a summary of all loaded stats to the console.
        /// </summary>
        public void PrintSummary()
        {
            Console.WriteLine($"[StatsRegistry] Characters: {_characters.Count}, Weapons: {_weapons.Count}, Armors: {_armors.Count}, Objects: {_objects.Count}");

            if (_charactersByClass.Count > 0)
            {
                Console.WriteLine("  Character classes:");
                foreach (var (cls, list) in _charactersByClass.OrderByDescending(kv => kv.Value.Count))
                    Console.WriteLine($"    {cls}: {list.Count}");
            }

            if (_weaponsByGroup.Count > 0)
            {
                Console.WriteLine("  Weapon groups:");
                foreach (var (grp, list) in _weaponsByGroup.OrderByDescending(kv => kv.Value.Count))
                    Console.WriteLine($"    {grp}: {list.Count}");
            }

            if (_armorsByType.Count > 0)
            {
                Console.WriteLine("  Armor types:");
                foreach (var (t, list) in _armorsByType.OrderByDescending(kv => kv.Value.Count))
                    Console.WriteLine($"    {t}: {list.Count}");
            }

            if (_errors.Count > 0)
                Console.WriteLine($"  Errors: {_errors.Count}");
            if (_warnings.Count > 0)
                Console.WriteLine($"  Warnings: {_warnings.Count}");
        }
    }
}
