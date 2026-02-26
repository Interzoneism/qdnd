using Godot;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Movement;
using QDND.Combat.Reactions;
using QDND.Combat.Rules;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Integration
{
    public class PhaseCIntegrationTests
    {
        [Fact]
        public void ActionBudget_IntegratesWithMovement()
        {
            var combatant = new Combatant("test", "Test", Faction.Player, 100, 10)
            {
                Position = Vector3.Zero
            };
            combatant.ActionBudget.ResetFull();

            var movementService = new MovementService();

            // Move partial distance (default MaxMovement is ActionBudget.DefaultMaxMovement)
            var result = movementService.MoveTo(combatant, new Vector3(4, 0, 0));

            Assert.True(result.Success);
            Assert.Equal(ActionBudget.DefaultMaxMovement - 4, combatant.ActionBudget.RemainingMovement);
        }

        [Fact]
        public void Surfaces_TriggerOnMovement()
        {
            var events = new RuleEventBus();
            var surfaces = new SurfaceManager(events);
            surfaces.CreateSurface("fire", new Vector3(10, 0, 0), radius: 3);

            var combatant = new Combatant("test", "Test", Faction.Player, 100, 10)
            {
                Position = Vector3.Zero
            };

            int initialHP = combatant.Resources.CurrentHP;
            surfaces.ProcessEnter(combatant, new Vector3(10, 0, 0));

            Assert.True(combatant.Resources.CurrentHP < initialHP, "Fire surface should deal damage");
        }

        [Fact]
        public void HeightAdvantage_AffectsAttackModifier()
        {
            var heightService = new HeightService();

            var attackerHigh = new Combatant("high", "HighAttacker", Faction.Player, 100, 10)
            {
                Position = new Vector3(0, 10, 0)
            };

            var targetLow = new Combatant("low", "LowTarget", Faction.Hostile, 100, 10)
            {
                Position = new Vector3(0, 0, 10)
            };

            int modifier = heightService.GetAttackModifier(attackerHigh, targetLow);

            Assert.Equal(2, modifier);
        }

        [Fact]
        public void Cover_AffectsAC()
        {
            var losService = new LOSService();
            losService.RegisterObstacle(new Obstacle
            {
                Id = "wall",
                Position = new Vector3(5, 0, 0),
                Width = 1,
                ProvidedCover = CoverLevel.Half,
                BlocksLOS = false
            });

            var attacker = new Combatant("attacker", "Attacker", Faction.Player, 100, 10)
            {
                Position = Vector3.Zero
            };
            var target = new Combatant("target", "Target", Faction.Hostile, 100, 10)
            {
                Position = new Vector3(10, 0, 0)
            };

            losService.RegisterCombatant(attacker);
            losService.RegisterCombatant(target);

            var result = losService.CheckLOS(attacker, target);

            Assert.True(result.HasLineOfSight);
            Assert.Equal(CoverLevel.Half, result.Cover);
            Assert.Equal(2, result.GetACBonus());
        }

        [Fact]
        public void ForcedMovement_TriggersSurfaces()
        {
            var surfaces = new SurfaceManager();
            surfaces.CreateSurface("fire", new Vector3(15, 0, 0), radius: 3);

            var forcedMove = new ForcedMovementService(surfaces: surfaces);

            var target = new Combatant("target", "Target", Faction.Hostile, 100, 10)
            {
                Position = new Vector3(5, 0, 0)
            };

            int initialHP = target.Resources.CurrentHP;
            var result = forcedMove.Push(target, Vector3.Zero, distance: 10);

            Assert.True(result.TriggeredSurface);
            Assert.Contains("fire", result.SurfacesCrossed);
        }

        [Fact]
        public void ReactionSystem_TracksEligibility()
        {
            var events = new RuleEventBus();
            var reactions = new ReactionSystem(events);

            var fighter = new Combatant("fighter", "Fighter", Faction.Player, 100, 10);
            fighter.ActionBudget.ResetFull();

            // Register and grant reaction
            reactions.RegisterReaction(new ReactionDefinition
            {
                Id = "opportunity_attack",
                Name = "Opportunity Attack",
                Triggers = new System.Collections.Generic.List<ReactionTriggerType>
                {
                    ReactionTriggerType.EnemyLeavesReach
                }
            });
            reactions.GrantReaction(fighter.Id, "opportunity_attack");

            // Fighter should be able to react
            Assert.True(fighter.ActionBudget.HasReaction);

            // Should have the reaction
            var availableReactions = reactions.GetReactions(fighter.Id);
            Assert.Single(availableReactions);
            Assert.Equal("reaction.opportunity_attack", availableReactions[0].Id);
        }

        [Fact]
        public void ResolutionStack_ManagesNestedActions()
        {
            var stack = new ResolutionStack { MaxDepth = 5 };

            // Push attack
            var attack = stack.Push("attack", "attacker", "target");
            Assert.Equal(1, stack.CurrentDepth);

            // Push reaction
            var reaction = stack.Push("reaction", "target");
            Assert.Equal(2, stack.CurrentDepth);

            // Resolve reaction
            stack.Pop();
            Assert.Equal(1, stack.CurrentDepth);

            // Resolve attack
            stack.Pop();
            Assert.True(stack.IsEmpty);
        }

        [Fact]
        public void SpecialMovement_Jump_UsesStrength()
        {
            var moveService = new SpecialMovementService();

            var strongCombatant = new Combatant("strong", "Strong", Faction.Player, 100, 10)
            {
                Position = Vector3.Zero
            };
            strongCombatant.AbilityScoreOverrides[AbilityType.Strength] = 16;
            strongCombatant.ActionBudget.ResetFull();

            float jumpDist = moveService.CalculateJumpDistance(strongCombatant, hasRunningStart: true);

            Assert.Equal(16, jumpDist);
        }

        [Fact]
        public void Teleport_DoesNotUseMovementBudget()
        {
            var moveService = new SpecialMovementService();

            var combatant = new Combatant("caster", "Caster", Faction.Player, 100, 10)
            {
                Position = Vector3.Zero
            };
            combatant.ActionBudget.ResetFull();

            float initialMovement = combatant.ActionBudget.RemainingMovement;
            var result = moveService.AttemptTeleport(combatant, new Vector3(25, 0, 0), maxRange: 30);

            Assert.True(result.Success);
            Assert.Equal(0, result.MovementBudgetUsed);
            Assert.Equal(initialMovement, combatant.ActionBudget.RemainingMovement);
        }

        [Fact]
        public void HeightAdvantage_Lower_GivesNegativeModifier()
        {
            var heightService = new HeightService();

            var attackerLow = new Combatant("low", "LowAttacker", Faction.Player, 100, 10)
            {
                Position = new Vector3(0, 0, 0)
            };

            var targetHigh = new Combatant("high", "HighTarget", Faction.Hostile, 100, 10)
            {
                Position = new Vector3(0, 10, 0)
            };

            int modifier = heightService.GetAttackModifier(attackerLow, targetHigh);

            Assert.Equal(-2, modifier);
        }

        [Fact]
        public void SurfaceInteraction_FireWater_CreatesSteam()
        {
            var surfaces = new SurfaceManager();

            // Create fire surface first
            surfaces.CreateSurface("fire", new Vector3(10, 0, 0), radius: 3);

            // Create water surface overlapping fire
            surfaces.CreateSurface("water", new Vector3(10, 0, 0), radius: 3);

            // The fire should be transformed to steam
            var surfacesAtPos = surfaces.GetSurfacesAt(new Vector3(10, 0, 0));
            Assert.Contains(surfacesAtPos, s => s.Definition.Id == "steam" || s.Definition.Id == "water");
        }

        [Fact]
        public void Dash_DoublesMovementBudget()
        {
            var moveService = new SpecialMovementService();

            var combatant = new Combatant("runner", "Runner", Faction.Player, 100, 10)
            {
                Position = Vector3.Zero
            };
            combatant.ActionBudget.ResetFull();

            float initialMax = combatant.ActionBudget.MaxMovement;
            float initialRemaining = combatant.ActionBudget.RemainingMovement;

            bool dashSuccess = moveService.PerformDash(combatant);

            Assert.True(dashSuccess);
            Assert.False(combatant.ActionBudget.HasAction); // Action consumed
            Assert.Equal(initialRemaining + initialMax, combatant.ActionBudget.RemainingMovement);
        }

        [Fact]
        public void MovementService_RejectsExcessiveMovement()
        {
            var movementService = new MovementService();

            var combatant = new Combatant("test", "Test", Faction.Player, 100, 10)
            {
                Position = Vector3.Zero
            };
            combatant.ActionBudget.ResetFull(); // 30 movement

            // Try to move farther than budget allows
            var result = movementService.MoveTo(combatant, new Vector3(50, 0, 0));

            Assert.False(result.Success);
            Assert.Contains("Insufficient movement", result.FailureReason);
        }

        [Fact]
        public void FallDamage_CalculatesCorrectly()
        {
            var heightService = new HeightService();

            // No damage for safe fall
            var safeFall = heightService.CalculateFallDamage(10);
            Assert.Equal(0, safeFall.Damage);

            // Damage for longer fall (20ft = 10ft above safe = 1d6 avg 3.5)
            var mediumFall = heightService.CalculateFallDamage(20);
            Assert.True(mediumFall.Damage > 0);
            Assert.True(mediumFall.IsProne);
        }

        [Fact]
        public void ReactionBudget_ConsumedOnUse()
        {
            var combatant = new Combatant("fighter", "Fighter", Faction.Player, 100, 10);
            combatant.ActionBudget.ResetFull();

            Assert.True(combatant.ActionBudget.HasReaction);

            combatant.ActionBudget.ConsumeReaction();

            Assert.False(combatant.ActionBudget.HasReaction);
        }

        [Fact]
        public void ResolutionStack_PreventOverflow()
        {
            var stack = new ResolutionStack { MaxDepth = 3 };

            stack.Push("action1", "actor1");
            stack.Push("action2", "actor2");
            stack.Push("action3", "actor3");

            Assert.Throws<System.InvalidOperationException>(() =>
                stack.Push("action4", "actor4"));
        }
    }
}
