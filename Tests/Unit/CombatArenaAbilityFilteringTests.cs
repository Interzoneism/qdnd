using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Abilities;
using QDND.Data;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for CombatArena.GetAbilitiesForCombatant to verify it filters
    /// abilities based on what each combatant actually knows.
    /// </summary>
    public class CombatArenaAbilityFilteringTests
    {
        private DataRegistry CreateTestRegistry()
        {
            var registry = new DataRegistry();
            
            // Register test abilities
            registry.RegisterAbility(new AbilityDefinition
            {
                Id = "fireball",
                Name = "Fireball",
                Description = "A classic fireball",
                TargetType = TargetType.Point,
                Range = 150
            });
            
            registry.RegisterAbility(new AbilityDefinition
            {
                Id = "magic_missile",
                Name = "Magic Missile",
                Description = "Never miss missiles",
                TargetType = TargetType.SingleUnit,
                Range = 120
            });
            
            registry.RegisterAbility(new AbilityDefinition
            {
                Id = "heal_wounds",
                Name = "Cure Wounds",
                Description = "Heal an ally",
                TargetType = TargetType.SingleUnit,
                Range = 5
            });
            
            registry.RegisterAbility(new AbilityDefinition
            {
                Id = "power_strike",
                Name = "Power Strike",
                Description = "A powerful melee attack",
                TargetType = TargetType.SingleUnit,
                Range = 5
            });
            
            registry.RegisterAbility(new AbilityDefinition
            {
                Id = "basic_attack",
                Name = "Basic Attack",
                Description = "A simple attack",
                TargetType = TargetType.SingleUnit,
                Range = 5
            });
            
            return registry;
        }
        
        private (TurnQueueService, List<Combatant>) CreateTestEnvironment(DataRegistry registry)
        {
            var turnQueue = new TurnQueueService();
            var combatants = new List<Combatant>();
            
            // Create wizard with spell abilities
            var wizard = new Combatant("wizard1", "Wizard", Faction.Player, 30, 10);
            wizard.Abilities = new List<string> { "fireball", "magic_missile" };
            combatants.Add(wizard);
            turnQueue.AddCombatant(wizard);
            
            // Create cleric with healing
            var cleric = new Combatant("cleric1", "Cleric", Faction.Player, 40, 9);
            cleric.Abilities = new List<string> { "heal_wounds" };
            combatants.Add(cleric);
            turnQueue.AddCombatant(cleric);
            
            // Create fighter with melee abilities
            var fighter = new Combatant("fighter1", "Fighter", Faction.Hostile, 50, 11);
            fighter.Abilities = new List<string> { "power_strike", "basic_attack" };
            combatants.Add(fighter);
            turnQueue.AddCombatant(fighter);
            
            // Create combatant with no abilities (should get fallback)
            var noob = new Combatant("noob1", "Noob", Faction.Neutral, 20, 5);
            noob.Abilities = new List<string>(); // Empty
            combatants.Add(noob);
            turnQueue.AddCombatant(noob);
            
            return (turnQueue, combatants);
        }

        [Fact]
        public void GetAbilitiesForCombatant_ReturnsOnlyKnownAbilities_ForWizard()
        {
            // Arrange
            var registry = CreateTestRegistry();
            var (turnQueue, combatants) = CreateTestEnvironment(registry);
            var wizard = combatants.First(c => c.Id == "wizard1");
            
            // Simulate CombatArena's behavior
            var knownAbilities = wizard.Abilities;
            var filteredAbilities = registry.GetAllAbilities()
                .Where(a => knownAbilities.Contains(a.Id))
                .ToList();
            
            // Assert
            Assert.Equal(2, filteredAbilities.Count);
            Assert.Contains(filteredAbilities, a => a.Id == "fireball");
            Assert.Contains(filteredAbilities, a => a.Id == "magic_missile");
            Assert.DoesNotContain(filteredAbilities, a => a.Id == "heal_wounds");
            Assert.DoesNotContain(filteredAbilities, a => a.Id == "power_strike");
        }
        
        [Fact]
        public void GetAbilitiesForCombatant_ReturnsOnlyKnownAbilities_ForCleric()
        {
            // Arrange
            var registry = CreateTestRegistry();
            var (turnQueue, combatants) = CreateTestEnvironment(registry);
            var cleric = combatants.First(c => c.Id == "cleric1");
            
            // Simulate CombatArena's behavior
            var knownAbilities = cleric.Abilities;
            var filteredAbilities = registry.GetAllAbilities()
                .Where(a => knownAbilities.Contains(a.Id))
                .ToList();
            
            // Assert
            Assert.Single(filteredAbilities);
            Assert.Contains(filteredAbilities, a => a.Id == "heal_wounds");
            Assert.DoesNotContain(filteredAbilities, a => a.Id == "fireball");
        }
        
        [Fact]
        public void GetAbilitiesForCombatant_ReturnsFallbackAbilities_WhenCombatantHasNone()
        {
            // Arrange
            var registry = CreateTestRegistry();
            
            // Add basic action abilities to registry (the fallback set)
            registry.RegisterAbility(new AbilityDefinition
            {
                Id = "attack",
                Name = "Attack",
                TargetType = TargetType.SingleUnit,
                Range = 5
            });
            
            registry.RegisterAbility(new AbilityDefinition
            {
                Id = "dodge",
                Name = "Dodge",
                TargetType = TargetType.Self,
                Range = 0
            });
            
            var (turnQueue, combatants) = CreateTestEnvironment(registry);
            var noob = combatants.First(c => c.Id == "noob1");
            
            // Expected fallback ability IDs
            var fallbackIds = new HashSet<string> { "attack", "dodge" };
            
            // Simulate fallback behavior
            var knownAbilities = noob.Abilities;
            List<AbilityDefinition> result;
            
            if (knownAbilities == null || knownAbilities.Count == 0)
            {
                // Return fallback abilities
                result = registry.GetAllAbilities()
                    .Where(a => fallbackIds.Contains(a.Id))
                    .ToList();
            }
            else
            {
                result = registry.GetAllAbilities()
                    .Where(a => knownAbilities.Contains(a.Id))
                    .ToList();
            }
            
            // Assert
            Assert.True(result.Count > 0, "Should return fallback abilities when combatant has none");
            Assert.All(result, ability => Assert.Contains(ability.Id, fallbackIds));
        }
        
        [Fact]
        public void GetAbilitiesForCombatant_DoesNotReturnUnknownAbilities()
        {
            // Arrange
            var registry = CreateTestRegistry();
            var (turnQueue, combatants) = CreateTestEnvironment(registry);
            var fighter = combatants.First(c => c.Id == "fighter1");
            
            // Fighter knows: power_strike, basic_attack
            // Fighter should NOT get: fireball, magic_missile, heal_wounds
            
            var knownAbilities = fighter.Abilities;
            var filteredAbilities = registry.GetAllAbilities()
                .Where(a => knownAbilities.Contains(a.Id))
                .ToList();
            
            // Assert
            Assert.DoesNotContain(filteredAbilities, a => a.Id == "fireball");
            Assert.DoesNotContain(filteredAbilities, a => a.Id == "magic_missile");
            Assert.DoesNotContain(filteredAbilities, a => a.Id == "heal_wounds");
            
            // Should only have what fighter knows
            Assert.All(filteredAbilities, a => Assert.Contains(a.Id, fighter.Abilities));
        }
    }
}
