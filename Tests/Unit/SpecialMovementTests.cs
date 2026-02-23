using Godot;
using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Movement;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Unit
{
    public class SpecialMovementTests
    {
        private SpecialMovementService CreateService()
        {
            return new SpecialMovementService();
        }

        private Combatant CreateCombatant(string id, float speed = 30f, int str = 10)
        {
            var combatant = new Combatant(id, id, Faction.Player, 20, 10);
            combatant.ActionBudget.MaxMovement = speed;
            combatant.ActionBudget.ResetFull();
            combatant.ResolvedCharacter = new ResolvedCharacter
            {
                Speed = speed,
                AbilityScores = new System.Collections.Generic.Dictionary<AbilityType, int>
                {
                    { AbilityType.Strength, str }, { AbilityType.Dexterity, 10 }, { AbilityType.Constitution, 10 },
                    { AbilityType.Intelligence, 10 }, { AbilityType.Wisdom, 10 }, { AbilityType.Charisma, 10 }
                }
            };
            combatant.Position = Vector3.Zero;
            return combatant;
        }

        [Fact]
        public void CalculateJumpDistance_WithRunningStart_ReturnsStrScore()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", str: 14);

            float distance = service.CalculateJumpDistance(combatant, hasRunningStart: true);

            Assert.Equal(14, distance);
        }

        [Fact]
        public void CalculateJumpDistance_StandingJump_ReturnsHalf()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", str: 14);

            float distance = service.CalculateJumpDistance(combatant, hasRunningStart: false);

            Assert.Equal(7, distance);
        }

        [Fact]
        public void AttemptJump_WithinRange_Succeeds()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", speed: 30, str: 14);

            var result = service.AttemptJump(combatant, new Vector3(10, 0, 0));

            Assert.True(result.Success);
            Assert.Equal(new Vector3(10, 0, 0), combatant.Position);
        }

        [Fact]
        public void AttemptJump_BeyondRange_Fails()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", str: 10);

            var result = service.AttemptJump(combatant, new Vector3(15, 0, 0));

            Assert.False(result.Success);
            Assert.Contains("exceeds max jump", result.FailureReason);
        }

        [Fact]
        public void AttemptJump_ConsumesMovementBudget()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", speed: 30, str: 14);
            float initialMovement = combatant.ActionBudget.RemainingMovement;

            var result = service.AttemptJump(combatant, new Vector3(8, 0, 0));

            Assert.True(result.Success);
            Assert.Equal(8f, result.MovementBudgetUsed);
            Assert.Equal(initialMovement - 8, combatant.ActionBudget.RemainingMovement);
        }

        [Fact]
        public void AttemptJump_InsufficientMovementBudget_Fails()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", speed: 30, str: 14);
            combatant.ActionBudget.ConsumeMovement(25f); // Leave only 5

            var result = service.AttemptJump(combatant, new Vector3(10, 0, 0));

            Assert.False(result.Success);
            Assert.Contains("Insufficient movement", result.FailureReason);
        }

        [Fact]
        public void AttemptClimb_ConsumesDoubleMovement()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", speed: 30);

            var result = service.AttemptClimb(combatant, new Vector3(0, 10, 0));

            Assert.True(result.Success);
            Assert.Equal(20f, result.MovementBudgetUsed); // 10 * 2
        }

        [Fact]
        public void AttemptClimb_WithClimbSpeed_NormalCost()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", speed: 30);
            combatant.ResolvedCharacter.ClimbSpeed = 30f; // Has climb speed

            var result = service.AttemptClimb(combatant, new Vector3(0, 10, 0));

            Assert.True(result.Success);
            Assert.Equal(10f, result.MovementBudgetUsed); // Normal cost
        }

        [Fact]
        public void AttemptClimb_InsufficientMovement_Fails()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", speed: 30);
            combatant.ActionBudget.ConsumeMovement(25f);

            var result = service.AttemptClimb(combatant, new Vector3(0, 10, 0));

            Assert.False(result.Success);
            Assert.Contains("Insufficient movement", result.FailureReason);
        }

        [Fact]
        public void AttemptTeleport_WithinRange_Succeeds()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c");

            var result = service.AttemptTeleport(combatant, new Vector3(25, 0, 0), maxRange: 30);

            Assert.True(result.Success);
            Assert.Equal(0, result.MovementBudgetUsed); // Teleport is free
            Assert.False(result.ProvokedOpportunityAttack);
        }

        [Fact]
        public void AttemptTeleport_BeyondRange_Fails()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c");

            var result = service.AttemptTeleport(combatant, new Vector3(50, 0, 0), maxRange: 30);

            Assert.False(result.Success);
            Assert.Contains("beyond teleport range", result.FailureReason);
        }

        [Fact]
        public void AttemptTeleport_DoesNotConsumeMovement()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", speed: 30);
            float initialMovement = combatant.ActionBudget.RemainingMovement;

            var result = service.AttemptTeleport(combatant, new Vector3(25, 0, 0), maxRange: 30);

            Assert.True(result.Success);
            Assert.Equal(initialMovement, combatant.ActionBudget.RemainingMovement);
        }

        [Fact]
        public void AttemptSwim_WithoutSwimSpeed_ConsumesDouble()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", speed: 30);

            var result = service.AttemptSwim(combatant, new Vector3(10, 0, 0), hasSwimSpeed: false);

            Assert.True(result.Success);
            Assert.Equal(20f, result.MovementBudgetUsed);
        }

        [Fact]
        public void AttemptSwim_WithSwimSpeed_NormalCost()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", speed: 30);

            var result = service.AttemptSwim(combatant, new Vector3(10, 0, 0), hasSwimSpeed: true);

            Assert.True(result.Success);
            Assert.Equal(10f, result.MovementBudgetUsed);
        }

        [Fact]
        public void AttemptSwim_UsesInnateSwimSpeed()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", speed: 30);
            combatant.ResolvedCharacter.SwimSpeed = 30f; // Has swim speed

            var result = service.AttemptSwim(combatant, new Vector3(10, 0, 0), hasSwimSpeed: false);

            Assert.True(result.Success);
            Assert.Equal(10f, result.MovementBudgetUsed); // Normal cost due to innate swim speed
        }

        [Fact]
        public void AttemptFly_Succeeds()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", speed: 30);

            var result = service.AttemptFly(combatant, new Vector3(10, 10, 0));

            Assert.True(result.Success);
            Assert.Equal(new Vector3(10, 10, 0), combatant.Position);
        }

        [Fact]
        public void AttemptFly_ConsumesMovement()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", speed: 30);
            float initialMovement = combatant.ActionBudget.RemainingMovement;

            var result = service.AttemptFly(combatant, new Vector3(10, 0, 0));

            Assert.True(result.Success);
            Assert.Equal(10f, result.MovementBudgetUsed);
            Assert.Equal(initialMovement - 10, combatant.ActionBudget.RemainingMovement);
        }

        [Fact]
        public void AttemptFly_InsufficientMovement_Fails()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", speed: 30);
            combatant.ActionBudget.ConsumeMovement(25f);

            var result = service.AttemptFly(combatant, new Vector3(10, 0, 0));

            Assert.False(result.Success);
            Assert.Contains("Insufficient movement", result.FailureReason);
        }

        [Fact]
        public void CanDash_ReturnsTrue_WhenHasAction()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c");

            bool canDash = service.CanDash(combatant);

            Assert.True(canDash);
        }

        [Fact]
        public void CanDash_ReturnsFalse_WhenNoAction()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c");
            combatant.ActionBudget.ConsumeAction();

            bool canDash = service.CanDash(combatant);

            Assert.False(canDash);
        }

        [Fact]
        public void PerformDash_GrantsExtraMovement()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", speed: 30);
            float initialMovement = combatant.ActionBudget.RemainingMovement;

            bool dashed = service.PerformDash(combatant);

            Assert.True(dashed);
            Assert.Equal(initialMovement + 30, combatant.ActionBudget.RemainingMovement);
        }

        [Fact]
        public void PerformDash_ConsumesAction()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c");

            service.PerformDash(combatant);

            Assert.False(combatant.ActionBudget.HasAction);
        }

        [Fact]
        public void PerformDash_ReturnsFalse_WhenNoAction()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c");
            combatant.ActionBudget.ConsumeAction();

            bool dashed = service.PerformDash(combatant);

            Assert.False(dashed);
        }

        [Fact]
        public void CalculateHighJumpHeight_BasedOnStr()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", str: 16); // +3 mod

            float height = service.CalculateHighJumpHeight(combatant);

            Assert.Equal(6, height); // 3 + 3
        }

        [Fact]
        public void CalculateHighJumpHeight_StandingJump_ReturnsHalf()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", str: 16); // +3 mod

            float height = service.CalculateHighJumpHeight(combatant, hasRunningStart: false);

            Assert.Equal(3, height); // (3 + 3) / 2
        }

        [Fact]
        public void CalculateHighJumpHeight_Minimum_IsOne()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", str: 3); // -4 mod, would give 3 + (-4) = -1

            float height = service.CalculateHighJumpHeight(combatant);

            Assert.Equal(1, height); // Minimum height is 1
        }

        [Fact]
        public void GetConfig_ReturnsConfigForType()
        {
            var service = CreateService();

            var climbConfig = service.GetConfig(MovementType.Climb);

            Assert.NotNull(climbConfig);
            Assert.Equal(MovementType.Climb, climbConfig.Type);
            Assert.Equal(0.5f, climbConfig.SpeedMultiplier);
        }

        [Fact]
        public void GetConfig_TeleportDoesNotProvokeOpportunityAttacks()
        {
            var service = CreateService();

            var teleportConfig = service.GetConfig(MovementType.Teleport);

            Assert.NotNull(teleportConfig);
            Assert.False(teleportConfig.ProvokesOpportunityAttacks);
        }

        [Fact]
        public void GetConfig_FlyIgnoresDifficultTerrain()
        {
            var service = CreateService();

            var flyConfig = service.GetConfig(MovementType.Fly);

            Assert.NotNull(flyConfig);
            Assert.True(flyConfig.IgnoresDifficultTerrain);
        }

        [Fact]
        public void MovementResult_TracksPositions()
        {
            var service = CreateService();
            var combatant = CreateCombatant("c", speed: 30, str: 14);
            combatant.Position = new Vector3(5, 0, 5);

            var result = service.AttemptJump(combatant, new Vector3(10, 0, 5));

            Assert.Equal(new Vector3(5, 0, 5), result.StartPosition);
            Assert.Equal(new Vector3(10, 0, 5), result.EndPosition);
            Assert.Equal(5f, result.DistanceMoved);
        }
    }
}
