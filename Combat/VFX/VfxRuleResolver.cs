using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Combat.Services;
using QDND.Data.CharacterModel;

namespace QDND.Combat.VFX
{
    public sealed class VfxRuleResolver : IVfxRuleResolver
    {
        private readonly VfxConfigBundle _config;

        public VfxRuleResolver(VfxConfigBundle config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public VfxResolvedSpec Resolve(VfxRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            string resolvedPresetId = ResolvePresetId(request);
            string presetId = resolvedPresetId;
            var preset = _config.GetPresetOrNull(presetId);

            if (preset == null)
            {
                presetId = ResolveGeneralizedPresetId(request, resolvedPresetId);
                preset = _config.GetPresetOrNull(presetId);
            }

            if (preset == null)
            {
                presetId = ResolveFallbackPresetId(request);
                preset = _config.GetPresetOrNull(presetId) ?? _config.Presets.Values.FirstOrDefault();
            }

            if (preset == null)
                throw new InvalidOperationException("VFX resolver has no presets to resolve.");

            return new VfxResolvedSpec
            {
                PresetId = preset.Id,
                Preset = preset,
                Phase = request.Phase,
                Pattern = request.Pattern,
                DamageType = request.DamageType,
                IsCritical = request.IsCritical,
                DidKill = request.DidKill,
                Magnitude = request.Magnitude,
                Seed = request.Seed,
                SourcePosition = request.SourcePosition,
                TargetPosition = request.TargetPosition,
                CastPosition = request.CastPosition,
                Direction = request.Direction
            };
        }

        private string ResolvePresetId(VfxRequest request)
        {
            // 1. Explicit runtime override on request.
            if (!string.IsNullOrWhiteSpace(request.PresetId))
                return request.PresetId;

            // 2. actionOverrides exact match on actionId + variantId + phase.
            var actionId = request.ActionId;
            if (!string.IsNullOrWhiteSpace(actionId))
            {
                var exact = _config.Rules.ActionOverrides.FirstOrDefault(rule =>
                    Matches(rule.ActionId, actionId) &&
                    !string.IsNullOrWhiteSpace(rule.VariantId) &&
                    Matches(rule.VariantId, request.VariantId) &&
                    MatchesPhase(rule.Phase, request.Phase));

                if (exact != null)
                    return exact.PresetId;

                // 3. actionOverrides match on actionId + phase.
                var actionOnly = _config.Rules.ActionOverrides.FirstOrDefault(rule =>
                    Matches(rule.ActionId, actionId) &&
                    string.IsNullOrWhiteSpace(rule.VariantId) &&
                    MatchesPhase(rule.Phase, request.Phase));

                if (actionOnly != null)
                    return actionOnly.PresetId;
            }

            // 4. defaultRules best-specificity match.
            var candidates = _config.Rules.DefaultRules
                .Select((rule, index) => new { Rule = rule, Index = index })
                .Where(x =>
                    MatchesPhase(x.Rule.Phase, request.Phase) &&
                    MatchesOptional(x.Rule.AttackType, request.AttackType?.ToString()) &&
                    MatchesOptional(x.Rule.TargetType, request.TargetType?.ToString()) &&
                    MatchesOptional(x.Rule.DamageType, request.DamageType?.ToString()) &&
                    MatchesOptional(x.Rule.Intent, request.Intent?.ToString()) &&
                    MatchesOptionalBool(x.Rule.IsSpell, request.IsSpell))
                .OrderByDescending(x => GetSpecificityScore(x.Rule))
                .ThenBy(x => x.Index)
                .ToList();

            if (candidates.Count > 0)
                return candidates[0].Rule.PresetId;

            return null;
        }

        private string ResolveGeneralizedPresetId(VfxRequest request, string unresolvedPresetId)
        {
            if (request == null)
                return null;

            var hints = BuildResolutionHints(request, unresolvedPresetId);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var hint in hints)
            {
                if (string.IsNullOrWhiteSpace(hint))
                    continue;
                if (!visited.Add(hint.Trim()))
                    continue;

                var mapped = MapHintToPresetId(request, hint);
                if (!string.IsNullOrWhiteSpace(mapped) && _config.GetPresetOrNull(mapped) != null)
                    return mapped;
            }

            return null;
        }

        private static IEnumerable<string> BuildResolutionHints(VfxRequest request, string unresolvedPresetId)
        {
            if (!string.IsNullOrWhiteSpace(unresolvedPresetId))
                yield return unresolvedPresetId;
            if (!string.IsNullOrWhiteSpace(request?.VariantVfxId))
                yield return request.VariantVfxId;
            if (!string.IsNullOrWhiteSpace(request?.ActionVfxId))
                yield return request.ActionVfxId;
            if (!string.IsNullOrWhiteSpace(request?.SpellType))
                yield return request.SpellType;
        }

