using QDND.Data.CharacterModel;

namespace QDND.Data
{
    /// <summary>
    /// Master loader for all BG3 character data.
    /// Loads classes, races, feats, and equipment from JSON data files.
    /// </summary>
    public static class BG3DataLoader
    {
        /// <summary>
        /// Load all BG3 character data into the registry.
        /// This includes all 12 BG3 classes, all races/subraces, feats, weapons, and armor.
        /// </summary>
        public static void LoadAll(CharacterDataRegistry registry)
        {
            int totalLoaded = 0;
            
            // Load Classes (12 BG3 classes)
            totalLoaded += registry.LoadClassesFromFile("res://Data/Classes/martial_classes.json");  // Fighter, Barbarian, Monk, Rogue
            totalLoaded += registry.LoadClassesFromFile("res://Data/Classes/arcane_classes.json");   // Wizard, Sorcerer, Warlock, Bard
            totalLoaded += registry.LoadClassesFromFile("res://Data/Classes/divine_classes.json");   // Cleric, Paladin, Druid, Ranger
            
            // Load Races (11 BG3 races with subraces)
            totalLoaded += registry.LoadRacesFromFile("res://Data/Races/core_races.json");     // Human, Elf, Drow, Half-Elf, Half-Orc, Halfling
            totalLoaded += registry.LoadRacesFromFile("res://Data/Races/exotic_races.json");   // Dwarf, Gnome, Tiefling, Githyanki, Dragonborn
            
            // Load Equipment (weapons and armor)
            totalLoaded += registry.LoadEquipmentFromFile("res://Data/CharacterModel/equipment_data.json");
            
            // Load Feats (optional but useful for character building)
            if (RuntimeSafety.ResourceFileExists("res://Data/Feats/bg3_feats.json"))
            {
                totalLoaded += registry.LoadFeatsFromFile("res://Data/Feats/bg3_feats.json");
            }
            
            RuntimeSafety.Log($"[BG3DataLoader] Loaded {totalLoaded} total items from BG3 data files.");
            registry.PrintStats();
        }
        
        /// <summary>
        /// Load only classes (useful for testing or incremental loading).
        /// </summary>
        public static void LoadClasses(CharacterDataRegistry registry)
        {
            int count = 0;
            count += registry.LoadClassesFromFile("res://Data/Classes/martial_classes.json");
            count += registry.LoadClassesFromFile("res://Data/Classes/arcane_classes.json");
            count += registry.LoadClassesFromFile("res://Data/Classes/divine_classes.json");
            RuntimeSafety.Log($"[BG3DataLoader] Loaded {count} classes.");
        }
        
        /// <summary>
        /// Load only races (useful for testing or incremental loading).
        /// </summary>
        public static void LoadRaces(CharacterDataRegistry registry)
        {
            int count = 0;
            count += registry.LoadRacesFromFile("res://Data/Races/core_races.json");
            count += registry.LoadRacesFromFile("res://Data/Races/exotic_races.json");
            RuntimeSafety.Log($"[BG3DataLoader] Loaded {count} races.");
        }
        
        /// <summary>
        /// Load only equipment (weapons and armor).
        /// </summary>
        public static void LoadEquipment(CharacterDataRegistry registry)
        {
            int count = registry.LoadEquipmentFromFile("res://Data/CharacterModel/equipment_data.json");
            RuntimeSafety.Log($"[BG3DataLoader] Loaded {count} equipment items.");
        }
        
        /// <summary>
        /// Load only feats.
        /// </summary>
        public static void LoadFeats(CharacterDataRegistry registry)
        {
            if (RuntimeSafety.ResourceFileExists("res://Data/Feats/bg3_feats.json"))
            {
                int count = registry.LoadFeatsFromFile("res://Data/Feats/bg3_feats.json");
                RuntimeSafety.Log($"[BG3DataLoader] Loaded {count} feats.");
            }
            else
            {
                RuntimeSafety.LogError("[BG3DataLoader] Feats file not found: res://Data/Feats/bg3_feats.json");
            }
        }
    }
}
