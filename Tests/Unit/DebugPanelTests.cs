using Xunit;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules;

namespace QDND.Tests.Unit;

/// <summary>
/// Tests for debug panel logic (without Godot dependencies).
/// Tests verify that debug panel operations work correctly in headless mode.
/// </summary>
public class DebugPanelTests
{
    #region Test Helpers

    private class TestCombatant
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int HP { get; set; }
        public int MaxHP { get; set; }
        public Faction Faction { get; set; }
        public bool IsActive => HP > 0;
    }

    private class TestDebugController
    {
        private readonly List<TestCombatant> _combatants = new();
        private readonly RulesEngine _rulesEngine;

        public int? ForcedRollResult { get; private set; }
        public List<TestCombatant> Combatants => _combatants;

        public TestDebugController(int seed = 42)
        {
            _rulesEngine = new RulesEngine(seed);
        }

        public void AddCombatant(TestCombatant combatant)
        {
            _combatants.Add(combatant);
        }

        public void SpawnUnit(Faction faction)
        {
            var newId = $"spawned_{_combatants.Count}";
            var newUnit = new TestCombatant
            {
                Id = newId,
                Name = $"{faction} Unit {_combatants.Count}",
                HP = 20,
                MaxHP = 20,
                Faction = faction
            };
            _combatants.Add(newUnit);
        }

        public void SetForceRoll(int? value)
        {
            ForcedRollResult = value;
        }

        public int GetNextRoll()
        {
            if (ForcedRollResult.HasValue)
            {
                var result = ForcedRollResult.Value;
                ForcedRollResult = null; // Clear after use
                return result;
            }
            return _rulesEngine.RollAttack(new QueryInput { Type = QueryType.AttackRoll, BaseValue = 0 }).NaturalRoll;
        }

        public string ComputeStateHash()
        {
            // Simple deterministic hash based on combatant state
            var hash = _combatants.Count * 31;
            foreach (var c in _combatants.OrderBy(c => c.Id))
            {
                hash = hash * 31 + c.HP;
                hash = hash * 31 + (c.IsActive ? 1 : 0);
                hash = hash * 31 + (int)c.Faction;
            }
            return hash.ToString("X8");
        }
    }

    #endregion

    #region Spawn Unit Tests

    [Fact]
    public void SpawnUnit_Player_AddsToCombatantsList()
    {
        var controller = new TestDebugController();
        var initialCount = controller.Combatants.Count;

        controller.SpawnUnit(Faction.Player);

        Assert.Equal(initialCount + 1, controller.Combatants.Count);
        Assert.Contains(controller.Combatants, c => c.Faction == Faction.Player);
    }

    [Fact]
    public void SpawnUnit_Enemy_AddsToCombatantsList()
    {
        var controller = new TestDebugController();
        var initialCount = controller.Combatants.Count;

        controller.SpawnUnit(Faction.Hostile);

        Assert.Equal(initialCount + 1, controller.Combatants.Count);
        Assert.Contains(controller.Combatants, c => c.Faction == Faction.Hostile);
    }

    [Fact]
    public void SpawnUnit_SetsDefaultHP()
    {
        var controller = new TestDebugController();

        controller.SpawnUnit(Faction.Player);

        var spawned = controller.Combatants.Last();
        Assert.Equal(20, spawned.HP);
        Assert.Equal(20, spawned.MaxHP);
    }

    #endregion

    #region Force Roll Tests

    [Fact]
    public void ForceRoll_SetsNextRollResult()
    {
        var controller = new TestDebugController();

        controller.SetForceRoll(20);

        var roll = controller.GetNextRoll();
        Assert.Equal(20, roll);
    }

    [Fact]
    public void ForceRoll_ClearsAfterUse()
    {
        var controller = new TestDebugController();

        controller.SetForceRoll(20);
        var roll1 = controller.GetNextRoll();
        var roll2 = controller.GetNextRoll();

        Assert.Equal(20, roll1);
        Assert.NotEqual(20, roll2); // Should be random now
    }

    [Fact]
    public void ForceRoll_Null_DoesNotForce()
    {
        var controller = new TestDebugController(seed: 42);

        controller.SetForceRoll(null);
        var roll = controller.GetNextRoll();

        // Should get a random roll (1-20)
        Assert.InRange(roll, 1, 20);
    }

    #endregion

    #region State Hash Tests

    [Fact]
    public void StateHash_SameState_SameHash()
    {
        var controller1 = new TestDebugController();
        controller1.AddCombatant(new TestCombatant { Id = "a", HP = 20, MaxHP = 20, Faction = Faction.Player });

        var controller2 = new TestDebugController();
        controller2.AddCombatant(new TestCombatant { Id = "a", HP = 20, MaxHP = 20, Faction = Faction.Player });

        var hash1 = controller1.ComputeStateHash();
        var hash2 = controller2.ComputeStateHash();

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void StateHash_DifferentHP_DifferentHash()
    {
        var controller1 = new TestDebugController();
        controller1.AddCombatant(new TestCombatant { Id = "a", HP = 20, MaxHP = 20, Faction = Faction.Player });

        var controller2 = new TestDebugController();
        controller2.AddCombatant(new TestCombatant { Id = "a", HP = 15, MaxHP = 20, Faction = Faction.Player });

        var hash1 = controller1.ComputeStateHash();
        var hash2 = controller2.ComputeStateHash();

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void StateHash_DifferentCombatantCount_DifferentHash()
    {
        var controller1 = new TestDebugController();
        controller1.AddCombatant(new TestCombatant { Id = "a", HP = 20, MaxHP = 20, Faction = Faction.Player });

        var controller2 = new TestDebugController();
        controller2.AddCombatant(new TestCombatant { Id = "a", HP = 20, MaxHP = 20, Faction = Faction.Player });
        controller2.AddCombatant(new TestCombatant { Id = "b", HP = 20, MaxHP = 20, Faction = Faction.Hostile });

        var hash1 = controller1.ComputeStateHash();
        var hash2 = controller2.ComputeStateHash();

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void StateHash_EmptyList_Deterministic()
    {
        var controller1 = new TestDebugController();
        var controller2 = new TestDebugController();

        var hash1 = controller1.ComputeStateHash();
        var hash2 = controller2.ComputeStateHash();

        Assert.Equal(hash1, hash2);
    }

    #endregion
}
