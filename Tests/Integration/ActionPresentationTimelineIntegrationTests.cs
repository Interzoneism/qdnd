using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Animation;
using QDND.Combat.Arena;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Services;
using QDND.Combat.Statuses;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Integration tests for timeline-driven presentation during ability execution.
    /// Verifies that timelines are created and presentation requests are emitted
    /// at correct marker times in a headless environment.
    /// </summary>
    public class ActionPresentationTimelineIntegrationTests
    {
        private readonly RulesEngine _rules;
        private readonly EffectPipeline _pipeline;
        private readonly PresentationRequestBus _bus;
        private readonly List<Combatant> _combatants;

        public ActionPresentationTimelineIntegrationTests()
        {
            _rules = new RulesEngine(42);
            _pipeline = new EffectPipeline
            {
                Rules = _rules,
                Statuses = new StatusManager(_rules),
                Rng = new Random(42)
            };
            _bus = new PresentationRequestBus();
            _combatants = new List<Combatant>();
        }

        [Fact]
        public void MeleeAttack_EmitsCorrectPresentationRequests()
        {
            // Arrange
            var action = new ActionDefinition
            {
                Id = "melee_test",
                Name = "Melee Test",
                AttackType = AttackType.MeleeWeapon,
                VfxId = "vfx_slash",
                SfxId = "sfx_sword",
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "slashing" }
                }
            };
            _pipeline.RegisterAction(action);

            var attacker = CreateTestCombatant("attacker");
            var target = CreateTestCombatant("target");

            var requests = new List<PresentationRequest>();
            var markerOrder = new List<MarkerType>();
            _bus.OnRequestPublished += req => requests.Add(req);

            // Act
            var timeline = CreateTimelineForAbility(action, attacker, target);

            // Subscribe to timeline.MarkerTriggered
            timeline.MarkerTriggered += (markerId, markerType) =>
            {
                markerOrder.Add(markerType);
                var marker = timeline.Markers.FirstOrDefault(m => m.Id == markerId);
                if (marker != null)
                {
                    EmitPresentationRequestsForMarker(marker, action, attacker, target);
                }
            };

            timeline.Play();

            // Process timeline to completion
            while (timeline.State == TimelineState.Playing)
            {
                timeline.Process(0.1f);
            }

            // Assert timeline completed
            Assert.Equal(TimelineState.Completed, timeline.State);

            // Verify marker trigger order: Start → Hit → AnimationEnd
            Assert.Contains(MarkerType.Start, markerOrder);
            Assert.Contains(MarkerType.Hit, markerOrder);
            Assert.Contains(MarkerType.AnimationEnd, markerOrder);
            var startIdx = markerOrder.IndexOf(MarkerType.Start);
            var hitIdx = markerOrder.IndexOf(MarkerType.Hit);
            var endIdx = markerOrder.IndexOf(MarkerType.AnimationEnd);
            Assert.True(startIdx < hitIdx, "Start should trigger before Hit");
            Assert.True(hitIdx < endIdx, "Hit should trigger before AnimationEnd");

            // Verify presentation requests
            Assert.NotEmpty(requests);

            // Should have camera focus on attacker at start
            var cameraFocusRequests = requests.OfType<CameraFocusRequest>().ToList();
            Assert.NotEmpty(cameraFocusRequests);

            // Should have VFX/SFX requests
            if (!string.IsNullOrEmpty(action.VfxId))
            {
                var vfxRequests = requests.OfType<VfxRequest>().ToList();
                Assert.NotEmpty(vfxRequests);
                Assert.Contains(vfxRequests, r => r.EffectId == action.VfxId);
            }

            if (!string.IsNullOrEmpty(action.SfxId))
            {
                var sfxRequests = requests.OfType<SfxRequest>().ToList();
                Assert.NotEmpty(sfxRequests);
                Assert.Contains(sfxRequests, r => r.SoundId == action.SfxId);
            }

            // Should have camera release at end
            var cameraReleaseRequests = requests.OfType<CameraReleaseRequest>().ToList();
            Assert.NotEmpty(cameraReleaseRequests);
        }

        [Fact]
        public void RangedAttack_EmitsVFXAtProjectileAndHitMarker()
        {
            // Arrange
            var action = new ActionDefinition
            {
                Id = "ranged_test",
                Name = "Ranged Test",
                AttackType = AttackType.RangedWeapon,
                VfxId = "vfx_arrow",
                SfxId = "sfx_bow",
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 8, DamageType = "piercing" }
                }
            };
            _pipeline.RegisterAction(action);

            var attacker = CreateTestCombatant("attacker");
            var target = CreateTestCombatant("target");

            var requests = new List<PresentationRequest>();
            var markerOrder = new List<MarkerType>();
            _bus.OnRequestPublished += req => requests.Add(req);

            // Act
            var timeline = CreateTimelineForAbility(action, attacker, target);

            // Subscribe to timeline.MarkerTriggered
            timeline.MarkerTriggered += (markerId, markerType) =>
            {
                markerOrder.Add(markerType);
                var marker = timeline.Markers.FirstOrDefault(m => m.Id == markerId);
                if (marker != null)
                {
                    // Specifically test projectile marker emission
                    if (markerType == MarkerType.Projectile)
                    {
                        // Assert marker.Data is null/empty (proving fallback is used)
                        Assert.True(string.IsNullOrEmpty(marker.Data), "Projectile marker should have null/empty Data to test fallback");

                        // Track VfxRequest count before emission
                        int vfxCountBefore = requests.OfType<VfxRequest>().Count();

                        // Emit request
                        EmitPresentationRequestsForMarker(marker, action, attacker, target);

                        // Assert count increased
                        int vfxCountAfter = requests.OfType<VfxRequest>().Count();
                        Assert.True(vfxCountAfter > vfxCountBefore, "Projectile marker should emit VfxRequest");

                        // Assert the new VfxRequest has EffectId == action.VfxId
                        var newVfxRequest = requests.OfType<VfxRequest>().Last();
                        Assert.Equal(action.VfxId, newVfxRequest.EffectId);
                    }
                    else
                    {
                        EmitPresentationRequestsForMarker(marker, action, attacker, target);
                    }
                }
            };

            timeline.Play();

            while (timeline.State == TimelineState.Playing)
            {
                timeline.Process(0.1f);
            }

            // Assert
            Assert.Equal(TimelineState.Completed, timeline.State);

            // Verify marker trigger order: Start → Projectile → Hit → AnimationEnd
            Assert.Contains(MarkerType.Start, markerOrder);
            Assert.Contains(MarkerType.Projectile, markerOrder);
            Assert.Contains(MarkerType.Hit, markerOrder);
            Assert.Contains(MarkerType.AnimationEnd, markerOrder);
            var startIdx = markerOrder.IndexOf(MarkerType.Start);
            var projectileIdx = markerOrder.IndexOf(MarkerType.Projectile);
            var hitIdx = markerOrder.IndexOf(MarkerType.Hit);
            var endIdx = markerOrder.IndexOf(MarkerType.AnimationEnd);
            Assert.True(startIdx < projectileIdx, "Start should trigger before Projectile");
            Assert.True(projectileIdx < hitIdx, "Projectile should trigger before Hit");
            Assert.True(hitIdx < endIdx, "Hit should trigger before AnimationEnd");
        }

        [Fact]
        public void SpellCast_EmitsVFXAndSFXAtStart()
        {
            // Arrange
            var action = new ActionDefinition
            {
                Id = "spell_test",
                Name = "Spell Test",
                AttackType = AttackType.RangedSpell,
                VfxId = "vfx_fireball",
                SfxId = "sfx_fireball",
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 20, DamageType = "fire" }
                }
            };
            _pipeline.RegisterAction(action);

            var attacker = CreateTestCombatant("caster");
            var target = CreateTestCombatant("target");

            var requests = new List<PresentationRequest>();
            var markerOrder = new List<MarkerType>();
            _bus.OnRequestPublished += req => requests.Add(req);

            // Act
            var timeline = CreateTimelineForAbility(action, attacker, target);

            // Subscribe to timeline.MarkerTriggered
            timeline.MarkerTriggered += (markerId, markerType) =>
            {
                markerOrder.Add(markerType);
                var marker = timeline.Markers.FirstOrDefault(m => m.Id == markerId);
                if (marker != null)
                {
                    EmitPresentationRequestsForMarker(marker, action, attacker, target);
                }
            };

            timeline.Play();

            while (timeline.State == TimelineState.Playing)
            {
                timeline.Process(0.1f);
            }

            // Assert
            Assert.Equal(TimelineState.Completed, timeline.State);

            // Verify marker trigger order: Start → VFX → Sound → Hit → AnimationEnd
            Assert.Contains(MarkerType.Start, markerOrder);
            Assert.Contains(MarkerType.VFX, markerOrder);
            Assert.Contains(MarkerType.Sound, markerOrder);
            Assert.Contains(MarkerType.Hit, markerOrder);
            Assert.Contains(MarkerType.AnimationEnd, markerOrder);
            var startIdx = markerOrder.IndexOf(MarkerType.Start);
            var hitIdx = markerOrder.IndexOf(MarkerType.Hit);
            var endIdx = markerOrder.IndexOf(MarkerType.AnimationEnd);
            Assert.True(startIdx < hitIdx, "Start should trigger before Hit");
            Assert.True(hitIdx < endIdx, "Hit should trigger before AnimationEnd");

            // VFX and Sound should trigger at time 0 (same as or right after Start)
            var vfxMarker = timeline.Markers.First(m => m.Type == MarkerType.VFX);
            var sfxMarker = timeline.Markers.First(m => m.Type == MarkerType.Sound);
            Assert.Equal(0f, vfxMarker.Time);
            Assert.Equal(0f, sfxMarker.Time);
        }

        [Fact]
        public void ExecuteAbility_GameplayResolutionIsImmediate()
        {
            // Arrange
            var action = new ActionDefinition
            {
                Id = "damage_test",
                Name = "Damage Test",
                AttackType = AttackType.MeleeWeapon,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 15 }
                }
            };
            _pipeline.RegisterAction(action);

            var attacker = CreateTestCombatant("attacker");
            var target = CreateTestCombatant("target");
            var initialHp = target.Resources.CurrentHP;

            // Act - Execute ability (gameplay resolution)
            var result = _pipeline.ExecuteAction(action.Id, attacker, new List<Combatant> { target });
            var hpAfterExecution = target.Resources.CurrentHP;

            // Create timeline (presentation scheduling)
            var timeline = CreateTimelineForAbility(action, attacker, target);
            timeline.Play();

            // Assert - Damage is applied immediately, before timeline starts
            Assert.True(result.Success);
            Assert.True(hpAfterExecution < initialHp);
            Assert.Equal(TimelineState.Playing, timeline.State);

            // Timeline is for presentation only
            timeline.Process(1.0f);
            Assert.Equal(hpAfterExecution, target.Resources.CurrentHP);
        }

        [Fact]
        public void MultipleAbilities_TimelinesCanRunConcurrently()
        {
            // Arrange
            var ability1 = new ActionDefinition
            {
                Id = "ability1",
                Name = "Ability 1",
                AttackType = AttackType.MeleeWeapon,
                VfxId = "vfx1"
            };
            var ability2 = new ActionDefinition
            {
                Id = "ability2",
                Name = "Ability 2",
                AttackType = AttackType.RangedWeapon,
                VfxId = "vfx2"
            };

            _pipeline.RegisterAction(ability1);
            _pipeline.RegisterAction(ability2);

            var attacker1 = CreateTestCombatant("attacker1");
            var attacker2 = CreateTestCombatant("attacker2");
            var target = CreateTestCombatant("target");

            // Act
            var timeline1 = CreateTimelineForAbility(ability1, attacker1, target);
            var timeline2 = CreateTimelineForAbility(ability2, attacker2, target);

            timeline1.Play();
            timeline2.Play();

            // Process both timelines
            int iterations = 0;
            while ((timeline1.State == TimelineState.Playing || timeline2.State == TimelineState.Playing) && iterations < 100)
            {
                timeline1.Process(0.1f);
                timeline2.Process(0.1f);
                iterations++;
            }

            // Assert
            Assert.Equal(TimelineState.Completed, timeline1.State);
            Assert.Equal(TimelineState.Completed, timeline2.State);
            Assert.True(iterations < 100);
        }

        // Helper methods

        private Combatant CreateTestCombatant(string id)
        {
            var combatant = new Combatant(id, id, Faction.Player, 100, 10);
            combatant.Position = Godot.Vector3.Zero;
            _combatants.Add(combatant);
            return combatant;
        }

        private ActionTimeline CreateTimelineForAbility(ActionDefinition action, Combatant attacker, Combatant target)
        {
            ActionTimeline timeline;

            // Create timeline based on ability type
            if (action.AttackType == AttackType.MeleeWeapon || action.AttackType == AttackType.MeleeSpell)
            {
                timeline = ActionTimeline.MeleeAttack(() => { }, 0.3f, 0.6f);
            }
            else if (action.AttackType == AttackType.RangedWeapon)
            {
                timeline = ActionTimeline.RangedAttack(() => { }, () => { }, 0.2f, 0.5f);
            }
            else if (action.AttackType == AttackType.RangedSpell)
            {
                timeline = ActionTimeline.SpellCast(() => { }, 1.0f, 1.2f);
            }
            else
            {
                // Default timeline
                timeline = new ActionTimeline(action.Id)
                    .AddMarker(TimelineMarker.Start())
                    .AddMarker(TimelineMarker.Hit(0.3f))
                    .AddMarker(TimelineMarker.End(0.6f));
            }

            return timeline;
        }

        private void EmitPresentationRequestsForMarker(TimelineMarker marker, ActionDefinition action, Combatant attacker, Combatant target)
        {
            string correlationId = $"{action.Id}_{attacker.Id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

            switch (marker.Type)
            {
                case MarkerType.Start:
                case MarkerType.CameraFocus:
                    _bus.Publish(new CameraFocusRequest(correlationId, attacker.Id));
                    break;

                case MarkerType.Projectile:
                    // Emit VFX for projectile using marker.Data, fallback to action.VfxId
                    {
                        string vfxId = !string.IsNullOrEmpty(marker.Data) ? marker.Data : action.VfxId;
                        if (!string.IsNullOrEmpty(vfxId))
                        {
                            var actorPos = new System.Numerics.Vector3(attacker.Position.X, attacker.Position.Y, attacker.Position.Z);
                            _bus.Publish(new VfxRequest(correlationId, vfxId, actorPos, attacker.Id));
                        }
                    }
                    break;

                case MarkerType.Hit:
                    // Emit VFX/SFX at hit time
                    if (!string.IsNullOrEmpty(action.VfxId))
                    {
                        var targetPos = new System.Numerics.Vector3(target.Position.X, target.Position.Y, target.Position.Z);
                        _bus.Publish(new VfxRequest(correlationId, action.VfxId, targetPos, target.Id));
                    }
                    if (!string.IsNullOrEmpty(action.SfxId))
                    {
                        var targetPos = new System.Numerics.Vector3(target.Position.X, target.Position.Y, target.Position.Z);
                        _bus.Publish(new SfxRequest(correlationId, action.SfxId, targetPos));
                    }
                    // Focus camera on target during hit
                    _bus.Publish(new CameraFocusRequest(correlationId, target.Id));
                    break;

                case MarkerType.VFX:
                    if (!string.IsNullOrEmpty(marker.Data))
                    {
                        var pos = marker.Position ?? new Godot.Vector3(target.Position.X, target.Position.Y, target.Position.Z);
                        var numPos = new System.Numerics.Vector3(pos.X, pos.Y, pos.Z);
                        _bus.Publish(new VfxRequest(correlationId, marker.Data, numPos, marker.TargetId ?? target.Id));
                    }
                    else if (!string.IsNullOrEmpty(action.VfxId))
                    {
                        var targetPos = new System.Numerics.Vector3(target.Position.X, target.Position.Y, target.Position.Z);
                        _bus.Publish(new VfxRequest(correlationId, action.VfxId, targetPos, target.Id));
                    }
                    break;

                case MarkerType.Sound:
                    if (!string.IsNullOrEmpty(marker.Data))
                    {
                        _bus.Publish(new SfxRequest(correlationId, marker.Data));
                    }
                    else if (!string.IsNullOrEmpty(action.SfxId))
                    {
                        _bus.Publish(new SfxRequest(correlationId, action.SfxId));
                    }
                    break;

                case MarkerType.AnimationEnd:
                case MarkerType.CameraRelease:
                    _bus.Publish(new CameraReleaseRequest(correlationId));
                    break;
            }
        }
    }
}
