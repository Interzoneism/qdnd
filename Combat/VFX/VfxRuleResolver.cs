using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Combat.Services;

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

            string presetId = ResolvePresetId(request);
            var preset = _config.GetPresetOrNull(presetId);

            if (preset == null)
            {
                presetId = ResolveFallbackPresetId(request.Phase);
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
                    MatchesOptional(x.Rule.Intent, request.Intent?.ToString()))
                .OrderByDescending(x => GetSpecificityScore(x.Rule))
                .ThenBy(x => x.Index)
                .ToList();

            if (candidates.Count > 0)
                return candidates[0].Rule.PresetId;

            // 5. fallbackRule.
            return ResolveFallbackPresetId(request.Phase);
        }

        private string ResolveFallbackPresetId(VfxEventPhase phase)
        {
            if (_config.Rules.FallbackRule.TryGetValue(phase.ToString(), out var fallback))
                return fallback;

            if (_config.Rules.FallbackRule.TryGetValue("Impact", out fallback))
                return fallback;

            return _config.Presets.Keys.FirstOrDefault();
        }

        private static int GetSpecificityScore(VfxRuleDefinition rule)
        {
            int score = 0;
            if (!IsWildcard(rule.AttackType)) score++;
            if (!IsWildcard(rule.TargetType)) score++;
            if (!IsWildcard(rule.DamageType)) score++;
            if (!IsWildcard(rule.Intent)) score++;
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

        private static bool Matches(string lhs, string rhs)
            => string.Equals(lhs?.Trim(), rhs?.Trim(), StringComparison.OrdinalIgnoreCase);

        private static bool IsWildcard(string value)
            => string.IsNullOrWhiteSpace(value) || value.Trim() == "*";
    }
}
