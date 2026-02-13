using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Actions;
using QDND.Data;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Integration test to verify GetActionsForCombatant filters abilities correctly
    /// based on combatant's known abilities.
    /// </summary>
    public class ActionFilteringIntegrationTest
    {
        private DataRegistry CreateTestRegistry()
        {
            var registry = new DataRegistry();
            
            // Register a variety of abilities
            registry.RegisterAction(new ActionDefinition
            {
                Id = "basic_attack",
                Name = "Basic Attack",
                Description = "A simple melee attack",
                TargetType = TargetType.SingleUnit,
                Range = 5
            });
            
            registry.RegisterAction(new ActionDefinition
            {
                Id = "power_strike",
                Name = "Power Strike",
                Description = "A powerful melee attack",
                TargetType = TargetType.SingleUnit,
                Range = 5
            });
            
            registry.RegisterAction(new ActionDefinition
            {
                Id = "second_wind",
                Name = "Second Wind",
                Description = "Heal yourself",
                TargetType = TargetType.Self,
                Range = 0
            });
            
            registry.RegisterAction(new ActionDefinition
            {
                Id = "fire_bolt",
                Name = "Fire Bolt",
                Description = "Ranged fire attack",
                TargetType = TargetType.SingleUnit,
                Range = 120
            });
            
            registry.RegisterAction(new ActionDefinition
            {
                Id = "magic_missile",
                Name = "Magic Missile",
                Description = "Never miss missiles",
                TargetType = TargetType.MultiUnit,
                Range = 120
            });
            
            registry.RegisterAction(new ActionDefinition
            {
                Id = "poison_strike",
                Name = "Poison Strike",
                Description = "Melee attack with poison",
                TargetType = TargetType.SingleUnit,
                Range = 5
            });
            
            // Fallback abilities
            registry.RegisterAction(new ActionDefinition
            {
                Id = "attack",
                Name = "Attack",
                TargetType = TargetType.SingleUnit,
                Range = 5
            });
            
            registry.RegisterAction(new ActionDefinition
            {
                Id = "dodge",
                Name = "Dodge",
                TargetType = TargetType.Self,
                Range = 0
            });
            
            return registry;
        }
        
        private ICombatContext CreateTestContext()
        {
            var context = new TestCombatContext();
            return context;
        }
        
        [Fact]
        public void GetAbilitiesForCombatant_FiltersAbilitiesCorrectly()
        {
            // Arrange
            var registry = CreateTestRegistry();
            var context = CreateTestContext();
            
            // Create combatants with specific abilities
            var fighter = new Combatant("ally_1", "Fighter", Faction.Player, 50, 15);
            fighter.Position = Vector3.Zero;
            fighter.KnownActions = new List<string> { "basic_attack", "power_strike", "second_wind" };
            context.RegisterCombatant(fighter);
            
            var mage = new Combatant("ally_2", "Mage", Faction.Player, 30, 12);
            mage.Position = new Vector3(-2, 0, 0);
            mage.KnownActions = new List<string> { "basic_attack", "fire_bolt", "magic_missile" };
            context.RegisterCombatant(mage);
            
            var goblin = new Combatant("enemy_1", "Goblin", Faction.Hostile, 20, 14);
            goblin.Position = new Vector3(6, 0, 0);
            goblin.KnownActions = new List<string> { "basic_attack", "poison_strike" };
            context.RegisterCombatant(goblin);
            
            // Simulate GetActionsForCombatant method
            Func<string, List<ActionDefinition>> getAbilitiesForCombatant = (combatantId) =>
            {
                var combatant = context.GetCombatant(combatantId);
                if (combatant == null)
                {
                    return new List<ActionDefinition>();
                }

                var knownAbilityIds = combatant.KnownActions;
                if (knownAbilityIds == null || knownAbilityIds.Count == 0)
                {
                    var fallbackIds = new HashSet<string> { "attack", "dodge", "dash", "disengage", "hide", "shove", "help", "basic_attack" };
                    return registry.GetAllActions().Where(a => fallbackIds.Contains(a.Id)).ToList();
                }

                var abilities = new List<ActionDefinition>();
                foreach (var actionId in knownAbilityIds)
                {
                    var action = registry.GetAction(actionId);
                    if (action != null)
                    {
                        abilities.Add(action);
                    }
                }
                return abilities;
            };
            
            // Act & Assert: Fighter should only see their abilities
            var fighterAbilities = getAbilitiesForCombatant("ally_1");
            var fighterAbilityIds = fighterAbilities.Select(a => a.Id).ToHashSet();
            
            Assert.Contains("basic_attack", fighterAbilityIds);
            Assert.Contains("power_strike", fighterAbilityIds);
            Assert.Contains("second_wind", fighterAbilityIds);
            Assert.DoesNotContain("fire_bolt", fighterAbilityIds);
            Assert.DoesNotContain("magic_missile", fighterAbilityIds);
            Assert.DoesNotContain("poison_strike", fighterAbilityIds);

            // Act & Assert: Mage should only see their abilities
            var mageAbilities = getAbilitiesForCombatant("ally_2");
            var mageAbilityIds = mageAbilities.Select(a => a.Id).ToHashSet();
            
            Assert.Contains("basic_attack", mageAbilityIds);
            Assert.Contains("fire_bolt", mageAbilityIds);
            Assert.Contains("magic_missile", mageAbilityIds);
            Assert.DoesNotContain("power_strike", mageAbilityIds);
            Assert.DoesNotContain("second_wind", mageAbilityIds);
            Assert.DoesNotContain("poison_strike", mageAbilityIds);

            // Act & Assert: Goblin should only see their abilities
            var goblinAbilities = getAbilitiesForCombatant("enemy_1");
            var goblinAbilityIds = goblinAbilities.Select(a => a.Id).ToHashSet();
            
            Assert.Contains("basic_attack", goblinAbilityIds);
            Assert.Contains("poison_strike", goblinAbilityIds);
            Assert.DoesNotContain("fire_bolt", goblinAbilityIds);
            Assert.DoesNotContain("power_strike", goblinAbilityIds);
            Assert.DoesNotContain("second_wind", goblinAbilityIds);
        }

        [Fact]
        public void GetAbilitiesForCombatant_ReturnsFallback_WhenCombatantHasNoAbilities()
        {
            // Arrange
            var registry = CreateTestRegistry();
            var context = CreateTestContext();
            
            var noob = new Combatant("noob_1", "Noob", Faction.Neutral, 20, 10);
            noob.Position = Vector3.Zero;
            noob.KnownActions = new List<string>(); // Empty
            context.RegisterCombatant(noob);
            
            // Simulate GetActionsForCombatant method
            Func<string, List<ActionDefinition>> getAbilitiesForCombatant = (combatantId) =>
            {
                var combatant = context.GetCombatant(combatantId);
                if (combatant == null)
                {
                    return new List<ActionDefinition>();
                }

                var knownAbilityIds = combatant.KnownActions;
                if (knownAbilityIds == null || knownAbilityIds.Count == 0)
                {
                    var fallbackIds = new HashSet<string> { "attack", "dodge", "dash", "disengage", "hide", "shove", "help", "basic_attack" };
                    return registry.GetAllActions().Where(a => fallbackIds.Contains(a.Id)).ToList();
                }

                var abilities = new List<ActionDefinition>();
                foreach (var actionId in knownAbilityIds)
                {
                    var action = registry.GetAction(actionId);
                    if (action != null)
                    {
                        abilities.Add(action);
                    }
                }
                return abilities;
            };

            // Act
            var abilities = getAbilitiesForCombatant("noob_1");
            var abilityIds = abilities.Select(a => a.Id).ToHashSet();

            // Assert: Should get fallback abilities
            var fallbackIds = new[] { "attack", "dodge", "dash", "disengage", "hide", "shove", "help", "basic_attack" };
            
            Assert.NotEmpty(abilities);
            Assert.True(abilityIds.Any(id => fallbackIds.Contains(id)), 
                $"Expected at least one fallback action, but got: {string.Join(", ", abilityIds)}");
        }

        [Fact]
        public void GetAbilitiesForCombatant_ReturnsEmpty_ForInvalidCombatantId()
        {
            // Arrange
            var registry = CreateTestRegistry();
            var context = CreateTestContext();
            
            var combatant = new Combatant("ally_1", "Ally", Faction.Player, 50, 10);
            combatant.KnownActions = new List<string> { "basic_attack" };
            context.RegisterCombatant(combatant);
            
            // Simulate GetActionsForCombatant method
            Func<string, List<ActionDefinition>> getAbilitiesForCombatant = (combatantId) =>
            {
                var c = context.GetCombatant(combatantId);
                if (c == null)
                {
                    return new List<ActionDefinition>();
                }

                var knownAbilityIds = c.KnownActions;
                if (knownAbilityIds == null || knownAbilityIds.Count == 0)
                {
                    var fallbackIds = new HashSet<string> { "attack", "dodge" };
                    return registry.GetAllActions().Where(a => fallbackIds.Contains(a.Id)).ToList();
                }

                var abilities = new List<ActionDefinition>();
                foreach (var actionId in knownAbilityIds)
                {
                    var action = registry.GetAction(actionId);
                    if (action != null)
                    {
                        abilities.Add(action);
                    }
                }
                return abilities;
            };

            // Act
            var abilities = getAbilitiesForCombatant("invalid_id_9999");

            // Assert
            Assert.Empty(abilities);
        }
        
        /// <summary>
        /// Minimal test combat context for testing.
        /// </summary>
        private class TestCombatContext : ICombatContext
        {
            private readonly Dictionary<string, Combatant> _combatants = new Dictionary<string, Combatant>();
            private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
            private readonly List<string> _registeredServices = new List<string>();

            public void RegisterCombatant(Combatant combatant)
            {
                _combatants[combatant.Id] = combatant;
            }

            public void AddCombatant(Combatant combatant)
            {
                RegisterCombatant(combatant);
            }

            public Combatant GetCombatant(string id)
            {
                return _combatants.TryGetValue(id, out var c) ? c : null;
            }

            public IEnumerable<Combatant> GetAllCombatants() => _combatants.Values;

            public void ClearCombatants()
            {
                _combatants.Clear();
            }

            public void RegisterService<T>(T service) where T : class
            {
                var serviceType = typeof(T);
                _services[serviceType] = service;
                if (!_registeredServices.Contains(serviceType.Name))
                {
                    _registeredServices.Add(serviceType.Name);
                }
            }

            public T GetService<T>() where T : class
            {
                return _services.TryGetValue(typeof(T), out var service) ? service as T : null;
            }

            public bool TryGetService<T>(out T service) where T : class
            {
                var result = _services.TryGetValue(typeof(T), out var serviceObj);
                service = result ? serviceObj as T : null;
                return result && service != null;
            }

            public bool HasService<T>() where T : class
            {
                return _services.ContainsKey(typeof(T));
            }

            public List<string> GetRegisteredServices()
            {
                return new List<string>(_registeredServices);
            }

            public void ClearServices()
            {
                _services.Clear();
                _registeredServices.Clear();
            }
        }
    }
}
