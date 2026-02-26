#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Tools;

/// <summary>
/// Debug console for runtime command execution.
/// </summary>
public class DebugConsole
{
    private readonly Dictionary<string, CommandInfo> _commands = new();
    private readonly List<string> _history = new();
    private readonly List<string> _output = new();

    public event Action<string>? OnOutput;
    public event Action<string>? OnError;

    public IReadOnlyList<string> History => _history;
    public IReadOnlyList<string> Output => _output;

    public DebugConsole()
    {
        RegisterBuiltInCommands();
    }

    /// <summary>
    /// Register a command handler.
    /// </summary>
    public void RegisterCommand(string name, string description, Action<string[]> handler, string? usage = null)
    {
        _commands[name.ToLowerInvariant()] = new CommandInfo(name, description, handler, usage);
    }

    /// <summary>
    /// Execute a command string.
    /// </summary>
    public void Execute(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return;

        _history.Add(input);

        var parts = ParseCommand(input);
        if (parts.Length == 0)
            return;

        var commandName = parts[0].ToLowerInvariant();
        var args = parts.Skip(1).ToArray();

        if (!_commands.TryGetValue(commandName, out var command))
        {
            Error($"Unknown command: {commandName}. Type 'help' for available commands.");
            return;
        }

        try
        {
            command.Handler(args);
        }
        catch (Exception ex)
        {
            Error($"Command error: {ex.Message}");
        }
    }

    /// <summary>
    /// Parse command string into parts (handles quoted strings).
    /// </summary>
    private string[] ParseCommand(string input)
    {
        var parts = new List<string>();
        var current = "";
        var inQuote = false;

        foreach (var c in input)
        {
            if (c == '"')
            {
                inQuote = !inQuote;
            }
            else if (c == ' ' && !inQuote)
            {
                if (current.Length > 0)
                {
                    parts.Add(current);
                    current = "";
                }
            }
            else
            {
                current += c;
            }
        }

        if (current.Length > 0)
            parts.Add(current);

        return parts.ToArray();
    }

    public void Log(string message)
    {
        _output.Add(message);
        OnOutput?.Invoke(message);
    }

    public void Error(string message)
    {
        _output.Add($"[ERROR] {message}");
        OnError?.Invoke(message);
    }

    private void RegisterBuiltInCommands()
    {
        RegisterCommand("help", "Show available commands", Help, "help [command]");
        RegisterCommand("clear", "Clear console output", _ => _output.Clear());
        RegisterCommand("history", "Show command history", _ =>
        {
            foreach (var cmd in _history)
                Log(cmd);
        });
    }

    private void Help(string[] args)
    {
        if (args.Length > 0 && _commands.TryGetValue(args[0].ToLowerInvariant(), out var cmd))
        {
            Log($"{cmd.Name}: {cmd.Description}");
            if (cmd.Usage != null)
                Log($"  Usage: {cmd.Usage}");
        }
        else
        {
            Log("Available commands:");
            foreach (var command in _commands.Values.OrderBy(c => c.Name))
            {
                Log($"  {command.Name} - {command.Description}");
            }
        }
    }

    public IEnumerable<string> GetCommandNames() => _commands.Keys;
}

public record CommandInfo(string Name, string Description, Action<string[]> Handler, string? Usage);
