namespace QDND.Tests.Helpers
{
    using System.Collections.Generic;
    using System.Linq;
    using QDND.Combat.Entities;
    using QDND.Data.CharacterModel;

    public static class TestHelpers
    {
        public static Combatant MakeCombatant(
            string id = "c1", string name = null,
            Faction faction = Faction.Hostile, int maxHP = 20, int initiative = 10,
            int str = 10, int dex = 10, int con = 10,
            int @int = 10, int wis = 10, int cha = 10,
            int baseAC = 10, float speed = 30f, int profBonus = 2,
            float climbSpeed = 0f, float swimSpeed = 0f, float flySpeed = 0f)
        {
            var combatant = new Combatant(id, name ?? id, faction, maxHP, initiative);
            // Compute the level needed for the desired proficiency bonus
            int levelForProf = profBonus switch { 3 => 5, 4 => 9, 5 => 13, 6 => 17, _ => 1 };
            var sheet = new CharacterSheet
            {
                ClassLevels = Enumerable.Range(0, levelForProf)
                    .Select(_ => new ClassLevel { ClassId = "fighter" }).ToList()
            };
            combatant.ResolvedCharacter = new ResolvedCharacter
            {
                Name = name ?? id,
                Sheet = sheet,
                AbilityScores = new Dictionary<AbilityType, int>
                {
                    { AbilityType.Strength, str },
                    { AbilityType.Dexterity, dex },
                    { AbilityType.Constitution, con },
                    { AbilityType.Intelligence, @int },
                    { AbilityType.Wisdom, wis },
                    { AbilityType.Charisma, cha }
                },
                BaseAC = baseAC,
                Speed = speed,
                ClimbSpeed = climbSpeed,
                SwimSpeed = swimSpeed,
                FlySpeed = flySpeed
            };
            combatant.CurrentAC = baseAC;
            combatant.ProficiencyBonus = profBonus;
            return combatant;
        }
    }
}
