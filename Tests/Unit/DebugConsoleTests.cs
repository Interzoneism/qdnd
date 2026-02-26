#nullable enable
using QDND.Tools;
using Xunit;
using System.Collections.Generic;

namespace QDND.Tests.Unit;

public class DebugConsoleTests
{
    [Fact]
    public void Execute_UnknownCommand_ReturnsError()
    {
        var console = new DebugConsole();
        string? error = null;
        console.OnError += msg => error = msg;

        console.Execute("unknowncommand");

        Assert.NotNull(error);
        Assert.Contains("Unknown command", error);
    }

    [Fact]
    public void Execute_RegisteredCommand_CallsHandler()
    {
        var console = new DebugConsole();
        bool called = false;
        console.RegisterCommand("test", "Test command", _ => called = true);

        console.Execute("test");

        Assert.True(called);
    }

    [Fact]
    public void Execute_WithArguments_PassesArgs()
    {
        var console = new DebugConsole();
        string[]? receivedArgs = null;
        console.RegisterCommand("test", "Test", args => receivedArgs = args);

        console.Execute("test arg1 arg2");

        Assert.NotNull(receivedArgs);
        Assert.Equal(2, receivedArgs!.Length);
        Assert.Equal("arg1", receivedArgs[0]);
        Assert.Equal("arg2", receivedArgs[1]);
    }

    [Fact]
    public void Execute_QuotedStrings_ParsedCorrectly()
    {
        var console = new DebugConsole();
        string[]? receivedArgs = null;
        console.RegisterCommand("test", "Test", args => receivedArgs = args);

        console.Execute("test \"hello world\" arg2");

        Assert.Equal(2, receivedArgs!.Length);
        Assert.Equal("hello world", receivedArgs[0]);
    }

    [Fact]
    public void Help_ListsAllCommands()
    {
        var console = new DebugConsole();
        var output = new List<string>();
        console.OnOutput += msg => output.Add(msg);

        console.Execute("help");

        Assert.True(output.Count > 0);
        Assert.Contains(output, o => o.Contains("help"));
    }

    [Fact]
    public void History_TracksExecutedCommands()
    {
        var console = new DebugConsole();

        console.Execute("help");
        console.Execute("clear");

        Assert.Equal(2, console.History.Count);
        Assert.Equal("help", console.History[0]);
    }

    [Fact]
    public void Execute_CaseInsensitive()
    {
        var console = new DebugConsole();
        bool called = false;
        console.RegisterCommand("Test", "Test", _ => called = true);

        console.Execute("TEST");

        Assert.True(called);
    }
}

public class DebugCommandsTests
{
    [Fact]
    public void AllCommands_Registered()
    {
        var console = new DebugConsole();
        var commands = new ConsoleDebugCommands(console);

        var names = console.GetCommandNames();

        Assert.Contains("spawn", names);
        Assert.Contains("kill", names);
        Assert.Contains("damage", names);
        Assert.Contains("heal", names);
        Assert.Contains("status", names);
        Assert.Contains("surface", names);
        Assert.Contains("cooldown", names);
        Assert.Contains("initiative", names);
        Assert.Contains("skip", names);
        Assert.Contains("godmode", names);
        Assert.Contains("fow", names);
        Assert.Contains("los", names);
        Assert.Contains("save", names);
        Assert.Contains("load", names);
    }

    [Fact]
    public void GodMode_Toggle_ChangesState()
    {
        var console = new DebugConsole();
        var commands = new ConsoleDebugCommands(console);

        Assert.False(commands.IsGodMode);
        console.Execute("godmode on");
        Assert.True(commands.IsGodMode);
        console.Execute("godmode off");
        Assert.False(commands.IsGodMode);
    }

    [Fact]
    public void Damage_InvalidArgs_ShowsError()
    {
        var console = new DebugConsole();
        var commands = new ConsoleDebugCommands(console);
        string? error = null;
        console.OnError += msg => error = msg;

        console.Execute("damage");  // Missing amount

        Assert.NotNull(error);
        Assert.Contains("Usage", error);
    }

    [Fact]
    public void Spawn_InvalidCoords_ShowsError()
    {
        var console = new DebugConsole();
        var commands = new ConsoleDebugCommands(console);
        string? error = null;
        console.OnError += msg => error = msg;

        console.Execute("spawn goblin abc 0 0 enemy");

        Assert.NotNull(error);
        Assert.Contains("Invalid coordinates", error);
    }
}
