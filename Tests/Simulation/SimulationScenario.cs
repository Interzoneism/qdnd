#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace QDND.Tests.Simulation;

/// <summary>
/// A scenario configuration for simulation testing.
/// </summary>
public class SimulationScenario
{
    public string Name { get; set; } = "Unnamed";
    public string Description { get; set; } = "";
    public List<ScenarioCombatant> Combatants { get; set; } = new();
    public bool StopOnViolation { get; set; } = true;
    public int DefaultMaxTurns { get; set; } = 100;

    public static SimulationScenario LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Scenario not found: {path}");

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<SimulationScenario>(json, options)
            ?? throw new InvalidDataException("Failed to parse scenario");
    }

    public void SaveToFile(string path)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(path, json);
    }
}

/// <summary>
/// Configuration for a combatant in a scenario.
/// </summary>
public class ScenarioCombatant
{
    public string Id { get; set; } = "";
    public int Team { get; set; }
    public int MaxHP { get; set; } = 20;
    public int AC { get; set; } = 10;
    public int AttackBonus { get; set; } = 0;
    public int DamageBonus { get; set; } = 0;
    public int InitiativeBonus { get; set; } = 0;
}
