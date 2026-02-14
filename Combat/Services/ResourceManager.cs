using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Data;
using QDND.Data.ActionResources;
using QDND.Data.CharacterModel;
using QDND.Data.Spells;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Service for managing action resources (spell slots, rage, ki, etc.) for combatants.
    /// Handles initialization, validation, consumption, and replenishment.
    /// 
    /// This is a SERVICE class that initializes resources for combatants based on their class/level.
    /// The actual per-combatant resource tracking is done by ResourcePool (Combatant.ActionResources).
    /// 
    /// Usage:
    /// - Load definitions: new ResourceManager() loads BG3 ActionResourceDefinitions
    /// - Initialize combatant: resourceManager.InitializeResources(combatant)
    /// - Check/consume costs: resourceManager.CanPayCost() / ConsumeCost()
    /// 
    /// See also: ResourcePool for per-combatant resource tracking
    /// </summary>
    public class ResourceManager
    {
        private readonly Dictionary<string, ActionResourceDefinition> _resourceDefinitions;
        
        /// <summary>
        /// All loaded resource definitions.
        /// </summary>
        public IReadOnlyDictionary<string, ActionResourceDefinition> ResourceDefinitions => _resourceDefinitions;
        
        public ResourceManager()
        {
            // Load resource definitions from BG3 data
            try
            {
                _resourceDefinitions = ActionResourceLoader.LoadActionResources();
            }
            catch (Exception ex)
            {
                Godot.GD.PushError($"[ResourceManager] Failed to load action resources: {ex.Message}");
                _resourceDefinitions = new Dictionary<string, ActionResourceDefinition>();
            }
        }
        
        /// <summary>
        /// Initialize resources for a combatant based on their character build.
        /// </summary>
        public void InitializeResources(Combatant combatant)
        {
            if (combatant == null)
                throw new ArgumentNullException(nameof(combatant));
            
            var pool = combatant.ActionResources;
            if (pool == null)
            {
                Godot.GD.PushError($"[ResourceManager] Combatant {combatant.Name} has no ActionResources");
                return;
            }
            
            // Always initialize core action economy resources
            InitializeCoreResources(pool);
            
            // Initialize character-specific resources if available
            if (combatant.ResolvedCharacter != null)
            {
                InitializeCharacterResources(combatant.ResolvedCharacter, pool);
            }
        }
        
        /// <summary>
        /// Initialize core action economy resources (ActionPoint, BonusActionPoint, Reaction).
        /// </summary>
        private void InitializeCoreResources(ResourcePool pool)
        {
            // Action Point (1 per turn)
            if (TryGetDefinition("ActionPoint", out var actionDef))
            {
                pool.AddResource(actionDef);
                pool.SetMax("ActionPoint", 1);
            }
            
            // Bonus Action Point (1 per turn)
            if (TryGetDefinition("BonusActionPoint", out var bonusDef))
            {
                pool.AddResource(bonusDef);
                pool.SetMax("BonusActionPoint", 1);
            }
            
            // Reaction (1 per turn)
            if (TryGetDefinition("ReactionActionPoint", out var reactionDef))
            {
                pool.AddResource(reactionDef);
                pool.SetMax("ReactionActionPoint", 1);
            }
        }
        
        /// <summary>
        /// Initialize resources based on character class, level, and abilities.
        /// </summary>
        private void InitializeCharacterResources(ResolvedCharacter character, ResourcePool pool)
        {
            if (character?.Sheet == null)
                return;
            
            int totalLevel = character.Sheet.TotalLevel;
            
            // Initialize spell slots based on spellcasting classes
            InitializeSpellSlots(character, pool);
            
            // Initialize class-specific resources
            foreach (var classLevel in character.Sheet.ClassLevels)
            {
                int level = character.Sheet.GetClassLevel(classLevel.ClassId);
                InitializeClassResources(classLevel.ClassId, level, pool);
            }
        }
        
        /// <summary>
        /// Initialize spell slots based on character's spellcasting progression.
        /// </summary>
        private void InitializeSpellSlots(ResolvedCharacter character, ResourcePool pool)
        {
            if (character?.Resources == null)
                return;
            
            // Check for pact magic (warlock)
            if (character.Resources.TryGetValue("pact_slots", out int pactSlots) &&
                character.Resources.TryGetValue("pact_slot_level", out int pactLevel))
            {
                if (!TryGetDefinition("WarlockSpellSlot", out var warlockSlotDef))
                {
                    Godot.GD.PushWarning("[ResourceManager] WarlockSpellSlot definition not found");
                }
                else
                {
                    pool.AddResource(warlockSlotDef);
                    if (pactSlots > 0 && pactLevel > 0)
                    {
                        pool.SetMax("WarlockSpellSlot", pactSlots, pactLevel);
                    }
                }
            }
            
            // Standard spell slots (spell_slot_1, spell_slot_2, etc.)
            bool hasAnySlots = false;
            for (int level = 1; level <= 9; level++)
            {
                string key = $"spell_slot_{level}";
                if (character.Resources.TryGetValue(key, out int slotCount) && slotCount > 0)
                {
                    if (!hasAnySlots)
                    {
                        // Add SpellSlot resource definition on first slot
                        if (TryGetDefinition("SpellSlot", out var spellSlotDef))
                        {
                            pool.AddResource(spellSlotDef);
                            hasAnySlots = true;
                        }
                        else
                        {
                            Godot.GD.PushWarning("[ResourceManager] SpellSlot definition not found");
                            break;
                        }
                    }
                    pool.SetMax("SpellSlot", slotCount, level);
                }
            }
        }
        
        /// <summary>
        /// Initialize class-specific resources (Rage, Ki, Channel Divinity, etc.).
        /// </summary>
        private void InitializeClassResources(string className, int level, ResourcePool pool)
        {
            switch (className?.ToLowerInvariant())
            {
                case "barbarian":
                    InitializeBarbarian(level, pool);
                    break;
                
                case "monk":
                    InitializeMonk(level, pool);
                    break;
                
                case "cleric":
                    InitializeCleric(level, pool);
                    break;
                
                case "paladin":
                    InitializePaladin(level, pool);
                    break;
                
                case "bard":
                    InitializeBard(level, pool);
                    break;
                
                case "druid":
                    InitializeDruid(level, pool);
                    break;
                
                case "fighter":
                    InitializeFighter(level, pool);
                    break;
                
                case "sorcerer":
                    InitializeSorcerer(level, pool);
                    break;
            }
        }
        
        private void InitializeBarbarian(int level, ResourcePool pool)
        {
            // Rage charges
            if (TryGetDefinition("Rage", out var rageDef))
            {
                pool.AddResource(rageDef);
                int rageCharges = level >= 17 ? 6 : level >= 12 ? 5 : level >= 6 ? 4 : level >= 3 ? 3 : 2;
                pool.SetMax("Rage", rageCharges);
            }
        }
        
        private void InitializeMonk(int level, ResourcePool pool)
        {
            // Ki points
            if (TryGetDefinition("KiPoint", out var kiDef))
            {
                pool.AddResource(kiDef);
                pool.SetMax("KiPoint", level);
            }
        }
        
        private void InitializeCleric(int level, ResourcePool pool)
        {
            // Channel Divinity
            if (TryGetDefinition("ChannelDivinity", out var channelDef))
            {
                pool.AddResource(channelDef);
                int charges = level >= 18 ? 3 : level >= 6 ? 2 : 1;
                pool.SetMax("ChannelDivinity", charges);
            }
        }
        
        private void InitializePaladin(int level, ResourcePool pool)
        {
            // Channel Oath (similar to Channel Divinity)
            if (TryGetDefinition("ChannelOath", out var oathDef))
            {
                pool.AddResource(oathDef);
                pool.SetMax("ChannelOath", 1);
            }
            
            // Lay on Hands
            if (TryGetDefinition("LayOnHandsCharge", out var layDef))
            {
                pool.AddResource(layDef);
                pool.SetMax("LayOnHandsCharge", level * 5);
            }
        }
        
        private void InitializeBard(int level, ResourcePool pool)
        {
            // Bardic Inspiration
            if (TryGetDefinition("BardicInspiration", out var inspirationDef))
            {
                pool.AddResource(inspirationDef);
                // Charges equal to Charisma modifier (default to proficiency bonus as approximation)
                int charges = Math.Max(1, (level - 1) / 4 + 2);
                pool.SetMax("BardicInspiration", charges);
            }
        }
        
        private void InitializeDruid(int level, ResourcePool pool)
        {
            // Wild Shape
            if (TryGetDefinition("WildShape", out var wildDef))
            {
                pool.AddResource(wildDef);
                pool.SetMax("WildShape", 2);
            }
        }
        
        private void InitializeFighter(int level, ResourcePool pool)
        {
            // Superiority Die (Battle Master)
            // Note: This should check subclass, but for now we'll add it
            if (TryGetDefinition("SuperiorityDie", out var superiorityDef))
            {
                pool.AddResource(superiorityDef);
                // Battle Master gets 4 dice at level 3, +1 at levels 7 and 15
                // This is a simplified version - proper implementation needs subclass check
            }
            
            // Action Surge (additional action)
            if (level >= 2 && TryGetDefinition("ExtraActionPoint", out var actionSurgeDef))
            {
                pool.AddResource(actionSurgeDef);
                int charges = level >= 17 ? 2 : 1;
                pool.SetMax("ExtraActionPoint", charges);
            }
        }
        
        private void InitializeSorcerer(int level, ResourcePool pool)
        {
            // Sorcery Points
            if (TryGetDefinition("SorceryPoint", out var sorceryDef))
            {
                pool.AddResource(sorceryDef);
                pool.SetMax("SorceryPoint", level);
            }
        }
        
        /// <summary>
        /// Validate if a combatant can pay the costs for a spell or action.
        /// </summary>
        public (bool CanPay, string Reason) CanPayCost(Combatant combatant, SpellUseCost useCost)
        {
            if (combatant == null || useCost == null)
                return (true, null);
            
            var pool = combatant.ActionResources;
            
            // Check action point
            if (useCost.ActionPoint > 0 && !pool.Has("ActionPoint", useCost.ActionPoint))
                return (false, "No action available");
            
            // Check bonus action
            if (useCost.BonusActionPoint > 0 && !pool.Has("BonusActionPoint", useCost.BonusActionPoint))
                return (false, "No bonus action available");
            
            // Check reaction
            if (useCost.ReactionActionPoint > 0 && !pool.Has("ReactionActionPoint", useCost.ReactionActionPoint))
                return (false, "No reaction available");
            
            // Check spell slot
            if (useCost.SpellSlotLevel > 0)
            {
                // Try standard spell slots first
                bool hasSlot = pool.Has("SpellSlot", useCost.SpellSlotCount, useCost.SpellSlotLevel);
                
                // If not, try warlock slots
                if (!hasSlot)
                    hasSlot = pool.Has("WarlockSpellSlot", useCost.SpellSlotCount, useCost.SpellSlotLevel);
                
                if (!hasSlot)
                    return (false, $"No level {useCost.SpellSlotLevel} spell slot available");
            }
            
            // Check custom resources
            if (useCost.CustomResources != null)
            {
                foreach (var (resourceName, amount) in useCost.CustomResources)
                {
                    if (!pool.Has(resourceName, amount))
                        return (false, $"Insufficient {resourceName} ({pool.GetCurrent(resourceName)}/{amount})");
                }
            }
            
            return (true, null);
        }
        
        /// <summary>
        /// Consume resources for a spell or action.
        /// </summary>
        public bool ConsumeCost(Combatant combatant, SpellUseCost useCost, out string errorReason)
        {
            errorReason = null;
            
            if (combatant == null || useCost == null)
                return true;
            
            // Validate first
            var (canPay, reason) = CanPayCost(combatant, useCost);
            if (!canPay)
            {
                errorReason = reason;
                return false;
            }
            
            var pool = combatant.ActionResources;
            
            // Consume action economy (handled by ActionBudget separately in most cases)
            // But we track it here for consistency
            if (useCost.ActionPoint > 0)
                pool.Consume("ActionPoint", useCost.ActionPoint);
            
            if (useCost.BonusActionPoint > 0)
                pool.Consume("BonusActionPoint", useCost.BonusActionPoint);
            
            if (useCost.ReactionActionPoint > 0)
                pool.Consume("ReactionActionPoint", useCost.ReactionActionPoint);
            
            // Consume spell slot
            if (useCost.SpellSlotLevel > 0)
            {
                bool consumed = pool.Consume("SpellSlot", useCost.SpellSlotCount, useCost.SpellSlotLevel);
                if (!consumed)
                    consumed = pool.Consume("WarlockSpellSlot", useCost.SpellSlotCount, useCost.SpellSlotLevel);
                
                if (!consumed)
                {
                    errorReason = "Failed to consume spell slot";
                    return false;
                }
            }
            
            // Consume custom resources
            if (useCost.CustomResources != null)
            {
                foreach (var (resourceName, amount) in useCost.CustomResources)
                {
                    if (!pool.Consume(resourceName, amount))
                    {
                        errorReason = $"Failed to consume {resourceName}";
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Replenish resources for a combatant based on replenish type.
        /// </summary>
        public void ReplenishResources(Combatant combatant, ReplenishType replenishType)
        {
            if (combatant?.ActionResources == null)
                return;
            
            combatant.ActionResources.ReplenishResources(replenishType);
        }
        
        /// <summary>
        /// Replenish turn-based resources (Action, Bonus Action, Reaction).
        /// </summary>
        public void ReplenishTurnResources(Combatant combatant)
        {
            ReplenishResources(combatant, ReplenishType.Turn);
        }
        
        /// <summary>
        /// Replenish short rest resources.
        /// </summary>
        public void ReplenishShortRest(Combatant combatant)
        {
            ReplenishResources(combatant, ReplenishType.ShortRest);
        }
        
        /// <summary>
        /// Replenish long rest resources (full restoration).
        /// </summary>
        public void ReplenishLongRest(Combatant combatant)
        {
            if (combatant?.ActionResources == null)
                return;
            
            combatant.ActionResources.RestoreAll();
        }
        
        /// <summary>
        /// Try to get a resource definition by name.
        /// </summary>
        private bool TryGetDefinition(string name, out ActionResourceDefinition definition)
        {
            return _resourceDefinitions.TryGetValue(name, out definition);
        }
        
        /// <summary>
        /// Get resource definition by name (throws if not found).
        /// </summary>
        public ActionResourceDefinition GetDefinition(string name)
        {
            if (_resourceDefinitions.TryGetValue(name, out var def))
                return def;
            
            throw new KeyNotFoundException($"Resource definition '{name}' not found");
        }
        
        /// <summary>
        /// Get current resource status for a combatant (for UI display).
        /// </summary>
        public Dictionary<string, string> GetResourceStatus(Combatant combatant)
        {
            var status = new Dictionary<string, string>();
            
            if (combatant?.ActionResources == null)
                return status;
            
            foreach (var resource in combatant.ActionResources.Resources.Values)
            {
                if (resource.IsLeveled)
                {
                    // Spell slots - show each level
                    var levels = new List<string>();
                    foreach (var level in resource.MaxByLevel.Keys.OrderBy(k => k))
                    {
                        int current = resource.GetCurrent(level);
                        int max = resource.GetMax(level);
                        if (max > 0)
                            levels.Add($"L{level}:{current}/{max}");
                    }
                    if (levels.Count > 0)
                        status[resource.Definition.DisplayName ?? resource.Definition.Name] = string.Join(", ", levels);
                }
                else if (resource.Max > 0)
                {
                    // Simple resource
                    status[resource.Definition.DisplayName ?? resource.Definition.Name] = 
                        $"{resource.Current}/{resource.Max}";
                }
            }
            
            return status;
        }
    }
}
