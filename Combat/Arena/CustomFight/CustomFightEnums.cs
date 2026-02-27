using QDND.Combat.Entities;

namespace QDND.Combat.Arena.CustomFight
{
    public enum CombatClass
    {
        Fighter, Barbarian, Monk, Rogue, Wizard, Sorcerer,
        Warlock, Bard, Cleric, Paladin, Druid, Ranger
    }

    public enum CombatRace
    {
        Human, Elf, Drow, HalfElf, HalfOrc, Halfling,
        Dwarf, Gnome, Tiefling, Githyanki, Dragonborn
    }

    public enum CombatTeam
    {
        Team1, Team2
    }

    public static class CustomFightEnumExtensions
    {
        public static string ToClassId(this CombatClass c) => c switch
        {
            CombatClass.Fighter => "fighter",
            CombatClass.Barbarian => "barbarian",
            CombatClass.Monk => "monk",
            CombatClass.Rogue => "rogue",
            CombatClass.Wizard => "wizard",
            CombatClass.Sorcerer => "sorcerer",
            CombatClass.Warlock => "warlock",
            CombatClass.Bard => "bard",
            CombatClass.Cleric => "cleric",
            CombatClass.Paladin => "paladin",
            CombatClass.Druid => "druid",
            CombatClass.Ranger => "ranger",
            _ => "fighter"
        };

        public static string ToRaceId(this CombatRace r) => r switch
        {
            CombatRace.Human => "human",
            CombatRace.Elf => "elf",
            CombatRace.Drow => "drow",
            CombatRace.HalfElf => "half_elf",
            CombatRace.HalfOrc => "half_orc",
            CombatRace.Halfling => "halfling",
            CombatRace.Dwarf => "dwarf",
            CombatRace.Gnome => "gnome",
            CombatRace.Tiefling => "tiefling",
            CombatRace.Githyanki => "githyanki",
            CombatRace.Dragonborn => "dragonborn",
            _ => "human"
        };

        public static Faction ToFaction(this CombatTeam t) => t switch
        {
            CombatTeam.Team1 => Faction.Player,
            CombatTeam.Team2 => Faction.Hostile,
            _ => Faction.Player
        };
    }
}
