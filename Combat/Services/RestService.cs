using System;
using System.Collections.Generic;
using QDND.Combat.Entities;
using QDND.Data.ActionResources;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Service that handles rest mechanics and resource replenishment.
    /// Manages short rests, long rests, and turn-based resource replenishment.
    /// 
    /// Rules:
    /// - Turn start: Replenish ActionPoint, BonusActionPoint, ReactionActionPoint (handled via ReplenishType.Turn)
    /// - Short rest: Replenish resources with ReplenishType.ShortRest (e.g., Warlock spell slots, Ki Points)
    /// - Long rest: Replenish ALL resources (ShortRest, Rest, FullRest) and fully heal HP
    /// - Resources with ReplenishType.Never are never automatically replenished
    /// 
    /// Integration:
    /// - Register in CombatContext for access across combat systems
    /// - Hook ReplenishTurnResources into turn start flow (CombatArena OnTurnStart)
    /// - Use ShortRest/LongRest for camp/rest UI
    /// </summary>
    public class RestService
    {
        private readonly ResourceManager _resourceManager;
        
        /// <summary>
        /// Create a new RestService.
        /// </summary>
        /// <param name="resourceManager">Resource manager for handling resource operations</param>
        public RestService(ResourceManager resourceManager)
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
        }
        
        /// <summary>
        /// Process a rest for a single combatant.
        /// </summary>
        /// <param name="combatant">The combatant to rest</param>
        /// <param name="restType">Type of rest (Short or Long)</param>
        public void ProcessRest(Combatant combatant, RestType restType)
        {
            if (combatant == null)
                return;
            
            switch (restType)
            {
                case RestType.Short:
                    ProcessShortRest(combatant);
                    break;
                
                case RestType.Long:
                    ProcessLongRest(combatant);
                    break;
            }
        }
        
        /// <summary>
        /// Replenish per-turn resources (ActionPoint, BonusActionPoint, ReactionActionPoint).
        /// Call this at the start of each combatant's turn.
        /// </summary>
        /// <param name="combatant">The combatant starting their turn</param>
        public void ReplenishTurnResources(Combatant combatant)
        {
            if (combatant?.ActionResources == null)
                return;
            
            // Replenish resources with ReplenishType.Turn
            // This includes ActionPoint, BonusActionPoint, ReactionActionPoint, and Movement
            combatant.ActionResources.ReplenishTurn();
        }
        
        /// <summary>
        /// Replenish per-round resources (if any).
        /// Currently a placeholder - BG3 doesn't have distinct round-based resources.
        /// </summary>
        /// <param name="combatant">The combatant</param>
        public void ReplenishRoundResources(Combatant combatant)
        {
            if (combatant?.ActionResources == null)
                return;
            
            // Placeholder for potential round-based resources
            // In BG3/5e, most round-based resources are handled as Turn resources
        }
        
        /// <summary>
        /// Process short rest for multiple combatants.
        /// </summary>
        /// <param name="combatants">Combatants to rest</param>
        public void ShortRest(IEnumerable<Combatant> combatants)
        {
            if (combatants == null)
                return;
            
            foreach (var combatant in combatants)
            {
                ProcessShortRest(combatant);
            }
        }
        
        /// <summary>
        /// Process long rest for multiple combatants.
        /// </summary>
        /// <param name="combatants">Combatants to rest</param>
        public void LongRest(IEnumerable<Combatant> combatants)
        {
            if (combatants == null)
                return;
            
            foreach (var combatant in combatants)
            {
                ProcessLongRest(combatant);
            }
        }
        
        /// <summary>
        /// Process a short rest for a combatant.
        /// Replenishes resources with ReplenishType.ShortRest.
        /// </summary>
        private void ProcessShortRest(Combatant combatant)
        {
            if (combatant?.ActionResources == null)
                return;
            
            // Replenish short rest resources (Ki, Warlock spell slots, etc.)
            combatant.ActionResources.ReplenishShortRest();
            
            // Note: Hit Dice healing could be added here in the future
            // For now, short rest only replenishes resources, not HP
        }
        
        /// <summary>
        /// Process a long rest for a combatant.
        /// Replenishes ALL resources and fully heals HP.
        /// </summary>
        private void ProcessLongRest(Combatant combatant)
        {
            if (combatant == null)
                return;
            
            // Replenish all resources (spell slots, rage, ki, class features, etc.)
            // This replenishes both Rest and FullRest ReplenishType resources
            if (combatant.ActionResources != null)
            {
                combatant.ActionResources.ReplenishRest();
                
                // Also replenish short rest resources (long rest includes short rest benefits)
                combatant.ActionResources.ReplenishShortRest();
            }
            
            // Fully heal HP
            if (combatant.Resources != null)
            {
                int maxHP = combatant.Resources.MaxHP;
                int healAmount = maxHP - combatant.Resources.CurrentHP;
                if (healAmount > 0)
                {
                    combatant.Resources.Heal(healAmount);
                }
            }
        }
    }
}
