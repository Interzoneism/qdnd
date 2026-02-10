using System;
using System.Linq;
using QDND.Combat.Statuses;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Built-in on-hit trigger implementations.
    /// </summary>
    public static class OnHitTriggers
    {
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
                if (context.AttackType != Abilities.AttackType.MeleeWeapon)
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
            service.RegisterTrigger("hex", OnHitTriggerType.OnHitConfirmed, (context) =>
            {
                if (statuses == null || context.Attacker == null || context.Target == null)
                    return false;

                // Check if target has hex status applied by this attacker
                var hexStatus = statuses.GetStatuses(context.Target.Id)
                    .FirstOrDefault(s => s.Definition.Id == "hex" && s.SourceId == context.Attacker.Id);

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
                if (context.AttackType != Abilities.AttackType.MeleeWeapon && 
                    context.AttackType != Abilities.AttackType.RangedWeapon)
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
            bool isWeaponAttack = context.AttackType == Abilities.AttackType.MeleeWeapon ||
                                 context.AttackType == Abilities.AttackType.RangedWeapon;

            if (!isWeaponAttack)
                return false;

            // Grant bonus action
            context.Attacker.ActionBudget.GrantAdditionalBonusAction(1);

            return true;
        }
    }
}