        private string MapHintToPresetId(VfxRequest request, string hint)
        {
            if (string.IsNullOrWhiteSpace(hint))
                return null;

            string trimmed = hint.Trim();
            if (_config.GetPresetOrNull(trimmed) != null)
                return trimmed;

            string normalized = NormalizeHint(trimmed);
            if (_config.GetPresetOrNull(normalized) != null)
                return normalized;

            if (normalized == "cast_start")
                return request.IsSpell ? ResolveSpellCastPresetId(request) : "cast_martial_generic";

            if (request.Phase == VfxEventPhase.Start && (request.IsSpell || IsSpellHint(normalized)))
                return ResolveSpellCastPresetId(request);

            if (request.Phase == VfxEventPhase.Projectile)
            {
                if (normalized is "projectile" or "projectilestrike" or "proj" or "throw")
                    return request.IsSpell || IsSpellHint(normalized) ? "proj_arcane_generic" : "proj_physical_generic";
            }

            if (request.Phase == VfxEventPhase.Area)
            {
                return request.TargetType switch
                {
                    TargetType.Cone => "area_cone_sweep",
                    TargetType.Line => "area_line_surge",
                    _ => "area_circle_blast"
                };
            }

            if (request.Phase == VfxEventPhase.Heal)
                return "status_heal";

            if (request.Phase == VfxEventPhase.Status)
            {
                return request.Intent == VerbalIntent.Buff
                    ? "status_buff_apply"
                    : "status_debuff_apply";
            }

            if (request.Phase == VfxEventPhase.Death)
                return "status_death_burst";

            if (request.Phase == VfxEventPhase.Impact)
            {
                if (request.DamageType.HasValue)
                    return ResolveDamageImpactPresetId(request.DamageType.Value);
                return "impact_physical";
            }

            if (request.Phase == VfxEventPhase.Custom && request.IsSpell)
                return ResolveSpellCastPresetId(request);

            return null;
        }

        private string ResolveFallbackPresetId(VfxRequest request)
        {
            if (request != null && request.IsSpell)
            {
                var spellFallback = ResolveSpellFallbackPresetId(request);
                if (!string.IsNullOrWhiteSpace(spellFallback))
                    return spellFallback;
            }

            var phase = request?.Phase ?? VfxEventPhase.Impact;
            if (_config.Rules.FallbackRule.TryGetValue(phase.ToString(), out var fallback))
                return fallback;

            if (_config.Rules.FallbackRule.TryGetValue("Impact", out fallback))
                return fallback;

            return _config.Presets.Keys.FirstOrDefault();
        }

        private string ResolveSpellFallbackPresetId(VfxRequest request)
        {
            switch (request.Phase)
            {
                case VfxEventPhase.Start:
                case VfxEventPhase.Custom:
                    return ResolveSpellCastPresetId(request);
                case VfxEventPhase.Projectile:
                    return "proj_arcane_generic";
                case VfxEventPhase.Area:
                    return request.TargetType switch
                    {
                        TargetType.Cone => "area_cone_sweep",
                        TargetType.Line => "area_line_surge",
                        _ => "area_circle_blast"
                    };
                case VfxEventPhase.Heal:
                    return "status_heal";
                case VfxEventPhase.Status:
                    return request.Intent == VerbalIntent.Buff
                        ? "status_buff_apply"
                        : "status_debuff_apply";
                case VfxEventPhase.Death:
                    return "status_death_burst";
                case VfxEventPhase.Impact:
                    return request.DamageType.HasValue
                        ? ResolveDamageImpactPresetId(request.DamageType.Value)
                        : "impact_force";
                default:
                    return null;
            }
        }

        private static string ResolveSpellCastPresetId(VfxRequest request)
            => request?.Intent == VerbalIntent.Healing ? "cast_divine_generic" : "cast_arcane_generic";

        private static string ResolveDamageImpactPresetId(DamageType damageType)
        {
            return damageType switch
            {
                DamageType.Fire => "impact_fire",
                DamageType.Cold => "impact_cold",
                DamageType.Lightning => "impact_lightning",
                DamageType.Thunder => "impact_lightning",
                DamageType.Poison => "impact_poison",
                DamageType.Acid => "impact_acid",
                DamageType.Necrotic => "impact_necrotic",
                DamageType.Radiant => "impact_radiant",
                DamageType.Force => "impact_force",
                DamageType.Psychic => "impact_psychic",
                _ => "impact_physical"
            };
        }

        private static bool IsSpellHint(string normalized)
        {
            return normalized is
                "spell" or
                "cantrip" or
                "magic" or
                "projectile" or
                "projectilestrike" or
                "target" or
                "multicast" or
                "shout" or
                "zone" or
                "cone" or
                "wall" or
                "rush" or
                "teleportation";
        }

        private static string NormalizeHint(string value)
            => value.Trim().Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant();

        private static int GetSpecificityScore(VfxRuleDefinition rule)
        {
            int score = 0;
            if (!IsWildcard(rule.AttackType)) score++;
            if (!IsWildcard(rule.TargetType)) score++;
            if (!IsWildcard(rule.DamageType)) score++;
            if (!IsWildcard(rule.Intent)) score++;
            if (rule.IsSpell.HasValue) score++;
            return score;
        }

        private static bool MatchesOptional(string ruleValue, string requestValue)
        {
            if (IsWildcard(ruleValue))
                return true;

            return Matches(ruleValue, requestValue);
        }

        private static bool MatchesPhase(string rulePhase, VfxEventPhase phase)
            => MatchesOptional(rulePhase, phase.ToString());

        private static bool MatchesOptionalBool(bool? ruleValue, bool requestValue)
        {
            if (!ruleValue.HasValue)
                return true;
            return ruleValue.Value == requestValue;
        }

        private static bool Matches(string lhs, string rhs)
            => string.Equals(lhs?.Trim(), rhs?.Trim(), StringComparison.OrdinalIgnoreCase);

        private static bool IsWildcard(string value)
            => string.IsNullOrWhiteSpace(value) || value.Trim() == "*";
    }
}
