using System.Collections.Generic;
using Xunit;
using QDND.Combat.Movement;
using QDND.Combat.Rules;
using QDND.Tools.AutoBattler;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for movement action logging via MovementDetailCollector.
    /// These tests verify that MovementResult and RuleEvent data can be correctly
    /// transformed into dictionaries suitable for BlackBoxLogger.LogActionDetail.
    /// </summary>
    public class MovementLoggingTests
    {
        [Fact]
        public void MovementAction_LogsStartEndPositionAndDistance()
        {
            // Arrange: Create a MovementResult with simulated data
            // Note: We can't construct Godot.Vector3 in test host, so we'll use default (0,0,0)
            // The collector should handle this gracefully
            var result = new MovementResult
            {
                Success = true,
                CombatantId = "Fighter_1",
                StartPosition = default, // Godot.Vector3.Zero equivalent
                EndPosition = default,   // Will be [0,0,0] when converted
                DistanceMoved = 15.5f,
                RemainingMovement = 14.5f,
                TriggeredOpportunityAttacks = new List<OpportunityAttackInfo>()
            };

            // Act: Collect details from the movement result
            var details = MovementDetailCollector.CollectFromMovement(result);

            // Assert: Verify all expected fields are present
            Assert.NotNull(details);
            Assert.Equal("Walk", details["movement_type"]);
            Assert.Equal(true, details["success"]);
            Assert.Equal("Fighter_1", details["combatant_id"]);
            Assert.Equal(15.5f, details["distance_moved"]);
            Assert.Equal(14.5f, details["remaining_movement"]);
            
            // Verify position arrays exist (will be [0,0,0] in test environment)
            Assert.True(details.ContainsKey("start_position"));
            Assert.True(details.ContainsKey("end_position"));
            
            var startPos = details["start_position"] as float[];
            Assert.NotNull(startPos);
            Assert.Equal(3, startPos.Length);
            
            // Verify opportunity attack count
            Assert.Equal(0, details["triggered_opportunity_attacks"]);
        }

        [Fact]
        public void JumpAction_LogsOriginTargetAndLandingPosition()
        {
            // Arrange: Create a Jump RuleEvent with simulated data
            var evt = new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "Jump",
                SourceId = "Rogue_1",
                Data = new Dictionary<string, object>
                {
                    { "from", default(Godot.Vector3) }, // Will be converted to [0,0,0]
                    { "to", default(Godot.Vector3) },
                    { "distance", 12.0f }
                }
            };

            // Act: Collect details from the jump event
            var details = MovementDetailCollector.CollectFromSpecialMovement(evt);

            // Assert: Verify jump-specific fields
            Assert.NotNull(details);
            Assert.Equal("Jump", details["movement_type"]);
            Assert.Equal("Rogue_1", details["source_id"]);
            Assert.Equal(12.0f, details["distance"]);
            
            // Verify position arrays
            Assert.True(details.ContainsKey("start_position"));
            Assert.True(details.ContainsKey("end_position"));
            
            var startPos = details["start_position"] as float[];
            Assert.NotNull(startPos);
            Assert.Equal(3, startPos.Length);
        }

        [Fact]
        public void DashAction_LogsMovementBudgetChange()
        {
            // Arrange: Create a Dash RuleEvent
            var evt = new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "Dash",
                SourceId = "Monk_1",
                Data = new Dictionary<string, object>
                {
                    { "extraMovement", 30.0f }
                }
            };

            // Act: Collect details from the dash event
            var details = MovementDetailCollector.CollectFromSpecialMovement(evt);

            // Assert: Verify dash-specific fields
            Assert.NotNull(details);
            Assert.Equal("Dash", details["movement_type"]);
            Assert.Equal("Monk_1", details["source_id"]);
            Assert.Equal(30.0f, details["extra_movement"]);
            
            // Dash events don't have positions
            Assert.False(details.ContainsKey("start_position"));
            Assert.False(details.ContainsKey("end_position"));
        }

        [Fact]
        public void MovementWithOpportunityAttacks_LogsCount()
        {
            // Arrange: Create movement result with opportunity attacks
            var result = new MovementResult
            {
                Success = true,
                CombatantId = "Wizard_1",
                DistanceMoved = 10f,
                RemainingMovement = 20f,
                TriggeredOpportunityAttacks = new List<OpportunityAttackInfo>
                {
                    new OpportunityAttackInfo { ReactorId = "Goblin_1" },
                    new OpportunityAttackInfo { ReactorId = "Goblin_2" }
                }
            };

            // Act
            var details = MovementDetailCollector.CollectFromMovement(result);

            // Assert
            Assert.Equal(2, details["triggered_opportunity_attacks"]);
        }

        [Fact]
        public void TeleportAction_LogsPositions()
        {
            // Arrange: Create a Teleport RuleEvent
            var evt = new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "Teleport",
                SourceId = "Warlock_1",
                Data = new Dictionary<string, object>
                {
                    { "from", default(Godot.Vector3) },
                    { "to", default(Godot.Vector3) }
                }
            };

            // Act
            var details = MovementDetailCollector.CollectFromSpecialMovement(evt);

            // Assert
            Assert.Equal("Teleport", details["movement_type"]);
            Assert.Equal("Warlock_1", details["source_id"]);
            Assert.True(details.ContainsKey("start_position"));
            Assert.True(details.ContainsKey("end_position"));
        }

        [Fact]
        public void CollectFromMovement_HandlesNullGracefully()
        {
            // Act
            var details = MovementDetailCollector.CollectFromMovement(null);

            // Assert: Should return empty or default dictionary
            Assert.NotNull(details);
        }

        [Fact]
        public void CollectFromSpecialMovement_HandlesNullGracefully()
        {
            // Act
            var details = MovementDetailCollector.CollectFromSpecialMovement(null);

            // Assert: Should return empty or default dictionary
            Assert.NotNull(details);
        }

        [Fact]
        public void UnknownSpecialMovement_StillLogsMovementType()
        {
            // Arrange: Create an unknown movement type
            var evt = new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "UnknownMovementType",
                SourceId = "Test_1",
                Data = new Dictionary<string, object>()
            };

            // Act
            var details = MovementDetailCollector.CollectFromSpecialMovement(evt);

            // Assert: Should still log the movement type
            Assert.Equal("UnknownMovementType", details["movement_type"]);
            Assert.Equal("Test_1", details["source_id"]);
        }

        [Fact]
        public void ClimbAction_LogsPositionsAndComputedDistance()
        {
            var evt = new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "Climb",
                SourceId = "Ranger_1",
                Data = new Dictionary<string, object>
                {
                    { "from", default(Godot.Vector3) },
                    { "to", default(Godot.Vector3) }
                }
            };

            var details = MovementDetailCollector.CollectFromSpecialMovement(evt);

            Assert.Equal("Climb", details["movement_type"]);
            Assert.Equal("Ranger_1", details["source_id"]);
            Assert.True(details.ContainsKey("start_position"));
            Assert.True(details.ContainsKey("end_position"));
            // Computed distance should be present (both positions are zero → distance = 0)
            Assert.True(details.ContainsKey("computed_distance"));
        }

        [Fact]
        public void FlyAction_LogsPositionsAndComputedDistance()
        {
            var evt = new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "Fly",
                SourceId = "Sorcerer_1",
                Data = new Dictionary<string, object>
                {
                    { "from", default(Godot.Vector3) },
                    { "to", default(Godot.Vector3) }
                }
            };

            var details = MovementDetailCollector.CollectFromSpecialMovement(evt);

            Assert.Equal("Fly", details["movement_type"]);
            Assert.True(details.ContainsKey("start_position"));
            Assert.True(details.ContainsKey("end_position"));
            Assert.True(details.ContainsKey("computed_distance"));
        }

        [Fact]
        public void SwimAction_LogsPositionsAndComputedDistance()
        {
            var evt = new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "Swim",
                SourceId = "Paladin_1",
                Data = new Dictionary<string, object>
                {
                    { "from", default(Godot.Vector3) },
                    { "to", default(Godot.Vector3) }
                }
            };

            var details = MovementDetailCollector.CollectFromSpecialMovement(evt);

            Assert.Equal("Swim", details["movement_type"]);
            Assert.True(details.ContainsKey("start_position"));
            Assert.True(details.ContainsKey("end_position"));
            Assert.True(details.ContainsKey("computed_distance"));
        }

        [Fact]
        public void CaseInsensitiveCustomType_StillExtractsPositions()
        {
            var evt = new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "teleport", // lowercase
                SourceId = "Wizard_1",
                Data = new Dictionary<string, object>
                {
                    { "from", default(Godot.Vector3) },
                    { "to", default(Godot.Vector3) }
                }
            };

            var details = MovementDetailCollector.CollectFromSpecialMovement(evt);

            Assert.Equal("teleport", details["movement_type"]);
            Assert.True(details.ContainsKey("start_position"));
            Assert.True(details.ContainsKey("end_position"));
        }
    }
}
