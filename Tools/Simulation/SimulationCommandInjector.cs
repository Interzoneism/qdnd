using Godot;
using QDND.Combat.Arena;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.States;
using System;
using System.Collections.Generic;

namespace QDND.Tools.Simulation
{
    /// <summary>
    /// Types of commands that can be injected into the simulation.
    /// </summary>
    public enum SimulationCommandType
    {
        MoveTo,
        UseAbility,
        UseAbilityAtPosition,  // For AoE abilities targeting a position
        EndTurn,
        Wait,
        SelectCombatant,
        SelectAction,
        ClearSelection
    }
    
    /// <summary>
    /// Error codes for simulation command execution.
    /// </summary>
    public enum SimulationErrorCode
    {
        None,
        InvalidArgument,
        NotActiveCombatant,
        NotPlayerTurn,
        InsufficientAction,
        InsufficientBonusAction,
        InsufficientReaction,
        InsufficientMovement,
        InvalidTarget,
        AbilityNotFound,
        ExecutionException
    }

    /// <summary>
    /// Represents a single gameplay command to be executed in the simulation.
    /// </summary>
    public class SimulationCommand
    {
        public SimulationCommandType Type { get; set; }
        public string ActorId { get; set; }          // For MoveTo, UseAbility
        public Vector3 TargetPosition { get; set; }  // For MoveTo, UseAbilityAtPosition
        public string ActionId { get; set; }        // For UseAbility, SelectAction
        public string TargetId { get; set; }         // For UseAbility (target combatant)
        public float WaitSeconds { get; set; }       // For Wait command

        // Factory methods for fluent API
        public static SimulationCommand MoveTo(string actorId, Vector3 target)
        {
            return new SimulationCommand
            {
                Type = SimulationCommandType.MoveTo,
                ActorId = actorId,
                TargetPosition = target
            };
        }

        public static SimulationCommand MoveTo(string actorId, float x, float y, float z)
        {
            return MoveTo(actorId, new Vector3(x, y, z));
        }

        public static SimulationCommand UseAbility(string actorId, string actionId, string targetId)
        {
            return new SimulationCommand
            {
                Type = SimulationCommandType.UseAbility,
                ActorId = actorId,
                ActionId = actionId,
                TargetId = targetId
            };
        }
        
        /// <summary>
        /// Create a command to use an AoE ability at a specific position.
        /// </summary>
        public static SimulationCommand UseAbilityAtPosition(string actorId, string actionId, Vector3 position)
        {
            return new SimulationCommand
            {
                Type = SimulationCommandType.UseAbilityAtPosition,
                ActorId = actorId,
                ActionId = actionId,
                TargetPosition = position
            };
        }
        
        /// <summary>
        /// Create a command to use an AoE ability at specific coordinates.
        /// </summary>
        public static SimulationCommand UseAbilityAtPosition(string actorId, string actionId, float x, float y, float z)
        {
            return UseAbilityAtPosition(actorId, actionId, new Vector3(x, y, z));
        }

        public static SimulationCommand EndTurn()
        {
            return new SimulationCommand
            {
                Type = SimulationCommandType.EndTurn
            };
        }
        
        /// <summary>
        /// Create an EndTurn command with optional actor validation.
        /// </summary>
        public static SimulationCommand EndTurn(string actorId)
        {
            return new SimulationCommand
            {
                Type = SimulationCommandType.EndTurn,
                ActorId = actorId
            };
        }

        public static SimulationCommand Wait(float seconds)
        {
            return new SimulationCommand
            {
                Type = SimulationCommandType.Wait,
                WaitSeconds = seconds
            };
        }

        public static SimulationCommand Select(string combatantId)
        {
            return new SimulationCommand
            {
                Type = SimulationCommandType.SelectCombatant,
                ActorId = combatantId
            };
        }

        public static SimulationCommand SelectAction(string actionId)
        {
            return new SimulationCommand
            {
                Type = SimulationCommandType.SelectAction,
                ActionId = actionId
            };
        }

