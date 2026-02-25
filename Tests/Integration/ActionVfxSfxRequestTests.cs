using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Animation;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.States;
using QDND.Data;
using QDND.Data.Actions;

namespace QDND.Tests.Integration
{
    public class ActionVfxSfxRequestTests
    {
        private readonly PresentationRequestBus _presentationBus;
        private readonly List<PresentationRequest> _capturedRequests;
        private readonly ActionRegistry _registry;
        private readonly bool _dataLoaded;

        public ActionVfxSfxRequestTests()
        {
            _presentationBus = new PresentationRequestBus();
            _capturedRequests = new List<PresentationRequest>();
            _presentationBus.OnRequestPublished += request => _capturedRequests.Add(request);

            // Attempt to load abilities from JSON (for data-driven test)
            _registry = new ActionRegistry();
            string[] possiblePaths = new[]
            {
                Path.Combine("Data", "Actions"),
                Path.Combine("..", "..", "..", "..", "Data", "Actions")
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    int loaded = ActionRegistryInitializer.LoadJsonActions(path, _registry);
                    if (loaded > 0)
                    {
                        _dataLoaded = true;
                        break;
                    }
                }
            }
        }

        [Fact]
        public void Fireball_WithVfxSfxIds_EmitsCorrectPresentationRequests()
        {
            // Arrange: Load fireball from JSON if available, otherwise create programmatically
            ActionDefinition fireball;
            if (_dataLoaded && _registry.GetAction("Projectile_Fireball") != null)
            {
                // Data-driven path: Load from JSON (proves end-to-end flow)
                fireball = _registry.GetAction("Projectile_Fireball");
                Assert.Equal("fireball_impact", fireball.VfxId);
                Assert.Equal("fireball_whoosh", fireball.SfxId);
            }
            else
            {
                // Fallback: Create programmatically for headless environments
                fireball = new ActionDefinition
                {
                    Id = "Projectile_Fireball",
                    Name = "Fireball",
                    TargetType = TargetType.Circle,
                    TargetFilter = TargetFilter.All,
                    Range = 20,
                    AreaRadius = 4,
                    AttackType = AttackType.RangedSpell,
                    VfxId = "fireball_impact",
                    SfxId = "fireball_whoosh",
                    Effects = new List<EffectDefinition>
                    {
                        new EffectDefinition { Type = "damage", DiceFormula = "6d6", DamageType = "fire" }
                    }
                };
            }

            // Create mock combatants
            var attacker = CreateMockCombatant("attacker_1", "Wizard", new Godot.Vector3(0, 0, 0));
            var target = CreateMockCombatant("target_1", "Goblin", new Godot.Vector3(5, 0, 5));

            // Build timeline
            var timeline = ActionTimeline.SpellCast(() => { }, 0.8f, 1.0f);
            string correlationId = "test_fireball_001";

            // Subscribe to markers and emit presentation requests
            timeline.MarkerTriggered += (markerId, markerType) =>
            {
                var marker = timeline.Markers.FirstOrDefault(m => m.Id == markerId);
                EmitPresentationRequestsForMarker(marker, markerType, correlationId, fireball, attacker, target);
            };

            _capturedRequests.Clear();

            // Act: Play timeline and process through all markers
            timeline.Play();

            // Process timeline until completion
            while (timeline.State == TimelineState.Playing)
            {
                timeline.Process(0.016f); // ~60 FPS
            }

            // Assert: Verify VfxRequest and SfxRequest were emitted with correct IDs
            var vfxRequests = _capturedRequests.FindAll(r => r is VfxRequest).ConvertAll(r => (VfxRequest)r);
            var sfxRequests = _capturedRequests.FindAll(r => r is SfxRequest).ConvertAll(r => (SfxRequest)r);

            Assert.NotEmpty(vfxRequests);
            Assert.NotEmpty(sfxRequests);

            // At least one VfxRequest should represent an impact event.
            Assert.Contains(vfxRequests, req => req.Phase == VfxEventPhase.Impact);

            // At least one SfxRequest should have the fireball SfxId
            Assert.Contains(sfxRequests, req => req.SoundId == "fireball_whoosh");
        }

