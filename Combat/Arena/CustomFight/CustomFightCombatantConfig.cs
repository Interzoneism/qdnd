using Godot;

namespace QDND.Combat.Arena.CustomFight
{
    [GlobalClass]
    public partial class CustomFightCombatantConfig : Resource
    {
        [Export] public string DisplayName = "";
        [Export] public CombatClass Class = CombatClass.Fighter;
        [Export] public CombatRace Race = CombatRace.Human;
        [Export(PropertyHint.Range, "1,12")] public int Level = 3;
        [Export] public CombatTeam Team = CombatTeam.Team1;
        [Export] public bool AiControlled = true;
    }
}