        public static SimulationCommand ClearSelection()
        {
            return new SimulationCommand
            {
                Type = SimulationCommandType.ClearSelection
            };
        }
        
        /// <summary>
        /// Returns a human-readable description of this command.
        /// </summary>
        public override string ToString()
        {
            return Type switch
            {
                SimulationCommandType.MoveTo => $"MoveTo({ActorId} -> {TargetPosition})",
                SimulationCommandType.UseAbility => $"UseAbility({ActorId} -> {ActionId} -> {TargetId})",
                SimulationCommandType.UseAbilityAtPosition => $"UseAbilityAtPosition({ActorId} -> {ActionId} @ {TargetPosition})",
                SimulationCommandType.EndTurn => string.IsNullOrEmpty(ActorId) ? "EndTurn()" : $"EndTurn({ActorId})",
                SimulationCommandType.Wait => $"Wait({WaitSeconds}s)",
                SimulationCommandType.SelectCombatant => $"Select({ActorId})",
                SimulationCommandType.SelectAction => $"SelectAction({ActionId})",
                SimulationCommandType.ClearSelection => "ClearSelection()",
                _ => $"Unknown({Type})"
            };
        }
    }
    
    /// <summary>
    /// Extended result for command execution with error code classification.
    /// </summary>
    public class CommandExecutionResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public SimulationErrorCode ErrorCode { get; set; }
        public SimulationCommand Command { get; set; }
        
        public static CommandExecutionResult Ok(SimulationCommand cmd) => new()
        {
            Success = true,
            Error = null,
            ErrorCode = SimulationErrorCode.None,
            Command = cmd
        };
        