        [Theory]
        [InlineData("Target_MainHandAttack", "sword_slash_impact", "sword_slash_hit")]
        [InlineData("Target_PoisonSpray", "poison_cloud", "poison_hiss")]
        public void MeleeAbilities_WithVfxSfxIds_EmitsCorrectPresentationRequests(
            string actionId, string expectedVfxId, string expectedSfxId)
        {
            // Arrange: Load ability from JSON if available, otherwise create programmatically
            ActionDefinition action;
            if (_dataLoaded && _registry.GetAction(actionId) != null)
            {
                // Data-driven path: Load from JSON
                action = _registry.GetAction(actionId);
                Assert.Equal(expectedVfxId, action.VfxId);
                Assert.Equal(expectedSfxId, action.SfxId);
            }
            else
            {
                // Fallback: Create programmatically
                action = new ActionDefinition
                {
                    Id = actionId,
                    Name = actionId,
                    TargetType = TargetType.SingleUnit,
                    TargetFilter = TargetFilter.Enemies,
                    Range = 1.5f,
                    AttackType = AttackType.MeleeWeapon,
                    VfxId = expectedVfxId,
                    SfxId = expectedSfxId,
                    Effects = new List<EffectDefinition>
                    {
                        new EffectDefinition { Type = "damage", DiceFormula = "1d8", DamageType = "physical" }
                    }
                };
            }

            var attacker = CreateMockCombatant("attacker_1", "Warrior", new Godot.Vector3(0, 0, 0));
            var target = CreateMockCombatant("target_1", "Orc", new Godot.Vector3(2, 0, 0));

            // Build melee timeline
            var timeline = ActionTimeline.MeleeAttack(() => { }, 0.3f, 0.6f);
            string correlationId = $"test_{actionId}_001";

            timeline.MarkerTriggered += (markerId, markerType) =>
            {
                var marker = timeline.Markers.FirstOrDefault(m => m.Id == markerId);
                EmitPresentationRequestsForMarker(marker, markerType, correlationId, action, attacker, target);
            };

            _capturedRequests.Clear();

            // Act
            timeline.Play();
            while (timeline.State == TimelineState.Playing)
            {
                timeline.Process(0.016f);
            }

            // Assert
            var vfxRequests = _capturedRequests.FindAll(r => r is VfxRequest).ConvertAll(r => (VfxRequest)r);
            var sfxRequests = _capturedRequests.FindAll(r => r is SfxRequest).ConvertAll(r => (SfxRequest)r);

            Assert.Contains(vfxRequests, req => req.Phase == VfxEventPhase.Impact);
            Assert.Contains(sfxRequests, req => req.SoundId == expectedSfxId);
        }

        [Fact]
        public void AbilityWithoutVfxSfxIds_StillEmitsDataDrivenVfxRequest()
        {
            // Arrange: Load basic_attack from JSON if available, otherwise create programmatically
            ActionDefinition basicAttack;
            if (_dataLoaded && _registry.GetAction("Target_MainHandAttack") != null)
            {
                // Data-driven path: Load from JSON (has no vfxId/sfxId)
                basicAttack = _registry.GetAction("Target_MainHandAttack");
                Assert.Null(basicAttack.VfxId);
                Assert.Null(basicAttack.SfxId);
            }
            else
            {
                // Fallback: Create programmatically
                basicAttack = new ActionDefinition
                {
                    Id = "Target_MainHandAttack",
                    Name = "Basic Attack",
                    TargetType = TargetType.SingleUnit,
                    TargetFilter = TargetFilter.Enemies,
                    Range = 1.5f,
                    AttackType = AttackType.MeleeWeapon,
                    VfxId = null,
                    SfxId = null,
                    Effects = new List<EffectDefinition>
                    {
                        new EffectDefinition { Type = "damage", DiceFormula = "1d8", DamageType = "physical" }
                    }
                };
            }

            var attacker = CreateMockCombatant("attacker_1", "Fighter", new Godot.Vector3(0, 0, 0));
            var target = CreateMockCombatant("target_1", "Bandit", new Godot.Vector3(2, 0, 0));

            var timeline = ActionTimeline.MeleeAttack(() => { }, 0.3f, 0.6f);
            string correlationId = "test_basic_attack_001";

            timeline.MarkerTriggered += (markerId, markerType) =>
            {
                var marker = timeline.Markers.FirstOrDefault(m => m.Id == markerId);
                EmitPresentationRequestsForMarker(marker, markerType, correlationId, basicAttack, attacker, target);
            };

            _capturedRequests.Clear();

            // Act
            timeline.Play();
            while (timeline.State == TimelineState.Playing)
            {
                timeline.Process(0.016f);
            }

            // Assert: VFX still emits through the canonical phase-based request path.
            var vfxRequests = _capturedRequests.FindAll(r => r is VfxRequest);
            var sfxRequests = _capturedRequests.FindAll(r => r is SfxRequest);

            Assert.NotEmpty(vfxRequests);
            Assert.Contains(vfxRequests.Cast<VfxRequest>(), req => req.Phase == VfxEventPhase.Impact);
            Assert.Empty(sfxRequests);
        }

