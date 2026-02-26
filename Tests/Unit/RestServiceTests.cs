using System;
using System.Collections.Generic;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Data.ActionResources;
using QDND.Data.CharacterModel;
using Xunit;

namespace Tests.Unit
{
    /// <summary>
    /// Unit tests for RestService - handles resource replenishment for short/long rests.
    /// </summary>
    public class RestServiceTests
    {
        [Fact]
        public void ShortRest_ReplenishesShortRestResources()
        {
            var resourceManager = new ResourceManager();
            var restService = new RestService(resourceManager);
            
            // Create a warlock with pact slots (ShortRest replenish type)
            var warlock = CreateWarlock(level: 5);
            resourceManager.InitializeResources(warlock);
            
            // Consume pact magic slots
            warlock.ActionResources.Consume("WarlockSpellSlot", 1, 3);
            Assert.Equal(1, warlock.ActionResources.GetCurrent("WarlockSpellSlot", 3));
            
            // Short rest
            restService.ProcessRest(warlock, QDND.Data.CharacterModel.RestType.Short);
            
            // Verify pact slots restored
            Assert.Equal(2, warlock.ActionResources.GetCurrent("WarlockSpellSlot", 3));
        }
        
        [Fact]
        public void ShortRest_DoesNotReplenishLongRestResources()
        {
            var resourceManager = new ResourceManager();
            var restService = new RestService(resourceManager);
            
            // Create a barbarian with rage (long rest resource)
            var barbarian = CreateBarbarian(level: 5);
            resourceManager.InitializeResources(barbarian);
            
            // Consume rage
            barbarian.ActionResources.Consume("Rage", 1);
            Assert.Equal(2, barbarian.ActionResources.GetCurrent("Rage"));
            
            // Short rest
            restService.ProcessRest(barbarian, QDND.Data.CharacterModel.RestType.Short);
            
            // Verify rage NOT restored
            Assert.Equal(2, barbarian.ActionResources.GetCurrent("Rage"));
        }
        
        [Fact]
        public void LongRest_ReplenishesAllResources()
        {
            var resourceManager = new ResourceManager();
            var restService = new RestService(resourceManager);
            
            var wizard = CreateWizard(level: 5);
            resourceManager.InitializeResources(wizard);
            
            // Consume spell slots
            wizard.ActionResources.Consume("SpellSlot", 1, 1);
            wizard.ActionResources.Consume("SpellSlot", 1, 2);
            wizard.ActionResources.Consume("SpellSlot", 1, 3);
            
            Assert.Equal(3, wizard.ActionResources.GetCurrent("SpellSlot", 1));
            Assert.Equal(2, wizard.ActionResources.GetCurrent("SpellSlot", 2));
            Assert.Equal(1, wizard.ActionResources.GetCurrent("SpellSlot", 3));
            
            // Long rest
            restService.ProcessRest(wizard, QDND.Data.CharacterModel.RestType.Long);
            
            // Verify all spell slots restored
            Assert.Equal(4, wizard.ActionResources.GetCurrent("SpellSlot", 1));
            Assert.Equal(3, wizard.ActionResources.GetCurrent("SpellSlot", 2));
            Assert.Equal(2, wizard.ActionResources.GetCurrent("SpellSlot", 3));
        }
        
        [Fact]
        public void LongRest_FullyHealsHP()
        {
            var resourceManager = new ResourceManager();
            var restService = new RestService(resourceManager);
            
            var fighter = CreateFighter(level: 5);
            resourceManager.InitializeResources(fighter);
            
            // Take damage
            fighter.Resources.TakeDamage(20);
            int currentHP = fighter.Resources.CurrentHP;
            Assert.True(currentHP < fighter.Resources.MaxHP);
            
            // Long rest
            restService.ProcessRest(fighter, QDND.Data.CharacterModel.RestType.Long);
            
            // Verify full heal
            Assert.Equal(fighter.Resources.MaxHP, fighter.Resources.CurrentHP);
        }
        
        [Fact]
        public void ReplenishTurnResources_ReplenishesActionEconomy()
        {
            var resourceManager = new ResourceManager();
            var restService = new RestService(resourceManager);
            
            var fighter = CreateFighter(level: 5);
            resourceManager.InitializeResources(fighter);
            
            // Consume action economy via ActionBudget (canonical system for action/bonus/reaction)
            fighter.ActionBudget.ConsumeAction();
            fighter.ActionBudget.ConsumeBonusAction();
            fighter.ActionBudget.ConsumeReaction();
            
            Assert.False(fighter.ActionBudget.HasAction);
            Assert.False(fighter.ActionBudget.HasBonusAction);
            Assert.False(fighter.ActionBudget.HasReaction);
            
            // Replenish turn resources
            restService.ReplenishRoundResources(fighter);
            
            // Verify replenishment via ActionBudget
            Assert.True(fighter.ActionBudget.HasAction);
            Assert.True(fighter.ActionBudget.HasBonusAction);
            Assert.True(fighter.ActionBudget.HasReaction);
        }
        
