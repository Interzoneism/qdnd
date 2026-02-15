using System;
using System.Collections.Generic;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Data.CharacterModel;
using Xunit;

namespace QDND.Tests.Unit
{
    public class PassiveRuleServiceTests
    {
        [Fact]
        public void Dueling_FromFeature_GrantsFlatDamageBonusOnBeforeDamage()
        {
            var rules = new RulesEngine(42);
            var statuses = new StatusManager(rules);
            var definitions = new List<PassiveRuleDefinition>
            {
                new PassiveRuleDefinition
                {
                    Id = "fighting_style_dueling",
                    ProviderType = "dueling_damage_bonus",
                    Selector = new PassiveRuleSelector
                    {
                        FeatureIds = new List<string> { "fighting_style" }
                    }
                }
            };

            var attacker = CreateCombatant(
                "attacker",
                features: new List<Feature>
                {
                    new Feature { Id = "fighting_style", IsPassive = true }
                },
                featIds: new List<string>());
            var target = CreateCombatant("target", new List<Feature>(), new List<string>());
            var combatants = new List<Combatant> { attacker, target };

            var service = new PassiveRuleService(rules, statuses, () => combatants, definitions);
            service.RebuildForCombatants(combatants);

            var ctx = new RuleEventContext
            {
                Source = attacker,
                Target = target,
                Ability = CreateMeleeWeaponAction(),
                IsMeleeWeaponAttack = true,
                DamageDiceFormula = "1d8",
                DamageRollValue = 5,
                Random = new Random(123)
            };

            rules.RuleWindows.Dispatch(RuleWindow.BeforeDamage, ctx);

            Assert.Equal(7, ctx.GetFinalDamageValue());
            service.Dispose();
        }

        [Fact]
        public void SavageAttacker_FromFeat_RerollsAndKeepsHigherDamageRoll()
        {
            var rules = new RulesEngine(42);
            var statuses = new StatusManager(rules);
            var definitions = new List<PassiveRuleDefinition>
            {
                new PassiveRuleDefinition
                {
                    Id = "savage_attacker",
                    ProviderType = "savage_attacker_reroll",
                    Selector = new PassiveRuleSelector
                    {
                        FeatIds = new List<string> { "savage_attacker" }
                    }
                }
            };

            var attacker = CreateCombatant("attacker", new List<Feature>(), new List<string> { "savage_attacker" });
            var target = CreateCombatant("target", new List<Feature>(), new List<string>());
            var combatants = new List<Combatant> { attacker, target };

            var service = new PassiveRuleService(rules, statuses, () => combatants, definitions);
            service.RebuildForCombatants(combatants);

            var ctx = new RuleEventContext
            {
                Source = attacker,
                Target = target,
                Ability = CreateMeleeWeaponAction(),
                IsMeleeWeaponAttack = true,
                DamageDiceFormula = "1d8",
                DamageRollValue = 1,
                Random = new MaxRollRandom()
            };

            rules.RuleWindows.Dispatch(RuleWindow.BeforeDamage, ctx);

            Assert.Equal(8, ctx.GetFinalDamageValue());
            service.Dispose();
        }

        private static Combatant CreateCombatant(string id, List<Feature> features, List<string> featIds)
        {
            var combatant = new Combatant(id, id, Faction.Player, 50, 10)
            {
                Stats = new CombatantStats
                {
                    Strength = 16,
                    Dexterity = 12,
                    Constitution = 14,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 12,
                    BaseAC = 10,
                    Speed = 9f
                },
                MainHandWeapon = new WeaponDefinition
                {
                    Id = "longsword",
                    Name = "Longsword",
                    DamageDiceCount = 1,
                    DamageDieFaces = 8,
                    DamageType = DamageType.Slashing,
                    Properties = WeaponProperty.Versatile
                },
                ResolvedCharacter = new ResolvedCharacter
                {
                    Sheet = new CharacterSheet
                    {
                        FeatIds = featIds ?? new List<string>()
                    },
                    Features = features ?? new List<Feature>(),
                    DamageResistances = new HashSet<DamageType>(),
                    DamageImmunities = new HashSet<DamageType>(),
                    ConditionImmunities = new HashSet<string>()
                }
            };

            return combatant;
        }

        private static ActionDefinition CreateMeleeWeaponAction()
        {
            return new ActionDefinition
            {
                Id = "Target_MainHandAttack",
                Name = "Basic Attack",
                AttackType = AttackType.MeleeWeapon,
                Tags = new HashSet<string> { "weapon_attack" }
            };
        }

        private sealed class MaxRollRandom : Random
        {
            public override int Next(int minValue, int maxValue)
            {
                return maxValue - 1;
            }
        }
    }
}