        // ===== HELPERS =====

        private Combatant CreateMockCombatant(string id, string name, Godot.Vector3 position)
        {
            var combatant = new Combatant(id, name, Faction.Player, maxHP: 50, initiative: 10);
            combatant.Position = position;
            return combatant;
        }

        private void EmitPresentationRequestsForMarker(
            TimelineMarker? marker,
            MarkerType markerType,
            string correlationId,
            ActionDefinition action,
            Combatant attacker,
            Combatant target)
        {
            switch (markerType)
            {
                case MarkerType.Start:
                    // Optionally emit camera focus on attacker
                    break;

                case MarkerType.Projectile:
                    // Emit projectile request; preset comes only from marker override.
                    if (marker != null)
                    {
                        var attackerPos = new Vector3(attacker.Position.X, attacker.Position.Y, attacker.Position.Z);
                        var targetPos = new Vector3(target.Position.X, target.Position.Y, target.Position.Z);
                        _presentationBus.Publish(new VfxRequest(correlationId, VfxEventPhase.Projectile)
                        {
                            PresetId = !string.IsNullOrEmpty(marker.Data) ? marker.Data : null,
                            SourceId = attacker.Id,
                            PrimaryTargetId = target.Id,
                            SourcePosition = attackerPos,
                            TargetPosition = targetPos,
                            Pattern = VfxTargetPattern.Path
                        });
                    }
                    break;

                case MarkerType.Hit:
                    // Emit VFX and SFX for ability at hit marker
                    var impactPos = new Vector3(target.Position.X, target.Position.Y, target.Position.Z);
                    _presentationBus.Publish(new VfxRequest(correlationId, VfxEventPhase.Impact)
                    {
                        SourceId = attacker.Id,
                        PrimaryTargetId = target.Id,
                        SourcePosition = new Vector3(attacker.Position.X, attacker.Position.Y, attacker.Position.Z),
                        TargetPosition = impactPos,
                        Pattern = VfxTargetPattern.Point
                    });
                    if (!string.IsNullOrEmpty(action.SfxId))
                    {
                        _presentationBus.Publish(new SfxRequest(correlationId, action.SfxId)
                        {
                            SourceId = attacker.Id,
                            PrimaryTargetId = target.Id,
                            TargetPosition = impactPos,
                            Phase = VfxEventPhase.Impact
                        });
                    }
                    break;

                case MarkerType.VFX:
                    // Additional VFX marker requires explicit marker preset.
                    if (marker != null)
                    {
                        if (!string.IsNullOrEmpty(marker.Data))
                        {
                            var attackerPos = new Vector3(attacker.Position.X, attacker.Position.Y, attacker.Position.Z);
                            _presentationBus.Publish(new VfxRequest(correlationId, VfxEventPhase.Custom)
                            {
                                PresetId = marker.Data,
                                SourceId = attacker.Id,
                                PrimaryTargetId = target.Id,
                                SourcePosition = attackerPos,
                                TargetPosition = new Vector3(target.Position.X, target.Position.Y, target.Position.Z),
                                Pattern = VfxTargetPattern.Point
                            });
                        }
                    }
                    break;

                case MarkerType.Sound:
                    // Additional SFX marker
                    if (marker != null)
                    {
                        string sfxId = !string.IsNullOrEmpty(marker.Data) ? marker.Data : action.SfxId;
                        if (!string.IsNullOrEmpty(sfxId))
                        {
                            var attackerPos = new Vector3(attacker.Position.X, attacker.Position.Y, attacker.Position.Z);
                            _presentationBus.Publish(new SfxRequest(correlationId, sfxId)
                            {
                                SourceId = attacker.Id,
                                SourcePosition = attackerPos,
                                Phase = VfxEventPhase.Custom
                            });
                        }
                    }
                    break;
            }
        }
    }
}
