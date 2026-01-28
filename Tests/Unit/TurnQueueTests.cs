using Xunit;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for TurnQueueService initiative ordering and turn progression.
    /// Uses isolated test version to avoid Godot dependencies.
    /// </summary>
    public class TurnQueueTests
    {
        // Simplified test version of combatant
        public enum TestFaction { Player, Hostile, Neutral, Ally }

        public class TestCombatant
        {
            public string Id { get; }
            public string Name { get; set; }
            public TestFaction Faction { get; set; }
            public int Initiative { get; set; }
            public int InitiativeTiebreaker { get; set; }
            public int CurrentHP { get; set; }
            public bool IsActive => CurrentHP > 0;
            public bool IsPlayerControlled => Faction == TestFaction.Player || Faction == TestFaction.Ally;

            public TestCombatant(string id, TestFaction faction, int initiative, int hp = 50)
            {
                Id = id;
                Name = id;
                Faction = faction;
                Initiative = initiative;
                CurrentHP = hp;
            }
        }

        public class TestTurnQueue
        {
            private List<TestCombatant> _combatants = new();
            private List<TestCombatant> _turnOrder = new();
            private int _currentTurnIndex = 0;
            private int _currentRound = 0;

            public int CurrentRound => _currentRound;
            public int CurrentTurnIndex => _currentTurnIndex;
            public TestCombatant? CurrentCombatant => 
                _turnOrder.Count > 0 && _currentTurnIndex < _turnOrder.Count 
                    ? _turnOrder[_currentTurnIndex] 
                    : null;
            public IReadOnlyList<TestCombatant> TurnOrder => _turnOrder;

            public void AddCombatant(TestCombatant combatant)
            {
                _combatants.Add(combatant);
                RecalculateTurnOrder();
            }

            public void StartCombat()
            {
                _currentRound = 1;
                _currentTurnIndex = 0;
                RecalculateTurnOrder();
            }

            public bool AdvanceTurn()
            {
                if (_turnOrder.Count == 0) return false;
                _currentTurnIndex++;
                if (_currentTurnIndex >= _turnOrder.Count)
                {
                    _currentRound++;
                    _currentTurnIndex = 0;
                    RecalculateTurnOrder();
                }
                return _turnOrder.Count > 0;
            }

            private void RecalculateTurnOrder()
            {
                _turnOrder = _combatants
                    .Where(c => c.IsActive)
                    .OrderByDescending(c => c.Initiative)
                    .ThenByDescending(c => c.InitiativeTiebreaker)
                    .ThenBy(c => c.Id)
                    .ToList();
            }
        }

        [Fact]
        public void InitialState_IsEmpty()
        {
            var queue = new TestTurnQueue();
            Assert.Equal(0, queue.CurrentRound);
            Assert.Null(queue.CurrentCombatant);
        }

        [Fact]
        public void AddCombatant_AppearsInQueue()
        {
            var queue = new TestTurnQueue();
            queue.AddCombatant(new TestCombatant("fighter", TestFaction.Player, 15));
            queue.StartCombat();
            
            Assert.Single(queue.TurnOrder);
            Assert.Equal("fighter", queue.CurrentCombatant?.Id);
        }

        [Fact]
        public void TurnOrder_SortedByInitiativeDescending()
        {
            var queue = new TestTurnQueue();
            queue.AddCombatant(new TestCombatant("low", TestFaction.Player, 5));
            queue.AddCombatant(new TestCombatant("high", TestFaction.Hostile, 20));
            queue.AddCombatant(new TestCombatant("mid", TestFaction.Player, 12));
            queue.StartCombat();
            
            Assert.Equal(3, queue.TurnOrder.Count);
            Assert.Equal("high", queue.TurnOrder[0].Id);
            Assert.Equal("mid", queue.TurnOrder[1].Id);
            Assert.Equal("low", queue.TurnOrder[2].Id);
        }

        [Fact]
        public void InitiativeTiebreaker_BreaksTies()
        {
            var queue = new TestTurnQueue();
            queue.AddCombatant(new TestCombatant("a", TestFaction.Player, 15) { InitiativeTiebreaker = 1 });
            queue.AddCombatant(new TestCombatant("b", TestFaction.Player, 15) { InitiativeTiebreaker = 5 });
            queue.AddCombatant(new TestCombatant("c", TestFaction.Player, 15) { InitiativeTiebreaker = 3 });
            queue.StartCombat();
            
            // Higher tiebreaker goes first
            Assert.Equal("b", queue.TurnOrder[0].Id);
            Assert.Equal("c", queue.TurnOrder[1].Id);
            Assert.Equal("a", queue.TurnOrder[2].Id);
        }

        [Fact]
        public void AdvanceTurn_MovesToNextCombatant()
        {
            var queue = new TestTurnQueue();
            queue.AddCombatant(new TestCombatant("first", TestFaction.Player, 20));
            queue.AddCombatant(new TestCombatant("second", TestFaction.Hostile, 10));
            queue.StartCombat();
            
            Assert.Equal("first", queue.CurrentCombatant?.Id);
            queue.AdvanceTurn();
            Assert.Equal("second", queue.CurrentCombatant?.Id);
        }

        [Fact]
        public void AdvanceTurn_WrapsToNewRound()
        {
            var queue = new TestTurnQueue();
            queue.AddCombatant(new TestCombatant("a", TestFaction.Player, 15));
            queue.AddCombatant(new TestCombatant("b", TestFaction.Hostile, 10));
            queue.StartCombat();
            
            Assert.Equal(1, queue.CurrentRound);
            queue.AdvanceTurn(); // a -> b
            queue.AdvanceTurn(); // b -> a (new round)
            
            Assert.Equal(2, queue.CurrentRound);
            Assert.Equal(0, queue.CurrentTurnIndex);
        }

        [Fact]
        public void DeadCombatant_ExcludedFromTurnOrder()
        {
            var queue = new TestTurnQueue();
            var alive = new TestCombatant("alive", TestFaction.Player, 15, hp: 50);
            var dead = new TestCombatant("dead", TestFaction.Hostile, 20, hp: 0);
            queue.AddCombatant(alive);
            queue.AddCombatant(dead);
            queue.StartCombat();
            
            Assert.Single(queue.TurnOrder);
            Assert.Equal("alive", queue.CurrentCombatant?.Id);
        }

        [Fact]
        public void StartCombat_SetsRound1()
        {
            var queue = new TestTurnQueue();
            queue.AddCombatant(new TestCombatant("test", TestFaction.Player, 10));
            queue.StartCombat();
            
            Assert.Equal(1, queue.CurrentRound);
            Assert.Equal(0, queue.CurrentTurnIndex);
        }

        [Fact]
        public void DeterministicOrder_WithSameInitiativeAndTiebreaker()
        {
            // When initiative and tiebreaker are equal, order by ID for determinism
            var queue = new TestTurnQueue();
            queue.AddCombatant(new TestCombatant("charlie", TestFaction.Player, 10));
            queue.AddCombatant(new TestCombatant("alpha", TestFaction.Player, 10));
            queue.AddCombatant(new TestCombatant("bravo", TestFaction.Player, 10));
            queue.StartCombat();
            
            Assert.Equal("alpha", queue.TurnOrder[0].Id);
            Assert.Equal("bravo", queue.TurnOrder[1].Id);
            Assert.Equal("charlie", queue.TurnOrder[2].Id);
        }
    }
}
