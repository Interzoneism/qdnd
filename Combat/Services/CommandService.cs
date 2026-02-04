using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Movement;
using QDND.Combat.States;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Types of commands that can be issued.
    /// </summary>
    public enum CommandType
    {
        EndTurn,
        Move,
        UseAbility, // Stub for Phase B
        UseItem     // Stub for Phase B
    }

    /// <summary>
    /// Result of command validation.
    /// </summary>
    public class CommandValidation
    {
        public bool IsValid { get; }
        public string Reason { get; }

        public CommandValidation(bool isValid, string reason = "")
        {
            IsValid = isValid;
            Reason = reason;
        }

        public static CommandValidation Valid() => new(true);
        public static CommandValidation Invalid(string reason) => new(false, reason);
    }

    /// <summary>
    /// Base class for combat commands.
    /// </summary>
    public abstract class CombatCommand
    {
        public string CommandId { get; } = Guid.NewGuid().ToString("N")[..8];
        public abstract CommandType Type { get; }
        public string CombatantId { get; }
        public long Timestamp { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        protected CombatCommand(string combatantId)
        {
            CombatantId = combatantId ?? throw new ArgumentNullException(nameof(combatantId));
        }

        public abstract Dictionary<string, object> ToEventData();
    }

    /// <summary>
    /// Command to end the current turn.
    /// </summary>
    public class EndTurnCommand : CombatCommand
    {
        public override CommandType Type => CommandType.EndTurn;

        public EndTurnCommand(string combatantId) : base(combatantId) { }

        public override Dictionary<string, object> ToEventData()
        {
            return new Dictionary<string, object>
            {
                { "commandId", CommandId },
                { "type", Type.ToString() },
                { "combatantId", CombatantId },
                { "timestamp", Timestamp }
            };
        }
    }

    /// <summary>
    /// Command to move a combatant (stub for Phase A).
    /// </summary>
    public class MoveCommand : CombatCommand
    {
        public override CommandType Type => CommandType.Move;
        public float TargetX { get; }
        public float TargetY { get; }
        public float TargetZ { get; }

        public MoveCommand(string combatantId, float x, float y, float z) : base(combatantId)
        {
            TargetX = x;
            TargetY = y;
            TargetZ = z;
        }

        public override Dictionary<string, object> ToEventData()
        {
            return new Dictionary<string, object>
            {
                { "commandId", CommandId },
                { "type", Type.ToString() },
                { "combatantId", CombatantId },
                { "targetX", TargetX },
                { "targetY", TargetY },
                { "targetZ", TargetZ },
                { "timestamp", Timestamp }
            };
        }
    }

    /// <summary>
    /// Event emitted when a command is executed.
    /// </summary>
    public class CommandExecutedEvent
    {
        public CombatCommand Command { get; }
        public bool Success { get; }
        public string Result { get; }
        public long Timestamp { get; }

        public CommandExecutedEvent(CombatCommand command, bool success, string result = "")
        {
            Command = command;
            Success = success;
            Result = result;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    /// <summary>
    /// Validates and executes combat commands.
    /// </summary>
    public class CommandService
    {
        private readonly List<CommandExecutedEvent> _commandHistory = new();
        private readonly MovementService _movement;

        /// <summary>
        /// Reference to state machine for state-aware validation.
        /// </summary>
        public CombatStateMachine StateMachine { get; set; }

        /// <summary>
        /// Reference to turn queue for turn validation.
        /// </summary>
        public TurnQueueService TurnQueue { get; set; }

        /// <summary>
        /// Movement service for executing move commands.
        /// </summary>
        public MovementService Movement => _movement;

        public CommandService(MovementService movement = null)
        {
            _movement = movement ?? new MovementService();
        }

        /// <summary>
        /// Fired when a command is executed.
        /// </summary>
        public event Action<CommandExecutedEvent> OnCommandExecuted;

        /// <summary>
        /// History of executed commands.
        /// </summary>
        public IReadOnlyList<CommandExecutedEvent> CommandHistory => _commandHistory;

        /// <summary>
        /// Validate if a command can be executed.
        /// </summary>
        public CommandValidation Validate(CombatCommand command)
        {
            if (command == null)
                return CommandValidation.Invalid("Command is null");

            if (TurnQueue == null)
                return CommandValidation.Invalid("TurnQueue not set");

            if (StateMachine == null)
                return CommandValidation.Invalid("StateMachine not set");

            // Check if it's the combatant's turn
            var currentCombatant = TurnQueue.CurrentCombatant;
            if (currentCombatant == null)
                return CommandValidation.Invalid("No active combatant");

            if (currentCombatant.Id != command.CombatantId)
                return CommandValidation.Invalid($"Not {command.CombatantId}'s turn (current: {currentCombatant.Id})");

            // Check state machine allows commands
            var state = StateMachine.CurrentState;
            if (state != CombatState.PlayerDecision && state != CombatState.AIDecision)
                return CommandValidation.Invalid($"Cannot execute commands in state {state}");

            // Command-specific validation
            return command.Type switch
            {
                CommandType.EndTurn => CommandValidation.Valid(),
                CommandType.Move => ValidateMove((MoveCommand)command),
                _ => CommandValidation.Invalid($"Unknown command type: {command.Type}")
            };
        }

        private CommandValidation ValidateMove(MoveCommand cmd)
        {
            // Phase A: Accept all moves as valid (validation comes in Phase C)
            return CommandValidation.Valid();
        }

        /// <summary>
        /// Execute a command if valid.
        /// </summary>
        public CommandExecutedEvent Execute(CombatCommand command)
        {
            var validation = Validate(command);
            if (!validation.IsValid)
            {
                var failEvent = new CommandExecutedEvent(command, false, validation.Reason);
                _commandHistory.Add(failEvent);
                OnCommandExecuted?.Invoke(failEvent);
                return failEvent;
            }

            // Execute based on type
            string result = command.Type switch
            {
                CommandType.EndTurn => ExecuteEndTurn((EndTurnCommand)command),
                CommandType.Move => ExecuteMove((MoveCommand)command),
                _ => "Unknown command"
            };

            var evt = new CommandExecutedEvent(command, true, result);
            _commandHistory.Add(evt);
            OnCommandExecuted?.Invoke(evt);
            return evt;
        }

        private string ExecuteEndTurn(EndTurnCommand cmd)
        {
            // Transition to TurnEnd, then advance turn
            StateMachine.TryTransition(CombatState.TurnEnd, $"{cmd.CombatantId} ended turn");

            // Check for combat end
            if (TurnQueue.ShouldEndCombat())
            {
                StateMachine.TryTransition(CombatState.CombatEnd, "Combat ended");
                return "Combat ended";
            }

            // Advance turn
            bool hasNext = TurnQueue.AdvanceTurn();
            if (!hasNext)
            {
                StateMachine.TryTransition(CombatState.CombatEnd, "No more combatants");
                return "Combat ended - no more combatants";
            }

            // Check for round end (if turn index is 0, we started a new round)
            if (TurnQueue.CurrentTurnIndex == 0)
            {
                StateMachine.TryTransition(CombatState.RoundEnd, $"Round {TurnQueue.CurrentRound - 1} ended");
            }

            // Start next turn
            StateMachine.TryTransition(CombatState.TurnStart, $"{TurnQueue.CurrentCombatant?.Name}'s turn");

            // Go to appropriate decision state
            var nextCombatant = TurnQueue.CurrentCombatant;
            if (nextCombatant != null)
            {
                var decisionState = nextCombatant.IsPlayerControlled
                    ? CombatState.PlayerDecision
                    : CombatState.AIDecision;
                StateMachine.TryTransition(decisionState, $"Awaiting {nextCombatant.Name}'s decision");
            }

            return $"Turn ended, now {TurnQueue.CurrentCombatant?.Name}'s turn";
        }

        private string ExecuteMove(MoveCommand cmd)
        {
            var combatant = TurnQueue?.CurrentCombatant;
            if (combatant == null || combatant.Id != cmd.CombatantId)
                return "Invalid combatant";

            var destination = new Vector3(cmd.TargetX, cmd.TargetY, cmd.TargetZ);
            var result = _movement.MoveTo(combatant, destination);

            if (!result.Success)
                return $"Move failed: {result.FailureReason}";

            return $"Moved to ({cmd.TargetX}, {cmd.TargetY}, {cmd.TargetZ}), {result.RemainingMovement:F1} movement remaining";
        }

        /// <summary>
        /// Execute a move command directly (bypasses validation for testing).
        /// </summary>
        public MovementResult ExecuteMove(Combatant combatant, Vector3 destination)
        {
            return _movement.MoveTo(combatant, destination);
        }

        /// <summary>
        /// Clear command history.
        /// </summary>
        public void Reset()
        {
            _commandHistory.Clear();
        }
    }
}
