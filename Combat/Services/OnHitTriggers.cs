using System;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Combat.Statuses;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Built-in on-hit trigger implementations.
    /// </summary>
    public static class OnHitTriggers
    {
        /// <summary>
        /// Register the Dip weapon coating bonus damage trigger.
        /// When attacker has a dipped_* status, adds 1d4 bonus damage of the matching element.
        /// </summary>
        public static void RegisterDipDamage(OnHitTriggerService service, StatusManager statuses)
        {
            service.RegisterTrigger("dip_coating", OnHitTriggerType.OnHitConfirmed, (context) =>
            {
                if (statuses == null || context.Attacker == null)
                    return false;

                // Only weapon attacks trigger dip coating
                if (context.AttackType != AttackType.MeleeWeapon &&
                    context.AttackType != AttackType.RangedWeapon)
                    return false;

                var dipStatus = statuses.GetStatuses(context.Attacker.Id)
                    .FirstOrDefault(s => s.Definition.Tags.Contains("weapon_coating"));

                if (dipStatus == null)
                    return false;

                string damageType = dipStatus.Definition.Id switch
                {
                    "dipped_fire" => "fire",
                    "dipped_poison" => "poison",
                    "dipped_acid" => "acid",
                    _ => null // dipped_weapon generic has flat modifier, no extra dice
                };

                if (damageType == null)
                    return false;

                // Roll 1d4 bonus damage
                var rng = new Random();
                int dipDamage = rng.Next(1, 5); // 1d4

                if (context.IsCritical)
                    dipDamage += rng.Next(1, 5);

                context.BonusDamage += dipDamage;
                context.BonusDamageType = damageType;

                return true;
            });
        }

        /// <summary>
        /// Register the Divine Smite trigger.
        /// Expends a spell slot on melee weapon hit for bonus radiant damage.
        /// </summary>
        public static void RegisterDivineSmite(OnHitTriggerService service, StatusManager statuses)
        {
            service.RegisterTrigger("divine_smite", OnHitTriggerType.OnHitConfirmed, (context) =>
            {
                // Check if attacker has divine smite toggle active
                if (statuses == null || context.Attacker == null)
                    return false;

                if (!statuses.HasStatus(context.Attacker.Id, "divine_smite_active"))
                    return false;

                // Check if this is a melee weapon attack
                if (context.AttackType != AttackType.MeleeWeapon)
                    return false;

                // Check if attacker has spell slots available
                if (context.Attacker.ResourcePool == null)
                    return false;

                // Find lowest available spell slot
                string slotUsed = null;
                for (int level = 1; level <= 5; level++)
                {
                    string resourceKey = $"spell_slot_{level}";
                    if (context.Attacker.ResourcePool.HasResource(resourceKey) &&
                        context.Attacker.ResourcePool.GetCurrent(resourceKey) > 0)
                    {
                        slotUsed = resourceKey;
                        break;
                    }
                }

                if (slotUsed == null)
                    return false; // No spell slots available

                // Consume the spell slot
                context.Attacker.ResourcePool.ModifyCurrent(slotUsed, -1);

                // Roll 2d8 radiant damage (3d8 vs undead/fiend)
                bool isUndeadOrFiend = context.Target?.Tags?.Contains("undead") == true ||
                                       context.Target?.Tags?.Contains("fiend") == true;
                
                int diceCount = isUndeadOrFiend ? 3 : 2;
                
                // Use RNG from context if available, otherwise create one
                var rng = new Random();
                int smiteDamage = 0;
                for (int i = 0; i < diceCount; i++)
                {
                    smiteDamage += rng.Next(1, 9); // 1d8
                }

                // On crit, double the dice (not the bonus)
                if (context.IsCritical)
                {
                    for (int i = 0; i < diceCount; i++)
                    {
                        smiteDamage += rng.Next(1, 9);
                    }
                }

                context.BonusDamage += smiteDamage;
                context.BonusDamageType = "radiant";

                return true;
            });
        }

        /// <summary>
        /// Register the Hex per-hit bonus damage trigger.
        /// Adds 1d6 necrotic damage when target has hex status from this attacker.
        /// </summary>
        public static void RegisterHex(OnHitTriggerService service, StatusManager statuses)
        {
            service.RegisterTrigger("hexed", OnHitTriggerType.OnHitConfirmed, (context) =>
            {
                if (statuses == null || context.Attacker == null || context.Target == null)
                    return false;

                // Check if target has hex status applied by this attacker
                var hexStatus = statuses.GetStatuses(context.Target.Id)
                    .FirstOrDefault(s => s.Definition.Id == "hexed" && s.SourceId == context.Attacker.Id);

                if (hexStatus == null)
                    return false;

                // Roll 1d6 necrotic damage
                var rng = new Random();
                int hexDamage = rng.Next(1, 7);

                context.BonusDamage += hexDamage;
                context.BonusDamageType = "necrotic";

                return true;
            });
        }

        /// <summary>
        /// Register the Hunter's Mark per-hit bonus damage trigger.
        /// Adds 1d6 damage when target has hunters_mark status from this attacker.
        /// </summary>
        public static void RegisterHuntersMark(OnHitTriggerService service, StatusManager statuses)
        {
            service.RegisterTrigger("hunters_mark", OnHitTriggerType.OnHitConfirmed, (context) =>
            {
                if (statuses == null || context.Attacker == null || context.Target == null)
                    return false;

                // Check if target has hunters_mark status applied by this attacker
                var markStatus = statuses.GetStatuses(context.Target.Id)
                    .FirstOrDefault(s => s.Definition.Id == "hunters_mark" && s.SourceId == context.Attacker.Id);

                if (markStatus == null)
                    return false;

                // Check if this is a weapon attack (Hunter's Mark only works on weapon attacks)
                if (context.AttackType != AttackType.MeleeWeapon && 
                    context.AttackType != AttackType.RangedWeapon)
                    return false;

                // Roll 1d6 damage (same type as weapon damage)
                var rng = new Random();
                int markDamage = rng.Next(1, 7);

                context.BonusDamage += markDamage;
                // Don't set BonusDamageType - let it use the weapon's damage type

                return true;
            });
        }

        /// <summary>
        /// Register the Thunderous Smite on-hit trigger.
        /// On melee weapon hit: adds 2d6 Thunder damage, applies prone on failed STR save,
        /// removes the buff, and breaks concentration.
        /// </summary>
        public static void RegisterThunderousSmite(OnHitTriggerService service, StatusManager statuses, QDND.Combat.Statuses.ConcentrationSystem concentrationSystem)
        {
            service.RegisterTrigger("thunderous_smite", OnHitTriggerType.OnHitConfirmed, (context) =>
            {
                if (statuses == null || context.Attacker == null || context.Target == null)
                    return false;

                if (!statuses.HasStatus(context.Attacker.Id, "thunderous_smite_buff"))
                    return false;

                if (context.AttackType != AttackType.MeleeWeapon)
                    return false;

                // Roll 2d6 Thunder damage
                var rng = new Random();
                int smiteDamage = 0;
                int diceCount = 2;
                for (int i = 0; i < diceCount; i++)
                {
                    smiteDamage += rng.Next(1, 7); // 1d6
                }

                if (context.IsCritical)
                {
                    for (int i = 0; i < diceCount; i++)
                    {
                        smiteDamage += rng.Next(1, 7);
                    }
                }

                context.BonusDamage += smiteDamage;
                context.BonusDamageType = "thunder";

                // Apply prone to the target (STR save to resist)
                // For now, apply prone directly — save logic is handled by the status system
                context.BonusStatusesToApply.Add("prone");

                // Remove the buff and break concentration
                statuses.RemoveStatus(context.Attacker.Id, "thunderous_smite_buff");
                concentrationSystem?.EndConcentration(context.Attacker.Id);

                return true;
            });
        }

        /// <summary>
        /// Register the Great Weapon Master / Sharpshooter bonus attack trigger.
        /// Grants bonus action attack on critical hit or kill with a weapon.
        /// </summary>
        public static void RegisterGWMBonusAttack(OnHitTriggerService service)
        {
            // Register for critical hits
            service.RegisterTrigger("gwm_critical", OnHitTriggerType.OnCriticalHit, (context) =>
            {
                return GrantGWMBonusAction(context);
            });

            // Register for kills
            service.RegisterTrigger("gwm_kill", OnHitTriggerType.OnKill, (context) =>
            {
                return GrantGWMBonusAction(context);
            });
        }

        /// <summary>
        /// Register the Improved Divine Smite trigger (Paladin L11).
        /// All melee weapon hits deal an extra 1d8 radiant damage automatically (no resource cost).
        /// </summary>
        public static void RegisterImprovedDivineSmite(OnHitTriggerService service)
        {
            service.RegisterTrigger("improved_divine_smite", OnHitTriggerType.OnHitConfirmed, (context) =>
            {
                if (context.Attacker == null)
                    return false;

                // Only melee weapon attacks
                if (context.AttackType != AttackType.MeleeWeapon)
                    return false;

                // Check if attacker has Improved Divine Smite feature (Paladin L11)
                bool hasFeature = context.Attacker.ResolvedCharacter?.Features?.Any(f =>
                    string.Equals(f.Id, "improved_divine_smite", StringComparison.OrdinalIgnoreCase)) == true;
                if (!hasFeature)
                    return false;

                // 1d8 radiant damage
                var rng = new Random();
                int smiteDamage = rng.Next(1, 9);

                // On crit, double the radiant dice
                if (context.IsCritical)
                    smiteDamage += rng.Next(1, 9);

                context.BonusDamage += smiteDamage;
                context.BonusDamageType = "radiant";
                return true;
            });
        }

        /// <summary>
        /// Register the Colossus Slayer trigger (Ranger: Hunter L3).
        /// Once per turn, deal extra 1d8 damage to a creature that is below max HP.
        /// </summary>
        public static void RegisterColossusSlayer(OnHitTriggerService service)
        {
            service.RegisterTrigger("colossus_slayer", OnHitTriggerType.OnHitConfirmed, (context) =>
            {
                if (context.Attacker == null || context.Target == null)
                    return false;

                // Only weapon attacks
                if (context.AttackType != AttackType.MeleeWeapon && context.AttackType != AttackType.RangedWeapon)
                    return false;

                // Check if attacker has Colossus Slayer feature
                bool hasFeature = context.Attacker.ResolvedCharacter?.Features?.Any(f =>
                    string.Equals(f.Id, "colossus_slayer", StringComparison.OrdinalIgnoreCase)) == true;
                if (!hasFeature)
                    return false;

                // Once per turn check
                if (context.Attacker.ActionBudget?.UsedOncePerTurnFeatures.Contains("colossus_slayer") == true)
                    return false;

                // Target must be below max HP (already damaged)
                if (context.Target.Resources.CurrentHP >= context.Target.Resources.MaxHP)
                    return false;

                // 1d8 extra damage
                var rng = new Random();
                int bonusDamage = rng.Next(1, 9);
                if (context.IsCritical)
                    bonusDamage += rng.Next(1, 9);

                context.BonusDamage += bonusDamage;

                // Mark as used this turn
                context.Attacker.ActionBudget?.UsedOncePerTurnFeatures.Add("colossus_slayer");
                return true;
            });
        }

        /// <summary>
        /// Register the Stunning Strike trigger (Monk L5).
        /// On melee weapon hit, spend 1 Ki point. Target makes CON save or is Stunned.
        /// </summary>
        public static void RegisterStunningStrike(OnHitTriggerService service, StatusManager statuses)
        {
            service.RegisterTrigger("stunning_strike", OnHitTriggerType.OnHitConfirmed, (context) =>
            {
                if (context.Attacker == null || context.Target == null)
                    return false;

                // Only melee weapon attacks
                if (context.AttackType != AttackType.MeleeWeapon)
                    return false;

                // Check if attacker has Stunning Strike feature
                bool hasFeature = context.Attacker.ResolvedCharacter?.Features?.Any(f =>
                    string.Equals(f.Id, "stunning_strike", StringComparison.OrdinalIgnoreCase)) == true;
                if (!hasFeature)
                    return false;

                // Once per turn
                if (context.Attacker.ActionBudget?.UsedOncePerTurnFeatures.Contains("stunning_strike") == true)
                    return false;

                // Requires Ki points
                if (context.Attacker.ResourcePool == null ||
                    !context.Attacker.ResourcePool.HasResource("ki_points") ||
                    context.Attacker.ResourcePool.GetCurrent("ki_points") <= 0)
                    return false;

                // Consume 1 Ki point
                context.Attacker.ResourcePool.ModifyCurrent("ki_points", -1);

                // CON save: DC = 8 + proficiency + WIS modifier
                int saveDC = 8 + context.Attacker.ProficiencyBonus + context.Attacker.Stats.WisdomModifier;
                var rng = new Random();
                int saveRoll = rng.Next(1, 21) + context.Target.Stats.ConstitutionModifier;

                if (saveRoll < saveDC)
                {
                    // Failed save: apply stunned
                    context.BonusStatusesToApply.Add("stunned");
                }

                // Mark as used this turn
                context.Attacker.ActionBudget?.UsedOncePerTurnFeatures.Add("stunning_strike");
                return true;
            });
        }

        /// <summary>
        /// Register the Horde Breaker trigger (Ranger: Hunter L3).
        /// Once per turn, when you hit with a weapon attack, grant a bonus action attack.
        /// In BG3, Horde Breaker allows an extra attack against a different enemy near the target.
        /// </summary>
        public static void RegisterHordeBreaker(OnHitTriggerService service)
        {
            service.RegisterTrigger("horde_breaker", OnHitTriggerType.OnHitConfirmed, (context) =>
            {
                if (context.Attacker == null || context.Target == null)
                    return false;

                // Only weapon attacks
                if (context.AttackType != AttackType.MeleeWeapon && context.AttackType != AttackType.RangedWeapon)
                    return false;

                // Check if attacker has Horde Breaker feature
                bool hasFeature = context.Attacker.ResolvedCharacter?.Features?.Any(f =>
                    string.Equals(f.Id, "horde_breaker", StringComparison.OrdinalIgnoreCase)) == true;
                if (!hasFeature)
                    return false;

                // Once per turn
                if (context.Attacker.ActionBudget?.UsedOncePerTurnFeatures.Contains("horde_breaker") == true)
                    return false;

                // Grant a bonus action for the extra attack
                context.Attacker.ActionBudget?.GrantAdditionalBonusAction(1);

                // Mark as used this turn
                context.Attacker.ActionBudget?.UsedOncePerTurnFeatures.Add("horde_breaker");
                return true;
            });
        }

        private static bool GrantGWMBonusAction(OnHitContext context)
        {
            if (context.Attacker == null || context.Attacker.ActionBudget == null)
                return false;

            // Check if attacker has GWM or Sharpshooter feat
            bool hasGWM = context.Attacker.ResolvedCharacter?.Sheet?.FeatIds?
                .Any(f => string.Equals(f, "great_weapon_master", StringComparison.OrdinalIgnoreCase)) == true;
            
            bool hasSharpshooter = context.Attacker.ResolvedCharacter?.Sheet?.FeatIds?
                .Any(f => string.Equals(f, "sharpshooter", StringComparison.OrdinalIgnoreCase)) == true;

            if (!hasGWM && !hasSharpshooter)
                return false;

            // Check if attack was a weapon attack
            bool isWeaponAttack = context.AttackType == AttackType.MeleeWeapon ||
                                 context.AttackType == AttackType.RangedWeapon;

            if (!isWeaponAttack)
                return false;

            // Grant bonus action
            context.Attacker.ActionBudget.GrantAdditionalBonusAction(1);

            return true;
        }
    }
}