        [Fact]
        public void ReplenishTurnResources_DoesNotReplenishLongRestResources()
        {
            var resourceManager = new ResourceManager();
            var restService = new RestService(resourceManager);
            
            var wizard = CreateWizard(level: 5);
            resourceManager.InitializeResources(wizard);
            
            // Consume spell slot
            wizard.ActionResources.Consume("SpellSlot", 1, 1);
            Assert.Equal(3, wizard.ActionResources.GetCurrent("SpellSlot", 1));
            
            // Replenish turn resources
            restService.ReplenishRoundResources(wizard);
            
            // Verify spell slot NOT restored
            Assert.Equal(3, wizard.ActionResources.GetCurrent("SpellSlot", 1));
        }
        
        [Fact]
        public void ReplenishRoundResources_Exists()
        {
            var resourceManager = new ResourceManager();
            var restService = new RestService(resourceManager);
            
            var fighter = CreateFighter(level: 5);
            resourceManager.InitializeResources(fighter);
            
            // Just verify it doesn't throw
            restService.ReplenishRoundResources(fighter);
        }
        
        [Fact]
        public void ShortRestMultipleCombatants_ReplenishesAll()
        {
            var resourceManager = new ResourceManager();
            var restService = new RestService(resourceManager);
            
            var warlock1 = CreateWarlock(level: 5);
            var warlock2 = CreateWarlock(level: 5);
            resourceManager.InitializeResources(warlock1);
            resourceManager.InitializeResources(warlock2);
            
            // Consume pact slots
            warlock1.ActionResources.Consume("WarlockSpellSlot", 1, 3);
            warlock2.ActionResources.Consume("WarlockSpellSlot", 2, 3);
            
            // Short rest party
            var combatants = new[] { warlock1, warlock2 };
            restService.ShortRest(combatants);
            
            // Verify both restored
            Assert.Equal(2, warlock1.ActionResources.GetCurrent("WarlockSpellSlot", 3));
            Assert.Equal(2, warlock2.ActionResources.GetCurrent("WarlockSpellSlot", 3));
        }
        
        [Fact]
        public void LongRestMultipleCombatants_ReplenishesAll()
        {
            var resourceManager = new ResourceManager();
            var restService = new RestService(resourceManager);
            
            var wizard = CreateWizard(level: 5);
            var fighter = CreateFighter(level: 5);
            resourceManager.InitializeResources(wizard);
            resourceManager.InitializeResources(fighter);
            
            // Damage and consume resources
            wizard.Resources.TakeDamage(10);
            fighter.Resources.TakeDamage(20);
            wizard.ActionResources.Consume("SpellSlot", 2, 1);
            
            // Long rest party
            var combatants = new[] { wizard, fighter };
            restService.LongRest(combatants);
            
            // Verify both restored
            Assert.Equal(wizard.Resources.MaxHP, wizard.Resources.CurrentHP);
            Assert.Equal(fighter.Resources.MaxHP, fighter.Resources.CurrentHP);
            Assert.Equal(4, wizard.ActionResources.GetCurrent("SpellSlot", 1));
        }
        
        // Helper methods
        
        private Combatant CreateWizard(int level)
        {
            var wizard = new Combatant("wizard1", "Test Wizard", Faction.Player, 38, 15);
            var classLevels = new List<ClassLevel>();
            for (int i = 0; i < level; i++)
                classLevels.Add(new ClassLevel { ClassId = "Wizard" });
            
            wizard.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet { Name = "Test Wizard", ClassLevels = classLevels },
                Resources = new Dictionary<string, int>
                {
                    { "spell_slot_1", 4 },
                    { "spell_slot_2", 3 },
                    { "spell_slot_3", 2 }
                }
            };
            
            return wizard;
        }
        
        private Combatant CreateWarlock(int level)
        {
            var warlock = new Combatant("warlock1", "Test Warlock", Faction.Player, 35, 14);
            var classLevels = new List<ClassLevel>();
            for (int i = 0; i < level; i++)
                classLevels.Add(new ClassLevel { ClassId = "Warlock" });
            
            warlock.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet { Name = "Test Warlock", ClassLevels = classLevels },
                Resources = new Dictionary<string, int>
                {
                    { "pact_slots", 2 },
                    { "pact_slot_level", 3 }
                }
            };
            
            return warlock;
        }
        
        private Combatant CreateBarbarian(int level)
        {
            var barbarian = new Combatant("barbarian1", "Test Barbarian", Faction.Player, 55, 16);
            var classLevels = new List<ClassLevel>();
            for (int i = 0; i < level; i++)
                classLevels.Add(new ClassLevel { ClassId = "Barbarian" });
            
            barbarian.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet { Name = "Test Barbarian", ClassLevels = classLevels }
            };
            
            return barbarian;
        }
        
        private Combatant CreateFighter(int level)
        {
            var fighter = new Combatant("fighter1", "Test Fighter", Faction.Player, 50, 14);
            var classLevels = new List<ClassLevel>();
            for (int i = 0; i < level; i++)
                classLevels.Add(new ClassLevel { ClassId = "Fighter" });
            
            fighter.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet { Name = "Test Fighter", ClassLevels = classLevels }
            };
            
            return fighter;
        }
    }
}
