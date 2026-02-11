using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QDND.Data.CharacterModel
{
    /// <summary>
    /// Registry for character build data: races, classes, feats.
    /// Loads from JSON files following the same pattern as DataRegistry.
    /// </summary>
    public class CharacterDataRegistry
    {
        private readonly Dictionary<string, RaceDefinition> _races = new();
        private readonly Dictionary<string, ClassDefinition> _classes = new();
        private readonly Dictionary<string, FeatDefinition> _feats = new();
        private readonly Dictionary<string, WeaponDefinition> _weapons = new();
        private readonly Dictionary<string, ArmorDefinition> _armors = new();
        
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        
        // --- Registration ---
        public void RegisterRace(RaceDefinition race) => _races[race.Id] = race;
        public void RegisterClass(ClassDefinition classDef) => _classes[classDef.Id] = classDef;
        public void RegisterFeat(FeatDefinition feat) => _feats[feat.Id] = feat;
        public void RegisterWeapon(WeaponDefinition weapon) => _weapons[weapon.Id] = weapon;
        public void RegisterArmor(ArmorDefinition armor) => _armors[armor.Id] = armor;
        
        // --- Lookup ---
        public RaceDefinition GetRace(string id) => id != null && _races.TryGetValue(id, out var r) ? r : null;
        public ClassDefinition GetClass(string id) => id != null && _classes.TryGetValue(id, out var c) ? c : null;
        public FeatDefinition GetFeat(string id) => id != null && _feats.TryGetValue(id, out var f) ? f : null;
        public WeaponDefinition GetWeapon(string id) => id != null && _weapons.TryGetValue(id, out var w) ? w : null;
        public ArmorDefinition GetArmor(string id) => id != null && _armors.TryGetValue(id, out var a) ? a : null;
        
        public IReadOnlyCollection<RaceDefinition> GetAllRaces() => _races.Values;
        public IReadOnlyCollection<ClassDefinition> GetAllClasses() => _classes.Values;
        public IReadOnlyCollection<FeatDefinition> GetAllFeats() => _feats.Values;
        public IReadOnlyCollection<WeaponDefinition> GetAllWeapons() => _weapons.Values;
        public IReadOnlyCollection<ArmorDefinition> GetAllArmors() => _armors.Values;
        
        /// <summary>Lookup weapon by WeaponType enum.</summary>
        public WeaponDefinition GetWeaponByType(WeaponType weaponType)
        {
            return _weapons.Values.FirstOrDefault(w => w.WeaponType == weaponType);
        }
        
        // --- Loading ---
        
        public int LoadRacesFromFile(string path) => LoadFromFile<RacePack, RaceDefinition>(path, p => p.Races, RegisterRace);
        public int LoadClassesFromFile(string path) => LoadFromFile<ClassPack, ClassDefinition>(path, p => p.Classes, RegisterClass);
        public int LoadFeatsFromFile(string path) => LoadFromFile<FeatPack, FeatDefinition>(path, p => p.Feats, RegisterFeat);
        
        public int LoadEquipmentFromFile(string path)
        {
            string json;
            if (path.StartsWith("res://"))
            {
                using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                if (file == null) { Godot.GD.PrintErr($"[CharRegistry] Equipment file not found: {path}"); return 0; }
                json = Encoding.UTF8.GetString(file.GetBuffer((long)file.GetLength()));
            }
            else
            {
                if (!File.Exists(path)) { Godot.GD.PrintErr($"[CharRegistry] Equipment file not found: {path}"); return 0; }
                json = File.ReadAllText(path);
            }
            
            try
            {
                var pack = JsonSerializer.Deserialize<EquipmentPack>(json, JsonOptions);
                int count = 0;
                if (pack.Weapons != null)
                {
                    foreach (var weapon in pack.Weapons) RegisterWeapon(weapon);
                    count += pack.Weapons.Count;
                }
                if (pack.Armors != null)
                {
                    foreach (var armor in pack.Armors) RegisterArmor(armor);
                    count += pack.Armors.Count;
                }
                return count;
            }
            catch (Exception ex)
            {
                Godot.GD.PrintErr($"[CharRegistry] Failed to load equipment from {path}: {ex.Message}");
                return 0;
            }
        }
        
        private int LoadFromFile<TPack, TItem>(string path, Func<TPack, List<TItem>> getItems, Action<TItem> register) where TItem : class
        {
            string json;
            if (path.StartsWith("res://"))
            {
                using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                if (file == null) { Godot.GD.PrintErr($"[CharRegistry] File not found: {path}"); return 0; }
                json = Encoding.UTF8.GetString(file.GetBuffer((long)file.GetLength()));
            }
            else
            {
                if (!File.Exists(path)) { Godot.GD.PrintErr($"[CharRegistry] File not found: {path}"); return 0; }
                json = File.ReadAllText(path);
            }
            
            try
            {
                var pack = JsonSerializer.Deserialize<TPack>(json, JsonOptions);
                var items = getItems(pack);
                if (items == null) return 0;
                foreach (var item in items) register(item);
                return items.Count;
            }
            catch (Exception ex)
            {
                Godot.GD.PrintErr($"[CharRegistry] Failed to load from {path}: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Load all character data from a directory structure.
        /// Expects: Races/, Classes/, Feats/ subdirectories, and optional equipment_data.json.
        /// </summary>
        public void LoadFromDirectory(string basePath)
        {
            int totalRaces = 0, totalClasses = 0, totalFeats = 0, totalEquipment = 0;
            
            string racesPath = Path.Combine(basePath, "Races");
            if (Directory.Exists(racesPath))
                foreach (var file in Directory.GetFiles(racesPath, "*.json"))
                    totalRaces += LoadRacesFromFile(file);
            
            string classesPath = Path.Combine(basePath, "Classes");
            if (Directory.Exists(classesPath))
                foreach (var file in Directory.GetFiles(classesPath, "*.json"))
                    totalClasses += LoadClassesFromFile(file);
            
            string featsPath = Path.Combine(basePath, "Feats");
            if (Directory.Exists(featsPath))
                foreach (var file in Directory.GetFiles(featsPath, "*.json"))
                    totalFeats += LoadFeatsFromFile(file);
            
            // Load equipment from CharacterModel/equipment_data.json
            string equipmentPath = Path.Combine(basePath, "CharacterModel", "equipment_data.json");
            if (File.Exists(equipmentPath))
                totalEquipment = LoadEquipmentFromFile(equipmentPath);
            
            Godot.GD.Print($"[CharRegistry] Loaded: {totalRaces} races, {totalClasses} classes, {totalFeats} feats, {totalEquipment} equipment items");
        }
        
        public void PrintStats()
        {
            Godot.GD.Print($"[CharRegistry] Races: {_races.Count}, Classes: {_classes.Count}, Feats: {_feats.Count}, Weapons: {_weapons.Count}, Armors: {_armors.Count}");
        }
    }
    
    // JSON pack wrappers
    public class RacePack { public List<RaceDefinition> Races { get; set; } }
    public class ClassPack { public List<ClassDefinition> Classes { get; set; } }
    public class FeatPack { public List<FeatDefinition> Feats { get; set; } }
    public class EquipmentPack 
    { 
        public List<WeaponDefinition> Weapons { get; set; } 
        public List<ArmorDefinition> Armors { get; set; } 
    }
}

