using System;
using System.Collections.Generic;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Services;
using QDND.Combat.VFX;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Unit
{
    public class VfxRuleResolverTests
    {
        [Fact]
        public void Resolve_RequestPresetOverride_WinsAll()
        {
            var resolver = new VfxRuleResolver(BuildBundle());
            var request = new VfxRequest("corr", VfxEventPhase.Impact)
            {
                PresetId = "status_heal",
                ActionId = "Projectile_Fireball",
                AttackType = AttackType.RangedSpell,
                DamageType = DamageType.Fire
            };

            var resolved = resolver.Resolve(request);

            Assert.Equal("status_heal", resolved.PresetId);
        }

        [Fact]
        public void Resolve_ActionVariantOverride_BeatsActionPhaseOverride()
        {
            var resolver = new VfxRuleResolver(BuildBundle());
            var request = new VfxRequest("corr", VfxEventPhase.Impact)
            {
                ActionId = "Shout_HealingWord",
                VariantId = "mass_heal",
                AttackType = AttackType.RangedSpell,
                TargetType = TargetType.Circle
            };

            var resolved = resolver.Resolve(request);

            Assert.Equal("area_circle_blast", resolved.PresetId);
        }

        [Fact]
        public void Resolve_ActionPhaseOverride_BeatsDefaultRule()
        {
            var resolver = new VfxRuleResolver(BuildBundle());
            var request = new VfxRequest("corr", VfxEventPhase.Projectile)
            {
                ActionId = "Projectile_Fireball",
                AttackType = AttackType.RangedSpell,
                DamageType = DamageType.Fire
            };

            var resolved = resolver.Resolve(request);

            Assert.Equal("proj_fire", resolved.PresetId);
        }

        [Fact]
        public void Resolve_DefaultRule_UsesBestSpecificity()
        {
            var resolver = new VfxRuleResolver(BuildBundle());
            var request = new VfxRequest("corr", VfxEventPhase.Impact)
            {
                AttackType = AttackType.RangedSpell,
                TargetType = TargetType.SingleUnit,
                DamageType = DamageType.Lightning
            };

            var resolved = resolver.Resolve(request);

            Assert.Equal("impact_lightning", resolved.PresetId);
        }

        [Fact]
        public void Resolve_NoMatchingRule_UsesPhaseFallback()
        {
            var resolver = new VfxRuleResolver(BuildBundle());
            var request = new VfxRequest("corr", VfxEventPhase.Custom)
            {
                AttackType = AttackType.MeleeWeapon,
                TargetType = TargetType.Point
            };

            var resolved = resolver.Resolve(request);

            Assert.Equal("impact_physical", resolved.PresetId);
        }

        private static VfxConfigBundle BuildBundle()
        {
            var presets = new Dictionary<string, VfxPresetDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["proj_fire"] = new() { Id = "proj_fire", ParticleRecipe = "proj_fire" },
                ["proj_arcane_generic"] = new() { Id = "proj_arcane_generic", ParticleRecipe = "proj_arcane_generic" },
                ["impact_fire"] = new() { Id = "impact_fire", ParticleRecipe = "impact_fire" },
                ["impact_lightning"] = new() { Id = "impact_lightning", ParticleRecipe = "impact_lightning" },
                ["impact_physical"] = new() { Id = "impact_physical", ParticleRecipe = "impact_physical" },
                ["status_heal"] = new() { Id = "status_heal", ParticleRecipe = "status_heal" },
                ["area_circle_blast"] = new() { Id = "area_circle_blast", ParticleRecipe = "area_circle_blast" }
            };

            var rules = new VfxRulesFile
            {
                DefaultRules = new List<VfxRuleDefinition>
                {
                    new() { Phase = "Projectile", AttackType = "RangedSpell", PresetId = "proj_arcane_generic" },
                    new() { Phase = "Impact", DamageType = "Fire", PresetId = "impact_fire" },
                    new() { Phase = "Impact", DamageType = "Lightning", PresetId = "impact_lightning" },
                    new() { Phase = "Impact", PresetId = "impact_physical" }
                },
                ActionOverrides = new List<VfxActionOverrideRule>
                {
                    new() { ActionId = "Projectile_Fireball", Phase = "Projectile", PresetId = "proj_fire" },
                    new() { ActionId = "Shout_HealingWord", Phase = "Impact", PresetId = "status_heal" },
                    new() { ActionId = "Shout_HealingWord", VariantId = "mass_heal", Phase = "Impact", PresetId = "area_circle_blast" }
                },
                FallbackRule = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Custom"] = "impact_physical"
                }
            };

            return new VfxConfigBundle
            {
                ActiveCap = 48,
                InitialPoolSize = 12,
                Presets = presets,
                Rules = rules
            };
        }
    }
}
