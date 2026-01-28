using System;
using System.Collections.Generic;
using System.Text.Json;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Combat.States;

namespace QDND.Tools
{
    /// <summary>
    /// Runtime debug commands for testing and development.
    /// </summary>
    public class DebugCommands
    {
        private readonly CombatContext _context;

        public DebugCommands(CombatContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Execute a debug command by name.
        /// </summary>
        public CommandResult Execute(string command, params string[] args)
        {
            try
            {
                return command.ToLower() switch
                {
                    "spawn" => SpawnCombatant(args),
                    "kill" => KillCombatant(args),
                    "damage" => ApplyDamage(args),
                    "heal" => ApplyHeal(args),
                    "status" => ApplyStatus(args),
                    "remove_status" => RemoveStatus(args),
                    "set_hp" => SetHP(args),
                    "set_initiative" => SetInitiative(args),
                    "dump_state" => DumpState(args),
                    "dump_combatant" => DumpCombatant(args),
                    "list" => ListCombatants(args),
                    "flag" => SetFlag(args),
                    "help" => ShowHelp(args),
                    _ => CommandResult.Error($"Unknown command: {command}")
                };
            }
            catch (Exception ex)
            {
                return CommandResult.Error($"Command failed: {ex.Message}");
            }
        }

        private CommandResult SpawnCombatant(string[] args)
        {
            if (args.Length < 4)
                return CommandResult.Error("Usage: spawn <id> <name> <faction> <maxHP> [initiative]");

            string id = args[0];
            string name = args[1];
            if (!Enum.TryParse<Faction>(args[2], true, out var faction))
                return CommandResult.Error($"Invalid faction: {args[2]}. Use: Player, Hostile, Neutral, Ally");
            if (!int.TryParse(args[3], out int maxHP))
                return CommandResult.Error("maxHP must be a number");
            int initiative = args.Length > 4 && int.TryParse(args[4], out int init) ? init : 10;

            var combatant = new Combatant(id, name, faction, maxHP, initiative);
            
            var turnQueue = _context.GetService<TurnQueueService>();
            turnQueue?.AddCombatant(combatant);

            return CommandResult.Success($"Spawned {combatant}");
        }

        private CommandResult KillCombatant(string[] args)
        {
            if (args.Length < 1)
                return CommandResult.Error("Usage: kill <id>");

            var combatant = FindCombatant(args[0]);
            if (combatant == null)
                return CommandResult.Error($"Combatant not found: {args[0]}");

            combatant.Resources.TakeDamage(combatant.Resources.CurrentHP + 100);
            return CommandResult.Success($"Killed {combatant.Name}");
        }

        private CommandResult ApplyDamage(string[] args)
        {
            if (args.Length < 2)
                return CommandResult.Error("Usage: damage <id> <amount> [type]");

            var combatant = FindCombatant(args[0]);
            if (combatant == null)
                return CommandResult.Error($"Combatant not found: {args[0]}");

            if (!int.TryParse(args[1], out int amount))
                return CommandResult.Error("Amount must be a number");

            string damageType = args.Length > 2 ? args[2] : "debug";
            int dealt = combatant.Resources.TakeDamage(amount);

            var rulesEngine = _context.GetService<RulesEngine>();
            rulesEngine?.Events.DispatchDamage("debug", combatant.Id, dealt, damageType);

            return CommandResult.Success($"Dealt {dealt} {damageType} damage to {combatant.Name} (HP: {combatant.Resources})");
        }

        private CommandResult ApplyHeal(string[] args)
        {
            if (args.Length < 2)
                return CommandResult.Error("Usage: heal <id> <amount>");

            var combatant = FindCombatant(args[0]);
            if (combatant == null)
                return CommandResult.Error($"Combatant not found: {args[0]}");

            if (!int.TryParse(args[1], out int amount))
                return CommandResult.Error("Amount must be a number");

            int healed = combatant.Resources.Heal(amount);

            var rulesEngine = _context.GetService<RulesEngine>();
            rulesEngine?.Events.DispatchHealing("debug", combatant.Id, healed);

            return CommandResult.Success($"Healed {combatant.Name} for {healed} HP (HP: {combatant.Resources})");
        }

        private CommandResult ApplyStatus(string[] args)
        {
            if (args.Length < 2)
                return CommandResult.Error("Usage: status <id> <statusId> [duration] [stacks]");

            var combatant = FindCombatant(args[0]);
            if (combatant == null)
                return CommandResult.Error($"Combatant not found: {args[0]}");

            string statusId = args[1];
            int? duration = args.Length > 2 && int.TryParse(args[2], out int d) ? d : null;
            int stacks = args.Length > 3 && int.TryParse(args[3], out int s) ? s : 1;

            var statusManager = _context.GetService<StatusManager>();
            if (statusManager == null)
                return CommandResult.Error("StatusManager not available");

            var instance = statusManager.ApplyStatus(statusId, "debug", combatant.Id, duration, stacks);
            if (instance == null)
                return CommandResult.Error($"Failed to apply status: {statusId}");

            return CommandResult.Success($"Applied {instance} to {combatant.Name}");
        }

        private CommandResult RemoveStatus(string[] args)
        {
            if (args.Length < 2)
                return CommandResult.Error("Usage: remove_status <id> <statusId>");

            var combatant = FindCombatant(args[0]);
            if (combatant == null)
                return CommandResult.Error($"Combatant not found: {args[0]}");

            var statusManager = _context.GetService<StatusManager>();
            if (statusManager == null)
                return CommandResult.Error("StatusManager not available");

            bool removed = statusManager.RemoveStatus(combatant.Id, args[1]);
            return removed 
                ? CommandResult.Success($"Removed {args[1]} from {combatant.Name}")
                : CommandResult.Error($"Status not found: {args[1]}");
        }

        private CommandResult SetHP(string[] args)
        {
            if (args.Length < 2)
                return CommandResult.Error("Usage: set_hp <id> <hp>");

            var combatant = FindCombatant(args[0]);
            if (combatant == null)
                return CommandResult.Error($"Combatant not found: {args[0]}");

            if (!int.TryParse(args[1], out int hp))
                return CommandResult.Error("HP must be a number");

            // Set HP by healing/damaging to target
            int diff = hp - combatant.Resources.CurrentHP;
            if (diff > 0)
                combatant.Resources.Heal(diff);
            else if (diff < 0)
                combatant.Resources.TakeDamage(-diff);

            return CommandResult.Success($"Set {combatant.Name} HP to {combatant.Resources}");
        }

        private CommandResult SetInitiative(string[] args)
        {
            if (args.Length < 2)
                return CommandResult.Error("Usage: set_initiative <id> <value>");

            var combatant = FindCombatant(args[0]);
            if (combatant == null)
                return CommandResult.Error($"Combatant not found: {args[0]}");

            if (!int.TryParse(args[1], out int initiative))
                return CommandResult.Error("Initiative must be a number");

            combatant.Initiative = initiative;
            
            // Note: initiative change takes effect on next recalculation
            // TurnQueueService recalculates on combat start and round changes

            return CommandResult.Success($"Set {combatant.Name} initiative to {initiative}");
        }

        private CommandResult DumpState(string[] args)
        {
            var turnQueue = _context.GetService<TurnQueueService>();
            var stateMachine = _context.GetService<CombatStateMachine>();
            var rulesEngine = _context.GetService<RulesEngine>();

            var state = new Dictionary<string, object>
            {
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["currentState"] = stateMachine?.CurrentState.ToString() ?? "unknown",
                ["round"] = turnQueue?.CurrentRound ?? 0,
                ["currentCombatant"] = turnQueue?.CurrentCombatant?.Id ?? "none",
                ["combatantCount"] = turnQueue?.Combatants?.Count ?? 0,
                ["eventCount"] = rulesEngine?.Events.EventHistory.Count ?? 0,
                ["debugFlags"] = DebugFlags.GetAllFlags()
            };

            string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            Godot.GD.Print($"=== STATE DUMP ===\n{json}");

            return CommandResult.Success("State dumped to console");
        }

        private CommandResult DumpCombatant(string[] args)
        {
            if (args.Length < 1)
                return CommandResult.Error("Usage: dump_combatant <id>");

            var combatant = FindCombatant(args[0]);
            if (combatant == null)
                return CommandResult.Error($"Combatant not found: {args[0]}");

            var statusManager = _context.GetService<StatusManager>();
            var rulesEngine = _context.GetService<RulesEngine>();

            var data = new Dictionary<string, object>
            {
                ["id"] = combatant.Id,
                ["name"] = combatant.Name,
                ["faction"] = combatant.Faction.ToString(),
                ["hp"] = combatant.Resources.CurrentHP,
                ["maxHp"] = combatant.Resources.MaxHP,
                ["tempHp"] = combatant.Resources.TemporaryHP,
                ["initiative"] = combatant.Initiative,
                ["isActive"] = combatant.IsActive,
                ["statuses"] = new List<string>(),
                ["stateHash"] = combatant.GetStateHash()
            };

            if (statusManager != null)
            {
                var statuses = statusManager.GetStatuses(combatant.Id);
                data["statuses"] = statuses.ConvertAll(s => s.ToString());
            }

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            Godot.GD.Print($"=== COMBATANT: {combatant.Name} ===\n{json}");

            return CommandResult.Success($"Dumped {combatant.Name}");
        }

        private CommandResult ListCombatants(string[] args)
        {
            var turnQueue = _context.GetService<TurnQueueService>();
            if (turnQueue == null)
                return CommandResult.Error("TurnQueueService not available");

            var combatants = turnQueue.Combatants;
            var lines = new List<string> { $"=== COMBATANTS ({combatants.Count}) ===" };
            
            foreach (var c in combatants)
            {
                string marker = c == turnQueue.CurrentCombatant ? " <-- CURRENT" : "";
                lines.Add($"  {c}{marker}");
            }

            string output = string.Join("\n", lines);
            Godot.GD.Print(output);

            return CommandResult.Success($"Listed {combatants.Count} combatants");
        }

        private CommandResult SetFlag(string[] args)
        {
            if (args.Length < 2)
                return CommandResult.Error("Usage: flag <name> <true|false|number>");

            string name = args[0];
            string value = args[1].ToLower();

            if (value == "true" || value == "1")
                DebugFlags.SetFlag(name, true);
            else if (value == "false" || value == "0")
                DebugFlags.SetFlag(name, false);
            else if (int.TryParse(value, out int num))
                DebugFlags.SetInt(name, num);
            else
                return CommandResult.Error($"Invalid value: {value}");

            return CommandResult.Success($"Set flag {name} = {value}");
        }

        private CommandResult ShowHelp(string[] args)
        {
            var commands = new[]
            {
                "spawn <id> <name> <faction> <maxHP> [initiative] - Spawn a combatant",
                "kill <id> - Kill a combatant",
                "damage <id> <amount> [type] - Apply damage",
                "heal <id> <amount> - Heal a combatant",
                "status <id> <statusId> [duration] [stacks] - Apply status",
                "remove_status <id> <statusId> - Remove status",
                "set_hp <id> <hp> - Set HP directly",
                "set_initiative <id> <value> - Set initiative",
                "dump_state - Dump combat state to console",
                "dump_combatant <id> - Dump combatant details",
                "list - List all combatants",
                "flag <name> <value> - Set debug flag",
                "help - Show this help"
            };

            Godot.GD.Print("=== DEBUG COMMANDS ===\n" + string.Join("\n", commands));
            return CommandResult.Success("Help displayed");
        }

        private Combatant FindCombatant(string id)
        {
            var turnQueue = _context.GetService<TurnQueueService>();
            if (turnQueue == null) return null;

            foreach (var c in turnQueue.Combatants)
            {
                if (c.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                    c.Name.Equals(id, StringComparison.OrdinalIgnoreCase))
                {
                    return c;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Result of executing a debug command.
    /// </summary>
    public class CommandResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }

        public static CommandResult Success(string message) => new() { IsSuccess = true, Message = message };
        public static CommandResult Error(string message) => new() { IsSuccess = false, Message = message };

        public override string ToString() => $"[{(IsSuccess ? "OK" : "ERROR")}] {Message}";
    }
}