        public static CommandExecutionResult Fail(SimulationCommand cmd, SimulationErrorCode code, string error) => new()
        {
            Success = false,
            Error = error,
            ErrorCode = code,
            Command = cmd
        };
    }

    /// <summary>
    /// Injects gameplay commands into CombatArena programmatically,
    /// mimicking what CombatInputHandler does but without requiring actual user input.
    /// </summary>
    public class SimulationCommandInjector
    {
        private readonly CombatArena _arena;

        /// <summary>
        /// Event fired after each command execution.
        /// Parameters: (command, success, errorMessage)
        /// </summary>
        public event Action<SimulationCommand, bool, string> OnCommandExecuted;

        public SimulationCommandInjector(CombatArena arena)
        {
            _arena = arena ?? throw new ArgumentNullException(nameof(arena));
        }

        /// <summary>
        /// Execute a single command synchronously.
        /// Returns (success, errorMessage)
        /// </summary>
        public (bool Success, string Error) Execute(SimulationCommand command)
        {
            if (command == null)
            {
                return (false, "Command is null");
            }

            (bool success, string error) = command.Type switch
            {
                SimulationCommandType.MoveTo => ExecuteMoveTo(command),
                SimulationCommandType.UseAbility => ExecuteUseAbility(command),
                SimulationCommandType.UseAbilityAtPosition => ExecuteUseAbilityAtPosition(command),
                SimulationCommandType.EndTurn => ExecuteEndTurn(command),
                SimulationCommandType.Wait => ExecuteWait(command),
                SimulationCommandType.SelectCombatant => ExecuteSelectCombatant(command),
                SimulationCommandType.SelectAction => ExecuteSelectAction(command),
                SimulationCommandType.ClearSelection => ExecuteClearSelection(command),
                _ => (false, $"Unknown command type: {command.Type}")
            };

            OnCommandExecuted?.Invoke(command, success, error);
            return (success, error);
        }
        
        /// <summary>
        /// Execute a single command with detailed error classification.
        /// </summary>
        public CommandExecutionResult ExecuteWithDetails(SimulationCommand command)
        {
            if (command == null)
            {
                return CommandExecutionResult.Fail(null, SimulationErrorCode.InvalidArgument, "Command is null");
            }
            
            var result = command.Type switch
            {
                SimulationCommandType.MoveTo => ExecuteMoveToWithDetails(command),
                SimulationCommandType.UseAbility => ExecuteUseAbilityWithDetails(command),
                SimulationCommandType.UseAbilityAtPosition => ExecuteUseAbilityAtPositionWithDetails(command),
                SimulationCommandType.EndTurn => ExecuteEndTurnWithDetails(command),
                _ => ExecuteAndWrap(command)
            };
            
            if (result.Success)
            {
                OnCommandExecuted?.Invoke(command, true, null);
            }
            else
            {
                OnCommandExecuted?.Invoke(command, false, result.Error);
            }
            
            return result;
        }
        
        private CommandExecutionResult ExecuteAndWrap(SimulationCommand command)
        {
            var (success, error) = Execute(command);
            return success 
                ? CommandExecutionResult.Ok(command) 
                : CommandExecutionResult.Fail(command, SimulationErrorCode.ExecutionException, error);
        }

        /// <summary>
        /// Execute a sequence of commands with optional delays.
        /// Returns a list of results for each command.
        /// </summary>
        public List<(SimulationCommand Command, bool Success, string Error)> ExecuteSequence(
            IEnumerable<SimulationCommand> commands)
        {
            var results = new List<(SimulationCommand, bool, string)>();

            if (commands == null)
            {
                return results;
            }

            foreach (var command in commands)
            {
                var (success, error) = Execute(command);
                results.Add((command, success, error));

                // Stop on first failure
                if (!success && command.Type != SimulationCommandType.Wait)
                {
                    break;
                }
            }

            return results;
        }
        
        /// <summary>
        /// Execute a sequence of commands with detailed results.
        /// </summary>
        public List<CommandExecutionResult> ExecuteSequenceWithDetails(IEnumerable<SimulationCommand> commands)
        {
            var results = new List<CommandExecutionResult>();
            
            if (commands == null)
            {
                return results;
            }
            
            foreach (var command in commands)
            {
                var result = ExecuteWithDetails(command);
                results.Add(result);
                
                // Stop on first non-recoverable failure
                if (!result.Success && command.Type != SimulationCommandType.Wait)
                {
                    break;
                }
            }
            
            return results;
        }
        
        /// <summary>
        /// Get the combatant with the given ID from the arena.
        /// </summary>
        private Combatant GetCombatant(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var c in _arena.GetCombatants())
            {
                if (c.Id == id) return c;
            }
            return null;
        }
        
        /// <summary>
        /// Transition the state machine back to PlayerDecision (or AIDecision) after an action.
        /// In normal gameplay, this would be handled by timeline completion callbacks,
        /// but for simulation testing we need immediate state transitions.
        /// </summary>
        private void TransitionBackToDecisionState()
        {
            var stateMachine = _arena.Context?.GetService<CombatStateMachine>();
            if (stateMachine == null)
            {
                GD.Print("[SimulationCommandInjector] Could not get CombatStateMachine from context");
                return;
            }
            
            // Only transition if we're stuck in ActionExecution
            if (stateMachine.CurrentState != CombatState.ActionExecution)
            {
                return;
            }
            
            // Transition back to appropriate decision state based on whose turn it is
            var targetState = _arena.IsPlayerTurn ? CombatState.PlayerDecision : CombatState.AIDecision;
            
            if (stateMachine.TryTransition(targetState, "Simulation: Action completed"))
            {
                GD.Print($"[SimulationCommandInjector] Transitioned state back to {targetState}");
            }
            else
            {
                GD.Print($"[SimulationCommandInjector] Failed to transition from {stateMachine.CurrentState} to {targetState}");
            }
        }

        // Internal execution handlers

        private (bool, string) ExecuteMoveTo(SimulationCommand cmd)
        {
            if (string.IsNullOrEmpty(cmd.ActorId))
            {
                return (false, "MoveTo: ActorId is required");
            }

            // Validate actor is active combatant
            if (cmd.ActorId != _arena.ActiveCombatantId)
            {
                return (false, $"MoveTo: {cmd.ActorId} is not the active combatant ({_arena.ActiveCombatantId})");
            }

            // Validate it's player turn
            if (!_arena.IsPlayerTurn)
            {
                return (false, "MoveTo: Not player turn");
            }

            try
            {
                _arena.ExecuteMovement(cmd.ActorId, cmd.TargetPosition);
                
                // For simulation: After movement execution, the state machine stays in ActionExecution.
                // Transition it back to PlayerDecision so subsequent commands can execute.
                TransitionBackToDecisionState();
                
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"MoveTo failed: {ex.Message}");
            }
        }
        
        private CommandExecutionResult ExecuteMoveToWithDetails(SimulationCommand cmd)
        {
            if (string.IsNullOrEmpty(cmd.ActorId))
            {
                return CommandExecutionResult.Fail(cmd, SimulationErrorCode.InvalidArgument, "MoveTo: ActorId is required");
            }
            
            if (cmd.ActorId != _arena.ActiveCombatantId)
            {
                return CommandExecutionResult.Fail(cmd, SimulationErrorCode.NotActiveCombatant, 
                    $"MoveTo: {cmd.ActorId} is not the active combatant ({_arena.ActiveCombatantId})");
            }
            
            if (!_arena.IsPlayerTurn)
            {
                return CommandExecutionResult.Fail(cmd, SimulationErrorCode.NotPlayerTurn, "MoveTo: Not player turn");
            }
            
            var combatant = GetCombatant(cmd.ActorId);
            if (combatant?.ActionBudget != null)
            {
                var distance = combatant.Position.DistanceTo(cmd.TargetPosition);
                if (distance > combatant.ActionBudget.RemainingMovement)
                {
                    return CommandExecutionResult.Fail(cmd, SimulationErrorCode.InsufficientMovement,
                        $"MoveTo: Insufficient movement ({combatant.ActionBudget.RemainingMovement:F1} remaining, need {distance:F1})");
                }
            }
            
            try
            {
                _arena.ExecuteMovement(cmd.ActorId, cmd.TargetPosition);
                
                // For simulation: After movement execution, the state machine stays in ActionExecution.
                // Transition it back to PlayerDecision so subsequent commands can execute.
                TransitionBackToDecisionState();
                
                return CommandExecutionResult.Ok(cmd);
            }
            catch (Exception ex)
            {
                return CommandExecutionResult.Fail(cmd, SimulationErrorCode.ExecutionException, $"MoveTo failed: {ex.Message}");
            }
        }

        private (bool, string) ExecuteUseAbility(SimulationCommand cmd)
        {
            if (string.IsNullOrEmpty(cmd.ActorId))
            {
                return (false, "UseAbility: ActorId is required");
            }

            if (string.IsNullOrEmpty(cmd.ActionId))
            {
                return (false, "UseAbility: ActionId is required");
            }

            if (string.IsNullOrEmpty(cmd.TargetId))
            {
                return (false, "UseAbility: TargetId is required");
            }

            // Validate actor is active combatant
            if (cmd.ActorId != _arena.ActiveCombatantId)
            {
                return (false, $"UseAbility: {cmd.ActorId} is not the active combatant ({_arena.ActiveCombatantId})");
            }

            // Validate it's player turn
            if (!_arena.IsPlayerTurn)
            {
                return (false, "UseAbility: Not player turn");
            }

            try
            {
                // Select combatant (should already be selected, but ensure it)
                _arena.SelectCombatant(cmd.ActorId);
                
                // Select ability
                _arena.SelectAction(cmd.ActionId);
                
                // Execute ability
                _arena.ExecuteAction(cmd.ActorId, cmd.ActionId, cmd.TargetId);
                
                // For simulation: After ability execution, the state machine stays in ActionExecution.
                // Transition it back to PlayerDecision so subsequent commands can execute.
                // In normal gameplay, this would be handled by timeline completion callbacks.
                TransitionBackToDecisionState();
                
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"UseAbility failed: {ex.Message}");
            }
        }
        
        private CommandExecutionResult ExecuteUseAbilityWithDetails(SimulationCommand cmd)
        {
            if (string.IsNullOrEmpty(cmd.ActorId))
            {
                return CommandExecutionResult.Fail(cmd, SimulationErrorCode.InvalidArgument, "UseAbility: ActorId is required");
            }
            
            if (string.IsNullOrEmpty(cmd.ActionId))
            {
                return CommandExecutionResult.Fail(cmd, SimulationErrorCode.InvalidArgument, "UseAbility: ActionId is required");
            }
            
            if (string.IsNullOrEmpty(cmd.TargetId))
            {
                return CommandExecutionResult.Fail(cmd, SimulationErrorCode.InvalidArgument, "UseAbility: TargetId is required");
            }
            
            if (cmd.ActorId != _arena.ActiveCombatantId)
            {
                return CommandExecutionResult.Fail(cmd, SimulationErrorCode.NotActiveCombatant,
                    $"UseAbility: {cmd.ActorId} is not the active combatant ({_arena.ActiveCombatantId})");
            }
            
            if (!_arena.IsPlayerTurn)
            {
                return CommandExecutionResult.Fail(cmd, SimulationErrorCode.NotPlayerTurn, "UseAbility: Not player turn");
            }
            
            // Pre-flight action budget check
            var combatant = GetCombatant(cmd.ActorId);
            if (combatant?.ActionBudget != null)
            {
                // Basic check - most abilities use action
                // TODO: Look up ability definition for exact cost
                if (!combatant.ActionBudget.HasAction && !combatant.ActionBudget.HasBonusAction)
                {
                    return CommandExecutionResult.Fail(cmd, SimulationErrorCode.InsufficientAction,
                        "UseAbility: No action or bonus action available");
                }
            }
            
            try
            {
                _arena.SelectCombatant(cmd.ActorId);
                _arena.SelectAction(cmd.ActionId);
                _arena.ExecuteAction(cmd.ActorId, cmd.ActionId, cmd.TargetId);
                
                // For simulation: After ability execution, the state machine stays in ActionExecution.
                // Transition it back to PlayerDecision so subsequent commands can execute.
                TransitionBackToDecisionState();
                
                return CommandExecutionResult.Ok(cmd);
            }
            catch (Exception ex)
            {
                return CommandExecutionResult.Fail(cmd, SimulationErrorCode.ExecutionException, $"UseAbility failed: {ex.Message}");
            }
        }
        
        private (bool, string) ExecuteUseAbilityAtPosition(SimulationCommand cmd)
        {
            if (string.IsNullOrEmpty(cmd.ActorId))
            {
                return (false, "UseAbilityAtPosition: ActorId is required");
            }
            
            if (string.IsNullOrEmpty(cmd.ActionId))
            {
                return (false, "UseAbilityAtPosition: ActionId is required");
            }
            
            if (cmd.ActorId != _arena.ActiveCombatantId)
            {
                return (false, $"UseAbilityAtPosition: {cmd.ActorId} is not the active combatant ({_arena.ActiveCombatantId})");
            }
            
            if (!_arena.IsPlayerTurn)
            {
                return (false, "UseAbilityAtPosition: Not player turn");
            }
            
            // TODO: Implement position-based AoE ability execution
            // For now, this is a stub that logs and fails gracefully
            GD.Print($"[SimulationCommandInjector] UseAbilityAtPosition not yet implemented for {cmd.ActionId} at {cmd.TargetPosition}");
            return (false, "UseAbilityAtPosition: Not yet implemented. Use UseAbility with a target combatant instead.");
        }
        
        private CommandExecutionResult ExecuteUseAbilityAtPositionWithDetails(SimulationCommand cmd)
        {
            if (string.IsNullOrEmpty(cmd.ActorId))
            {
                return CommandExecutionResult.Fail(cmd, SimulationErrorCode.InvalidArgument, "UseAbilityAtPosition: ActorId is required");
            }
            
            if (string.IsNullOrEmpty(cmd.ActionId))
            {
                return CommandExecutionResult.Fail(cmd, SimulationErrorCode.InvalidArgument, "UseAbilityAtPosition: ActionId is required");
            }
            
            if (cmd.ActorId != _arena.ActiveCombatantId)
            {
                return CommandExecutionResult.Fail(cmd, SimulationErrorCode.NotActiveCombatant,
                    $"UseAbilityAtPosition: {cmd.ActorId} is not the active combatant ({_arena.ActiveCombatantId})");
            }
            
            if (!_arena.IsPlayerTurn)
            {
                return CommandExecutionResult.Fail(cmd, SimulationErrorCode.NotPlayerTurn, "UseAbilityAtPosition: Not player turn");
            }
            
            // TODO: Implement position-based AoE ability execution
            GD.Print($"[SimulationCommandInjector] UseAbilityAtPosition not yet implemented for {cmd.ActionId} at {cmd.TargetPosition}");
            return CommandExecutionResult.Fail(cmd, SimulationErrorCode.ExecutionException, 
                "UseAbilityAtPosition: Not yet implemented. Use UseAbility with a target combatant instead.");
        }

        private (bool, string) ExecuteEndTurn(SimulationCommand cmd)
        {
            // If actor specified, validate it matches active combatant
            if (!string.IsNullOrEmpty(cmd.ActorId) && cmd.ActorId != _arena.ActiveCombatantId)
            {
                return (false, $"EndTurn: {cmd.ActorId} is not the active combatant ({_arena.ActiveCombatantId})");
            }

            try
            {
                string previousCombatant = _arena.ActiveCombatantId;
                bool wasPlayerTurn = _arena.IsPlayerTurn;
                
                _arena.EndCurrentTurn();
                
                GD.Print($"[SimulationCommandInjector] Turn ended: {previousCombatant} -> {_arena.ActiveCombatantId} (was {(wasPlayerTurn ? "player" : "AI")} turn)");
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"EndTurn failed: {ex.Message}");
            }
        }
        
        private CommandExecutionResult ExecuteEndTurnWithDetails(SimulationCommand cmd)
        {
            if (!string.IsNullOrEmpty(cmd.ActorId) && cmd.ActorId != _arena.ActiveCombatantId)
            {
                return CommandExecutionResult.Fail(cmd, SimulationErrorCode.NotActiveCombatant,
                    $"EndTurn: {cmd.ActorId} is not the active combatant ({_arena.ActiveCombatantId})");
            }
            
            try
            {
                string previousCombatant = _arena.ActiveCombatantId;
                bool wasPlayerTurn = _arena.IsPlayerTurn;
                
                _arena.EndCurrentTurn();
                
                GD.Print($"[SimulationCommandInjector] Turn ended: {previousCombatant} -> {_arena.ActiveCombatantId} (was {(wasPlayerTurn ? "player" : "AI")} turn)");
                return CommandExecutionResult.Ok(cmd);
            }
            catch (Exception ex)
            {
                return CommandExecutionResult.Fail(cmd, SimulationErrorCode.ExecutionException, $"EndTurn failed: {ex.Message}");
            }
        }

        private (bool, string) ExecuteWait(SimulationCommand cmd)
        {
            // Wait is not supported in synchronous mode
            // But we don't fail - just log a warning
            GD.Print($"[SimulationCommandInjector] Wait command ({cmd.WaitSeconds}s) not supported in synchronous mode - skipping");
            return (true, "Wait not supported in synchronous mode");
        }

        private (bool, string) ExecuteSelectCombatant(SimulationCommand cmd)
        {
            if (string.IsNullOrEmpty(cmd.ActorId))
            {
                return (false, "SelectCombatant: ActorId is required");
            }

            try
            {
                _arena.SelectCombatant(cmd.ActorId);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"SelectCombatant failed: {ex.Message}");
            }
        }

        private (bool, string) ExecuteSelectAction(SimulationCommand cmd)
        {
            if (string.IsNullOrEmpty(cmd.ActionId))
            {
                return (false, "SelectAction: ActionId is required");
            }

            try
            {
                _arena.SelectAction(cmd.ActionId);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"SelectAction failed: {ex.Message}");
            }
        }

        private (bool, string) ExecuteClearSelection(SimulationCommand cmd)
        {
            try
            {
                _arena.ClearSelection();
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"ClearSelection failed: {ex.Message}");
            }
        }
    }
}
