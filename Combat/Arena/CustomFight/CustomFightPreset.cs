using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace QDND.Combat.Arena.CustomFight
{
    public class CustomFightPresetCombatant
    {
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("class")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CombatClass Class { get; set; } = CombatClass.Fighter;

        [JsonPropertyName("race")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CombatRace Race { get; set; } = CombatRace.Human;

        [JsonPropertyName("level")]
        public int Level { get; set; } = 3;

        [JsonPropertyName("team")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CombatTeam Team { get; set; } = CombatTeam.Team1;

        [JsonPropertyName("aiControlled")]
        public bool AiControlled { get; set; } = true;
    }

    public class CustomFightPreset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Custom Fight";

        [JsonPropertyName("seed")]
        public int Seed { get; set; }

        [JsonPropertyName("combatants")]
        public List<CustomFightPresetCombatant> Combatants { get; set; } = new();
    }
}
