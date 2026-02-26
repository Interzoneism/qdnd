using System;
using System.Collections.Generic;
using QDND.Combat.Entities;
using QDND.Data;
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
        private DifficultyService _difficultyService;
        
        /// <summary>
        /// Create a new RestService.
        /// </summary>
        /// <param name="resourceManager">Resource manager for handling resource operations</param>
        /// <param name="difficultyService">Optional difficulty service for Explorer-mode short rest full heal</param>
        public RestService(ResourceManager resourceManager, DifficultyService difficultyService = null)
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            _difficultyService = difficultyService;
        }
        
        /// <summary>
        /// Set or update the difficulty service (e.g. after combat context init).
        /// </summary>
        public void SetDifficultyService(DifficultyService difficultyService)
        {
            _difficultyService = difficultyService;
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
        /// Replenish per-turn resources (Action, Bonus Action, Reaction).
        /// Delegates to ResourceManager.ReplenishTurnResources.
        /// Hook this into CombatArena OnTurnStart.
        /// </summary>
        /// <param name="combatant">The combatant whose turn is starting</param>
        public void ReplenishTurnResources(Combatant combatant)
        {
            if (combatant == null)
                return;
            
            _resourceManager.ReplenishTurnResources(combatant);
            
            // Also reset ActionBudget flags
            combatant.ActionBudget?.ResetForTurn();
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
        /// Spend one hit die during a short rest.
        /// Heals average hit-die roll + Constitution modifier: (hitDieMax/2 + 1) + conMod.
        /// Returns the amount of HP actually restored (0 if no hit dice left or not applicable).
        /// </summary>
        /// <param name="combatant">The combatant spending a hit die</param>
        /// <param name="hitDieSize">Hit die size (e.g. 6 for d6, 10 for d10). Pass 0 to auto-detect from character data (defaults to d8).</param>
        public int SpendHitDie(Combatant combatant, int hitDieSize = 0)
        {
            if (combatant == null) return 0;

            // Check if the combatant has HitDice resource remaining
            var hdResource = combatant.ActionResources.GetResource("HitDice");
            if (hdResource == null || hdResource.Current <= 0) return 0;

            // Determine hit die size
            int hitDieMax = hitDieSize > 0 ? hitDieSize : 8; // default d8 if not specified

            // Average roll = hitDieMax / 2 + 1
            int conMod = combatant.GetAbilityModifier(AbilityType.Constitution);
            int healAmount = Math.Max(1, hitDieMax / 2 + 1 + conMod);

            // Spend the resource
            hdResource.Current = Math.Max(0, hdResource.Current - 1);

            // Apply healing
            return combatant.Resources.Heal(healAmount);
        }
        
        /// <summary>
        /// Process a short rest for a combatant.
        /// Replenishes resources with ReplenishType.ShortRest.
        /// Optionally heals via hit dice or fully heals in Explorer mode.
        /// </summary>
        private void ProcessShortRest(Combatant combatant)
        {
            if (combatant?.ActionResources == null)
                return;
            
            // Replenish short rest resources (Ki, Warlock spell slots, etc.)
            combatant.ActionResources.ReplenishShortRest();
            
            // Explorer difficulty: short rest fully heals
            if (_difficultyService != null && _difficultyService.ShortRestFullyHeals)
            {
                if (combatant.Resources != null)
                {
                    combatant.Resources.Heal(combatant.Resources.MaxHP - combatant.Resources.CurrentHP);
                }
            }
        }
        
        /// <summary>
        /// Process a long rest for a combatant.
        /// Replenishes ALL resources and fully heals HP.
        /// </summary>
        private void ProcessLongRest(Combatant combatant)
        {
            if (combatant == null)
                return;
            
            // Restore resources with ReplenishType.FullRest or ShortRest
            if (combatant.ActionResources != null)
            {
                combatant.ActionResources.ReplenishRest();
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
