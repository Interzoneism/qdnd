#nullable enable
using System;

namespace Tools;

/// <summary>
/// Combat debug commands for testing and development.
/// </summary>
public class DebugCommands
{
    private readonly DebugConsole _console;
    private bool _godMode = false;
    private bool _fowEnabled = true;
    private bool _losDebug = false;

    public DebugCommands(DebugConsole console)
    {
        _console = console;
        RegisterCommands();
    }

    private void RegisterCommands()
    {
        // Combat manipulation
        _console.RegisterCommand("spawn", "Spawn a combatant", CmdSpawn,
            "spawn <combatant_id> <x> <y> <z> <faction>");
        _console.RegisterCommand("kill", "Kill a combatant", CmdKill,
            "kill [target_id]");
        _console.RegisterCommand("damage", "Apply damage", CmdDamage,
            "damage <amount> [target_id]");
        _console.RegisterCommand("heal", "Apply healing", CmdHeal,
            "heal <amount> [target_id]");

        // Status effects
        _console.RegisterCommand("status", "Apply status effect", CmdStatus,
            "status <status_id> [target_id]");
        _console.RegisterCommand("surface", "Spawn a surface", CmdSurface,
            "surface <type> <x> <y> <z>");

        // Combat flow
        _console.RegisterCommand("cooldown", "Reset cooldowns", CmdCooldown,
            "cooldown reset [ability]");
        _console.RegisterCommand("initiative", "Set initiative", CmdInitiative,
            "initiative <combatant_id> <value>");
        _console.RegisterCommand("skip", "Skip current turn", CmdSkip);

        // Debug toggles
        _console.RegisterCommand("godmode", "Toggle invincibility", CmdGodMode,
            "godmode [on|off]");
        _console.RegisterCommand("fow", "Toggle fog of war", CmdFow,
            "fow [on|off]");
        _console.RegisterCommand("los", "Toggle LOS debug", CmdLos,
            "los [on|off]");

        // Save/Load
        _console.RegisterCommand("save", "Save combat state", CmdSave,
            "save <filename>");
        _console.RegisterCommand("load", "Load combat state", CmdLoad,
            "load <filename>");
    }

    private void CmdSpawn(string[] args)
    {
        if (args.Length < 5)
        {
            _console.Error("Usage: spawn <combatant_id> <x> <y> <z> <faction>");
            return;
        }

        var id = args[0];
        if (!float.TryParse(args[1], out var x) ||
            !float.TryParse(args[2], out var y) ||
            !float.TryParse(args[3], out var z))
        {
            _console.Error("Invalid coordinates");
            return;
        }
        var faction = args[4];

        _console.Log($"[STUB] Would spawn {id} at ({x}, {y}, {z}) faction={faction}");
    }

    private void CmdKill(string[] args)
    {
        var target = args.Length > 0 ? args[0] : "current";
        _console.Log($"[STUB] Would kill {target}");
    }

    private void CmdDamage(string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out var amount))
        {
            _console.Error("Usage: damage <amount> [target_id]");
            return;
        }
        var target = args.Length > 1 ? args[1] : "current";
        _console.Log($"[STUB] Would deal {amount} damage to {target}");
    }

    private void CmdHeal(string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out var amount))
        {
            _console.Error("Usage: heal <amount> [target_id]");
            return;
        }
        var target = args.Length > 1 ? args[1] : "current";
        _console.Log($"[STUB] Would heal {target} for {amount}");
    }

    private void CmdStatus(string[] args)
    {
        if (args.Length < 1)
        {
            _console.Error("Usage: status <status_id> [target_id]");
            return;
        }
        var statusId = args[0];
        var target = args.Length > 1 ? args[1] : "current";
        _console.Log($"[STUB] Would apply status {statusId} to {target}");
    }

    private void CmdSurface(string[] args)
    {
        if (args.Length < 4)
        {
            _console.Error("Usage: surface <type> <x> <y> <z>");
            return;
        }
        var type = args[0];
        if (!float.TryParse(args[1], out var x) ||
            !float.TryParse(args[2], out var y) ||
            !float.TryParse(args[3], out var z))
        {
            _console.Error("Invalid coordinates");
            return;
        }
        _console.Log($"[STUB] Would spawn {type} surface at ({x}, {y}, {z})");
    }

    private void CmdCooldown(string[] args)
    {
        if (args.Length < 1 || args[0] != "reset")
        {
            _console.Error("Usage: cooldown reset [ability]");
            return;
        }
        var action = args.Length > 1 ? args[1] : "all";
        _console.Log($"[STUB] Would reset cooldown for: {action}");
    }

    private void CmdInitiative(string[] args)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out var value))
        {
            _console.Error("Usage: initiative <combatant_id> <value>");
            return;
        }
        _console.Log($"[STUB] Would set {args[0]} initiative to {value}");
    }

    private void CmdSkip(string[] args)
    {
        _console.Log("[STUB] Would skip current turn");
    }

    private void CmdGodMode(string[] args)
    {
        _godMode = ParseToggle(args, _godMode);
        _console.Log($"God mode: {(_godMode ? "ON" : "OFF")}");
    }

    private void CmdFow(string[] args)
    {
        _fowEnabled = ParseToggle(args, _fowEnabled);
        _console.Log($"Fog of war: {(_fowEnabled ? "ON" : "OFF")}");
    }

    private void CmdLos(string[] args)
    {
        _losDebug = ParseToggle(args, _losDebug);
        _console.Log($"LOS debug: {(_losDebug ? "ON" : "OFF")}");
    }

    private void CmdSave(string[] args)
    {
        if (args.Length < 1)
        {
            _console.Error("Usage: save <filename>");
            return;
        }
        _console.Log($"[STUB] Would save to: {args[0]}");
    }

    private void CmdLoad(string[] args)
    {
        if (args.Length < 1)
        {
            _console.Error("Usage: load <filename>");
            return;
        }
        _console.Log($"[STUB] Would load from: {args[0]}");
    }

    private bool ParseToggle(string[] args, bool current)
    {
        if (args.Length == 0)
            return !current;  // Toggle
        return args[0].ToLowerInvariant() switch
        {
            "on" or "true" or "1" => true,
            "off" or "false" or "0" => false,
            _ => !current
        };
    }

    // Properties for checking state
    public bool IsGodMode => _godMode;
    public bool IsFowEnabled => _fowEnabled;
    public bool IsLosDebug => _losDebug;
}
