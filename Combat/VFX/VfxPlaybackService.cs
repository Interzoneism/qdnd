using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using QDND.Combat.Arena;
using QDND.Combat.Services;

namespace QDND.Combat.VFX
{
    public sealed class VfxPlaybackService : IVfxPlaybackService
    {
        private readonly PresentationRequestBus _presentationBus;
        private readonly CombatVFXManager _vfxManager;
        private readonly ICombatContext _combatContext;
        private readonly IVfxRuleResolver _resolver;
        private readonly float _tileSize;

        public VfxPlaybackService(
            PresentationRequestBus presentationBus,
            CombatVFXManager vfxManager,
            ICombatContext combatContext,
            float tileSize,
            IVfxRuleResolver resolver)
        {
            _presentationBus = presentationBus ?? throw new ArgumentNullException(nameof(presentationBus));
            _vfxManager = vfxManager ?? throw new ArgumentNullException(nameof(vfxManager));
            _combatContext = combatContext;
            _tileSize = tileSize <= 0f ? 1f : tileSize;
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

            _presentationBus.OnRequestPublished += OnRequestPublished;
        }

        public void Handle(VfxRequest request)
        {
            var resolved = _resolver.Resolve(request);
            var targets = ResolveTargetPositions(request);
            var sampled = VfxPatternSampler.Sample(request, resolved.Preset, targets);

            var worldSampled = sampled.Select(ToWorld).ToList();
            var worldSpec = new VfxResolvedSpec
            {
                PresetId = resolved.PresetId,
                Preset = resolved.Preset,
                Phase = resolved.Phase,
                Pattern = resolved.Pattern,
                DamageType = resolved.DamageType,
                IsCritical = resolved.IsCritical,
                DidKill = resolved.DidKill,
                Magnitude = resolved.Magnitude,
                Seed = resolved.Seed,
                SourcePosition = resolved.SourcePosition.HasValue ? ToWorld(resolved.SourcePosition.Value) : null,
                TargetPosition = resolved.TargetPosition.HasValue ? ToWorld(resolved.TargetPosition.Value) : null,
                CastPosition = resolved.CastPosition.HasValue ? ToWorld(resolved.CastPosition.Value) : null,
                Direction = resolved.Direction,
                EmissionPoints = worldSampled
            };

            _vfxManager.Spawn(worldSpec);
        }

        private void OnRequestPublished(PresentationRequest request)
        {
            if (request is VfxRequest vfxRequest)
                Handle(vfxRequest);
        }

        private List<Vector3> ResolveTargetPositions(VfxRequest request)
        {
            var resolved = new List<Vector3>();

            if (request.TargetPositions != null)
            {
                resolved.AddRange(request.TargetPositions);
            }

            if (resolved.Count == 0 && request.TargetIds != null && _combatContext != null)
            {
                foreach (var targetId in request.TargetIds)
                {
                    if (string.IsNullOrWhiteSpace(targetId))
                        continue;

                    var combatant = _combatContext.GetCombatant(targetId);
                    if (combatant == null)
                        continue;

                    resolved.Add(new Vector3(combatant.Position.X, combatant.Position.Y, combatant.Position.Z));
                }
            }

            if (resolved.Count == 0 && request.TargetPosition.HasValue)
            {
                resolved.Add(request.TargetPosition.Value);
            }

            return resolved;
        }

        private Vector3 ToWorld(Vector3 grid)
            => new(grid.X * _tileSize, grid.Y, grid.Z * _tileSize);
    }
}
