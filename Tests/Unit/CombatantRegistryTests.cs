using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using Xunit;

namespace QDND.Tests.Unit
{
    public class CombatantRegistryTests
    {
        [Fact]
        public void Add_WhenCombatantsRegistered_PreservesOrderAndLookup()
        {
            var registry = new CombatantRegistry();
            var a = CreateCombatant("a", Vector3.Zero);
            var b = CreateCombatant("b", new Vector3(1, 0, 0));

            registry.Add(a);
            registry.Add(b);

            Assert.Equal(new[] { "a", "b" }, registry.GetAll().Select(c => c.Id).ToArray());
            Assert.Same(a, registry.Get("a"));
            Assert.Same(b, registry.Get("b"));
        }

        [Fact]
        public void Remove_WhenCombatantExists_RemovesCombatantAndFiresEvent()
        {
            var registry = new CombatantRegistry();
            var removed = new List<string>();
            var a = CreateCombatant("a", Vector3.Zero);
            var b = CreateCombatant("b", new Vector3(1, 0, 0));
            registry.CombatantRemoved += combatant => removed.Add(combatant.Id);

            registry.Add(a);
            registry.Add(b);

            bool result = registry.Remove("a");

            Assert.True(result);
            Assert.Equal(new[] { "a" }, removed);
            Assert.Null(registry.Get("a"));
            Assert.Equal(new[] { "b" }, registry.GetAll().Select(c => c.Id).ToArray());
        }

        [Fact]
        public void ReplaceAll_WhenCalled_ReplacesContentsAndFiresSingleReplaceEvent()
        {
            var registry = new CombatantRegistry();
            var replacedSnapshots = new List<IReadOnlyList<string>>();
            registry.CombatantsReplaced += combatants => replacedSnapshots.Add(combatants.Select(c => c.Id).ToList());

            registry.Add(CreateCombatant("old", Vector3.Zero));

            registry.ReplaceAll(new[]
            {
                CreateCombatant("new_a", Vector3.Zero),
                CreateCombatant("new_b", new Vector3(1, 0, 0))
            });

            Assert.Single(replacedSnapshots);
            Assert.Equal(new[] { "new_a", "new_b" }, replacedSnapshots[0]);
            Assert.Equal(new[] { "new_a", "new_b" }, registry.GetAll().Select(c => c.Id).ToArray());
            Assert.Null(registry.Get("old"));
        }

        [Fact]
        public void Clear_WhenRegistryHasEntries_ClearsContentsAndFiresEvent()
        {
            var registry = new CombatantRegistry();
            bool cleared = false;
            registry.Cleared += () => cleared = true;

            registry.Add(CreateCombatant("a", Vector3.Zero));
            registry.Clear();

            Assert.True(cleared);
            Assert.Empty(registry.GetAll());
        }

        private static Combatant CreateCombatant(string id, Vector3 position)
        {
            var combatant = new Combatant(id, id, Faction.Player, 20, 10)
            {
                Position = position
            };
            return combatant;
        }
    }
}
