#nullable enable
using System.Collections.Generic;

namespace QDND.Editor;

/// <summary>
/// Represents an editable ability definition.
/// </summary>
public class EditableAbilityDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "action";
    public int CooldownTurns { get; set; }
    public int MaxCharges { get; set; } = 1;
    public string TargetType { get; set; } = "single";
    public int Range { get; set; }
    public int AreaRadius { get; set; }
    public List<string> EffectIds { get; set; } = new();
    public Dictionary<string, object> CustomData { get; set; } = new();
}

/// <summary>
/// Represents an editable status definition.
/// </summary>
public class EditableStatusDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "buff";
    public int Duration { get; set; }
    public bool Stackable { get; set; }
    public int MaxStacks { get; set; } = 1;
    public string DurationType { get; set; } = "turns";
    public List<string> EffectIds { get; set; } = new();
    public Dictionary<string, object> Modifiers { get; set; } = new();
}

/// <summary>
/// Represents an editable scenario definition.
/// </summary>
public class EditableScenarioDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<ScenarioSpawnPoint> SpawnPoints { get; set; } = new();
    public string MapId { get; set; } = "";
    public Dictionary<string, object> Settings { get; set; } = new();
}

/// <summary>
/// A spawn point in a scenario.
/// </summary>
public class ScenarioSpawnPoint
{
    public string CombatantId { get; set; } = "";
    public string Faction { get; set; } = "enemy";
    public int Team { get; set; } = 2;
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}
