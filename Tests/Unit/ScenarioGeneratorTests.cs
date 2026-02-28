using QDND.Data;
using QDND.Data.CharacterModel;
using Xunit;

namespace QDND.Tests.Unit
{
    public class ScenarioGeneratorTests
    {
        [Fact]
        public void GenerateRandomScenario_ClericWithoutMartialOrHeavy_StaysWithinBaseProficiencies()
        {
            var registry = new CharacterDataRegistry();
            registry.RegisterRace(new RaceDefinition
            {
                Id = "human",
                Name = "Human"
            });

            registry.RegisterClass(new ClassDefinition
            {
                Id = "cleric",
                Name = "Cleric",
                HitDie = 8,
                PrimaryAbility = "Wisdom",
                SpellcastingAbility = "Wisdom",
                StartingProficiencies = new ProficiencyGrant
                {
                    WeaponCategories = new() { "Simple" },
                    ArmorCategories = new() { "Light", "Medium", "Shield" }
                }
            });

            var generator = new ScenarioGenerator(registry, seed: 1234);
            var scenario = generator.GenerateRandomScenario(2, 2, level: 3);

            Assert.Equal(4, scenario.Units.Count);
            Assert.All(scenario.Units, unit =>
            {
                Assert.Equal("scale_mail", unit.ArmorId);
                Assert.Equal("shield", unit.ShieldId);
                Assert.Contains(unit.MainHandWeaponId, new[] { "mace", "quarterstaff" });
                Assert.DoesNotContain(unit.MainHandWeaponId, new[] { "morningstar", "flail", "warhammer" });
            });
        }
    }
}
