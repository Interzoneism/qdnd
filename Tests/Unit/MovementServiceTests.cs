using System;
using Xunit;
using Godot;
using QDND.Combat.Movement;
using QDND.Combat.Entities;
using QDND.Combat.Rules;

namespace QDND.Tests.Unit
{
    public class MovementServiceTests
    {
        private Combatant CreateCombatant(string id, Vector3 position, float maxMove = 30f)
        {
            var c = new Combatant(id, $"Test_{id}", Faction.Player, 100, 10);
            c.Position = position;
            c.ActionBudget.MaxMovement = maxMove;
            c.ActionBudget.ResetFull();
            return c;
        }

        [Fact]
        public void MoveTo_Success_UpdatesPosition()
        {
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0));
            
            var result = service.MoveTo(combatant, new Vector3(10, 0, 0));
            
            Assert.True(result.Success);
            Assert.Equal(new Vector3(10, 0, 0), combatant.Position);
        }

        [Fact]
        public void MoveTo_Success_ConsumesBudget()
        {
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);
            
            service.MoveTo(combatant, new Vector3(10, 0, 0));
            
            Assert.Equal(20, combatant.ActionBudget.RemainingMovement, 0.01);
        }

        [Fact]
        public void MoveTo_InsufficientBudget_Fails()
        {
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 5f);
            
            var result = service.MoveTo(combatant, new Vector3(10, 0, 0));
            
            Assert.False(result.Success);
            Assert.Contains("Insufficient movement", result.FailureReason);
            Assert.Equal(new Vector3(0, 0, 0), combatant.Position); // Position unchanged
        }

        [Fact]
        public void MoveTo_EmitsEvents()
        {
            var events = new RuleEventBus();
            var service = new MovementService(events);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0));
            
            bool startedFired = false;
            bool completedFired = false;
            events.Subscribe(RuleEventType.MovementStarted, e => startedFired = true);
            events.Subscribe(RuleEventType.MovementCompleted, e => completedFired = true);
            
            service.MoveTo(combatant, new Vector3(5, 0, 0));
            
            Assert.True(startedFired);
            Assert.True(completedFired);
        }

        [Fact]
        public void MoveTo_IncapacitatedCombatant_Fails()
        {
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0));
            combatant.Resources.TakeDamage(200); // Kill it
            
            var result = service.MoveTo(combatant, new Vector3(5, 0, 0));
            
            Assert.False(result.Success);
            Assert.Contains("incapacitated", result.FailureReason);
        }

        [Fact]
        public void MoveTo_MultipleMovements_AccumulateBudgetConsumption()
        {
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);
            
            service.MoveTo(combatant, new Vector3(10, 0, 0)); // 10 used
            service.MoveTo(combatant, new Vector3(15, 0, 0)); // 5 more used
            
            Assert.Equal(15, combatant.ActionBudget.RemainingMovement, 0.01);
        }

        [Fact]
        public void GetDistance_ReturnsCorrectValue()
        {
            var service = new MovementService();
            
            float distance = service.GetDistance(new Vector3(0, 0, 0), new Vector3(3, 4, 0));
            
            Assert.Equal(5f, distance, 0.01);
        }

        [Fact]
        public void Result_ContainsCorrectData()
        {
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0));
            
            var result = service.MoveTo(combatant, new Vector3(10, 0, 0));
            
            Assert.Equal("test", result.CombatantId);
            Assert.Equal(new Vector3(0, 0, 0), result.StartPosition);
            Assert.Equal(new Vector3(10, 0, 0), result.EndPosition);
            Assert.Equal(10f, result.DistanceMoved, 0.01);
        }
    }
}
