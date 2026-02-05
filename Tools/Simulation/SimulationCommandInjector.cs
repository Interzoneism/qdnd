using Godot;
using QDND.Combat.Arena;
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
        EndTurn,
        Wait,
        SelectCombatant,
        SelectAbility,
        ClearSelection
    }

    /// <summary>
    /// Represents a single gameplay command to be executed in the simulation.
    /// </summary>
    public class SimulationCommand
    {
        public SimulationCommandType Type { get; set; }
        public string ActorId { get; set; }          // For MoveTo, UseAbility
        public Vector3 TargetPosition { get; set; }  // For MoveTo
        public string AbilityId { get; set; }        // For UseAbility, SelectAbility
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

        public static SimulationCommand UseAbility(string actorId, string abilityId, string targetId)
        {
            return new SimulationCommand
            {
                Type = SimulationCommandType.UseAbility,
                ActorId = actorId,
                AbilityId = abilityId,
                TargetId = targetId
            };
        }

        public static SimulationCommand EndTurn()
        {
            return new SimulationCommand
            {
                Type = SimulationCommandType.EndTurn
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

        public static SimulationCommand SelectAbility(string abilityId)
        {
            return new SimulationCommand
            {
                Type = SimulationCommandType.SelectAbility,
                AbilityId = abilityId
            };
        }

        public static SimulationCommand ClearSelection()
        {
            return new SimulationCommand
            {
                Type = SimulationCommandType.ClearSelection
            };
        }
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
                SimulationCommandType.EndTurn => ExecuteEndTurn(command),
                SimulationCommandType.Wait => ExecuteWait(command),
                SimulationCommandType.SelectCombatant => ExecuteSelectCombatant(command),
                SimulationCommandType.SelectAbility => ExecuteSelectAbility(command),
                SimulationCommandType.ClearSelection => ExecuteClearSelection(command),
                _ => (false, $"Unknown command type: {command.Type}")
            };

            OnCommandExecuted?.Invoke(command, success, error);
            return (success, error);
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
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"MoveTo failed: {ex.Message}");
            }
        }

        private (bool, string) ExecuteUseAbility(SimulationCommand cmd)
        {
            if (string.IsNullOrEmpty(cmd.ActorId))
            {
                return (false, "UseAbility: ActorId is required");
            }

            if (string.IsNullOrEmpty(cmd.AbilityId))
            {
                return (false, "UseAbility: AbilityId is required");
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
                _arena.SelectAbility(cmd.AbilityId);
                
                // Execute ability
                _arena.ExecuteAbility(cmd.ActorId, cmd.AbilityId, cmd.TargetId);
                
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"UseAbility failed: {ex.Message}");
            }
        }

        private (bool, string) ExecuteEndTurn(SimulationCommand cmd)
        {
            // Validate it's player turn
            if (!_arena.IsPlayerTurn)
            {
                return (false, "EndTurn: Not player turn");
            }

            try
            {
                _arena.EndCurrentTurn();
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"EndTurn failed: {ex.Message}");
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

        private (bool, string) ExecuteSelectAbility(SimulationCommand cmd)
        {
            if (string.IsNullOrEmpty(cmd.AbilityId))
            {
                return (false, "SelectAbility: AbilityId is required");
            }

            try
            {
                _arena.SelectAbility(cmd.AbilityId);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"SelectAbility failed: {ex.Message}");
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
