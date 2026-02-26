using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Movement
{
    /// <summary>
    /// Result of a special movement attempt.
    /// </summary>
    public class SpecialMovementResult
    {
        public bool Success { get; set; }
        public MovementType Type { get; set; }
        public Vector3 StartPosition { get; set; }
        public Vector3 EndPosition { get; set; }
        public float DistanceMoved { get; set; }
        public float MovementBudgetUsed { get; set; }
        public string FailureReason { get; set; }
        public int FallDamage { get; set; }
        public bool ProvokedOpportunityAttack { get; set; }
    }

    /// <summary>
    /// Service for special movement types (jump, climb, teleport, etc).
    /// </summary>
    public class SpecialMovementService
    {
        private readonly RuleEventBus _events;
        private readonly Dictionary<MovementType, MovementTypeConfig> _configs = new();
        private Func<string, string, bool> _hasStatus;

        public SpecialMovementService(RuleEventBus events = null, Func<string, string, bool> hasStatus = null)
        {
            _events = events;
            _hasStatus = hasStatus;
            RegisterDefaultConfigs();
        }

        /// <summary>
        /// Set a status query callback used by movement rules that depend on active statuses.
        /// </summary>
        public void SetStatusQuery(Func<string, string, bool> hasStatus)
        {
            _hasStatus = hasStatus;
        }

        /// <summary>
        /// Register default movement type configurations.
        /// </summary>
        private void RegisterDefaultConfigs()
        {
            _configs[MovementType.Walk] = new MovementTypeConfig
            {
                Type = MovementType.Walk,
                SpeedMultiplier = 1f,
                ProvokesOpportunityAttacks = true
            };

            _configs[MovementType.Jump] = new MovementTypeConfig
            {
                Type = MovementType.Jump,
                SpeedMultiplier = 1f,
                ProvokesOpportunityAttacks = true,
                RequiredStatCheck = "STR"
            };

            _configs[MovementType.HighJump] = new MovementTypeConfig
            {
                Type = MovementType.HighJump,
                SpeedMultiplier = 0f, // Uses fixed calculation
                ProvokesOpportunityAttacks = true,
                RequiredStatCheck = "STR"
            };

            _configs[MovementType.Climb] = new MovementTypeConfig
            {
                Type = MovementType.Climb,
                SpeedMultiplier = 0.5f, // Half speed
                ProvokesOpportunityAttacks = true,
                RequiredStatCheck = "STR"
            };

            _configs[MovementType.Swim] = new MovementTypeConfig
            {
                Type = MovementType.Swim,
                SpeedMultiplier = 0.5f, // Half speed without swim speed
                ProvokesOpportunityAttacks = true,
                RequiredStatCheck = "STR"
            };

            _configs[MovementType.Fly] = new MovementTypeConfig
            {
                Type = MovementType.Fly,
                SpeedMultiplier = 1f,
                ProvokesOpportunityAttacks = true,
                IgnoresDifficultTerrain = true
            };

            _configs[MovementType.Teleport] = new MovementTypeConfig
            {
                Type = MovementType.Teleport,
                SpeedMultiplier = 0f, // Uses fixed range
                ProvokesOpportunityAttacks = false,
                IgnoresDifficultTerrain = true
            };

            _configs[MovementType.Dash] = new MovementTypeConfig
            {
                Type = MovementType.Dash,
                SpeedMultiplier = 2f, // Double speed
                ProvokesOpportunityAttacks = true
            };
        }

        /// <summary>
        /// Get config for a movement type.
        /// </summary>
        public MovementTypeConfig GetConfig(MovementType type)
        {
            return _configs.TryGetValue(type, out var config) ? config : null;
        }

        /// <summary>
        /// Calculate jump distance based on strength.
        /// D&D 5e: Long jump = STR score feet (with 10ft running start)
        /// </summary>
        public float CalculateJumpDistance(Combatant combatant, bool hasRunningStart = true)
        {
            int str = combatant.GetAbilityScore(AbilityType.Strength);
            float baseDistance = str;
            float jumpMultiplier = HasStatus(combatant, "enhance_leap") ? 3f : 1f;

            if (!hasRunningStart)
                baseDistance /= 2f; // Standing long jump is half

            return baseDistance * jumpMultiplier;
        }

        /// <summary>
        /// Calculate high jump height.
        /// D&D 5e: 3 + STR modifier feet
        /// </summary>
        public float CalculateHighJumpHeight(Combatant combatant, bool hasRunningStart = true)
        {
            int strMod = combatant.GetAbilityModifier(AbilityType.Strength);
            float height = 3 + strMod;
            float jumpMultiplier = HasStatus(combatant, "enhance_leap") ? 3f : 1f;

            if (!hasRunningStart)
                height /= 2f;

            return Math.Max(height * jumpMultiplier, 1);
        }

        private bool HasStatus(Combatant combatant, string statusId)
        {
            if (combatant == null || string.IsNullOrWhiteSpace(statusId))
                return false;

            return _hasStatus?.Invoke(combatant.Id, statusId) == true;
        }

        /// <summary>
        /// Check if combatant has enough movement budget.
        /// </summary>
        private bool HasEnoughMovement(Combatant combatant, float cost)
        {
            if (combatant.ActionBudget == null)
                return true;
            return combatant.ActionBudget.RemainingMovement >= cost;
        }

        /// <summary>
        /// Attempt a jump from current position.
        /// </summary>
        public SpecialMovementResult AttemptJump(Combatant combatant, Vector3 targetPosition, bool hasRunningStart = true)
        {
            var result = new SpecialMovementResult
            {
                Type = MovementType.Jump,
                StartPosition = combatant.Position
            };

            float distance = combatant.Position.DistanceTo(targetPosition);
            float maxJump = CalculateJumpDistance(combatant, hasRunningStart);

            // Check if jump is within range
            if (distance > maxJump)
            {
                result.Success = false;
                result.FailureReason = $"Distance {distance:F1} exceeds max jump {maxJump:F1}";
                return result;
            }

            // Check movement budget (jump costs movement equal to distance)
            if (!HasEnoughMovement(combatant, distance))
            {
                result.Success = false;
                result.FailureReason = "Insufficient movement budget";
                return result;
            }

            // Perform jump
            combatant.ActionBudget?.ConsumeMovement(distance);
            combatant.Position = targetPosition;

            result.Success = true;
            result.EndPosition = targetPosition;
            result.DistanceMoved = distance;
            result.MovementBudgetUsed = distance;
            result.ProvokedOpportunityAttack = _configs[MovementType.Jump].ProvokesOpportunityAttacks;

            _events?.Dispatch(new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "Jump",
                SourceId = combatant.Id,
                Data = new Dictionary<string, object>
                {
                    { "from", result.StartPosition },
                    { "to", result.EndPosition },
                    { "distance", distance }
                }
            });

            return result;
        }

        /// <summary>
        /// Attempt to climb to a position.
        /// </summary>
        public SpecialMovementResult AttemptClimb(Combatant combatant, Vector3 targetPosition)
        {
            var result = new SpecialMovementResult
            {
                Type = MovementType.Climb,
                StartPosition = combatant.Position
            };

            float distance = combatant.Position.DistanceTo(targetPosition);
            var config = _configs[MovementType.Climb];

            // If combatant has climb speed, use normal cost; otherwise double
            bool hasClimbSpeed = combatant.ResolvedCharacter?.ClimbSpeed > 0;
            float multiplier = hasClimbSpeed ? 1f : config.SpeedMultiplier;
            float cost = distance / multiplier; // Costs double if no climb speed

            if (!HasEnoughMovement(combatant, cost))
            {
                result.Success = false;
                result.FailureReason = $"Insufficient movement (need {cost:F1})";
                return result;
            }

            // Perform climb
            combatant.ActionBudget?.ConsumeMovement(cost);
            combatant.Position = targetPosition;

            result.Success = true;
            result.EndPosition = targetPosition;
            result.DistanceMoved = distance;
            result.MovementBudgetUsed = cost;

            _events?.Dispatch(new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "Climb",
                SourceId = combatant.Id,
                Data = new Dictionary<string, object>
                {
                    { "from", result.StartPosition },
                    { "to", result.EndPosition }
                }
            });

            return result;
        }

        /// <summary>
        /// Attempt a teleport (like Misty Step).
        /// </summary>
        public SpecialMovementResult AttemptTeleport(Combatant combatant, Vector3 targetPosition, float maxRange = 30f)
        {
            var result = new SpecialMovementResult
            {
                Type = MovementType.Teleport,
                StartPosition = combatant.Position
            };

            float distance = combatant.Position.DistanceTo(targetPosition);

            if (distance > maxRange)
            {
                result.Success = false;
                result.FailureReason = $"Target beyond teleport range ({distance:F1} > {maxRange})";
                return result;
            }

            // Teleport doesn't use movement budget
            combatant.Position = targetPosition;

            result.Success = true;
            result.EndPosition = targetPosition;
            result.DistanceMoved = distance;
            result.MovementBudgetUsed = 0; // Teleport is free movement
            result.ProvokedOpportunityAttack = false;

            _events?.Dispatch(new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "Teleport",
                SourceId = combatant.Id,
                Data = new Dictionary<string, object>
                {
                    { "from", result.StartPosition },
                    { "to", result.EndPosition }
                }
            });

            return result;
        }

        /// <summary>
        /// Attempt swim movement.
        /// </summary>
        public SpecialMovementResult AttemptSwim(Combatant combatant, Vector3 targetPosition, bool hasSwimSpeed = false)
        {
            var result = new SpecialMovementResult
            {
                Type = MovementType.Swim,
                StartPosition = combatant.Position
            };

            float distance = combatant.Position.DistanceTo(targetPosition);

            // Check if combatant has innate swim speed or it's passed as parameter
            bool effectiveSwimSpeed = hasSwimSpeed || combatant.ResolvedCharacter?.SwimSpeed > 0;
            float multiplier = effectiveSwimSpeed ? 1f : 0.5f;
            float cost = distance / multiplier;

            if (!HasEnoughMovement(combatant, cost))
            {
                result.Success = false;
                result.FailureReason = "Insufficient movement for swim";
                return result;
            }

            combatant.ActionBudget?.ConsumeMovement(cost);
            combatant.Position = targetPosition;

            result.Success = true;
            result.EndPosition = targetPosition;
            result.DistanceMoved = distance;
            result.MovementBudgetUsed = cost;

            _events?.Dispatch(new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "Swim",
                SourceId = combatant.Id,
                Data = new Dictionary<string, object>
                {
                    { "from", result.StartPosition },
                    { "to", result.EndPosition }
                }
            });

            return result;
        }

        /// <summary>
        /// Attempt fly movement.
        /// </summary>
        public SpecialMovementResult AttemptFly(Combatant combatant, Vector3 targetPosition, float flySpeed = 30f)
        {
            var result = new SpecialMovementResult
            {
                Type = MovementType.Fly,
                StartPosition = combatant.Position
            };

            float distance = combatant.Position.DistanceTo(targetPosition);

            // Check fly speed budget (uses regular movement budget)
            if (!HasEnoughMovement(combatant, distance))
            {
                result.Success = false;
                result.FailureReason = "Insufficient movement for flight";
                return result;
            }

            combatant.ActionBudget?.ConsumeMovement(distance);
            combatant.Position = targetPosition;

            result.Success = true;
            result.EndPosition = targetPosition;
            result.DistanceMoved = distance;
            result.MovementBudgetUsed = distance;

            _events?.Dispatch(new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "Fly",
                SourceId = combatant.Id,
                Data = new Dictionary<string, object>
                {
                    { "from", result.StartPosition },
                    { "to", result.EndPosition }
                }
            });

            return result;
        }

        /// <summary>
        /// Check if combatant can dash.
        /// </summary>
        public bool CanDash(Combatant combatant)
        {
            return combatant.ActionBudget?.HasAction ?? false;
        }

        /// <summary>
        /// Perform dash action (grants extra movement equal to speed).
        /// Uses the combatant's action and grants MaxMovement additional movement.
        /// </summary>
        public bool PerformDash(Combatant combatant)
        {
            if (!CanDash(combatant))
                return false;

            float extraMovement = combatant.ActionBudget.MaxMovement;

            // Dash() consumes action and adds MaxMovement
            bool result = combatant.ActionBudget.Dash();

            if (result)
            {
                _events?.Dispatch(new RuleEvent
                {
                    Type = RuleEventType.Custom,
                    CustomType = "Dash",
                    SourceId = combatant.Id,
                    Data = new Dictionary<string, object>
                    {
                        { "extraMovement", extraMovement }
                    }
                });
            }

            return result;
        }
    }
}
