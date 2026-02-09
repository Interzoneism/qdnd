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
        
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        
        // --- Registration ---
        public void RegisterRace(RaceDefinition race) => _races[race.Id] = race;
        public void RegisterClass(ClassDefinition classDef) => _classes[classDef.Id] = classDef;
        public void RegisterFeat(FeatDefinition feat) => _feats[feat.Id] = feat;
        
        // --- Lookup ---
        public RaceDefinition GetRace(string id) => id != null && _races.TryGetValue(id, out var r) ? r : null;
        public ClassDefinition GetClass(string id) => id != null && _classes.TryGetValue(id, out var c) ? c : null;
        public FeatDefinition GetFeat(string id) => id != null && _feats.TryGetValue(id, out var f) ? f : null;
        
        public IReadOnlyCollection<RaceDefinition> GetAllRaces() => _races.Values;
        public IReadOnlyCollection<ClassDefinition> GetAllClasses() => _classes.Values;
        public IReadOnlyCollection<FeatDefinition> GetAllFeats() => _feats.Values;
        
        // --- Loading ---
        
        public int LoadRacesFromFile(string path) => LoadFromFile<RacePack, RaceDefinition>(path, p => p.Races, RegisterRace);
        public int LoadClassesFromFile(string path) => LoadFromFile<ClassPack, ClassDefinition>(path, p => p.Classes, RegisterClass);
        public int LoadFeatsFromFile(string path) => LoadFromFile<FeatPack, FeatDefinition>(path, p => p.Feats, RegisterFeat);
        
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
        /// Expects: Races/, Classes/, Feats/ subdirectories.
        /// </summary>
        public void LoadFromDirectory(string basePath)
        {
            int totalRaces = 0, totalClasses = 0, totalFeats = 0;
            
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
            
            Godot.GD.Print($"[CharRegistry] Loaded: {totalRaces} races, {totalClasses} classes, {totalFeats} feats");
        }
        
        public void PrintStats()
        {
            Godot.GD.Print($"[CharRegistry] Races: {_races.Count}, Classes: {_classes.Count}, Feats: {_feats.Count}");
        }
    }
    
    // JSON pack wrappers
    public class RacePack { public List<RaceDefinition> Races { get; set; } }
    public class ClassPack { public List<ClassDefinition> Classes { get; set; } }
    public class FeatPack { public List<FeatDefinition> Feats { get; set; } }
}

