using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Targeting;
using QDND.Combat.Actions;
using System.Collections.Generic;
using Godot;

namespace QDND.Tests.Unit
{
    public class PositionSystemTests
    {
        private Combatant CreateCombatant(string id, Vector3 position, Faction faction = Faction.Player)
        {
            var c = new Combatant(id, $"Test_{id}", faction, 100, 10);
            c.Position = position;
            return c;
        }

        private ActionDefinition CreateAbility(float range, TargetFilter filter = TargetFilter.Enemies)
        {
            return new ActionDefinition
            {
                Id = "test_ability",
                Name = "Test",
                TargetType = TargetType.SingleUnit,
                TargetFilter = filter,
                Range = range
            };
        }

        #region Combatant Position Tests

        [Fact]
        public void Combatant_Position_DefaultsToZero()
        {
            var c = new Combatant("test", "Test", Faction.Player, 100, 10);

            Assert.Equal(Vector3.Zero, c.Position);
        }

        [Fact]
        public void Combatant_Position_CanBeSet()
        {
            var c = CreateCombatant("test", new Vector3(10, 5, 0));

            Assert.Equal(new Vector3(10, 5, 0), c.Position);
        }

        [Fact]
        public void Combatant_Position_CanBeModified()
        {
            var c = CreateCombatant("test", new Vector3(0, 0, 0));
            c.Position = new Vector3(5, 3, 2);

            Assert.Equal(new Vector3(5, 3, 2), c.Position);
        }

        #endregion

        #region Distance Calculation Tests

        [Fact]
        public void DistanceCalculation_SamePosition_IsZero()
        {
            var source = CreateCombatant("source", new Vector3(5, 5, 5));
            var target = CreateCombatant("target", new Vector3(5, 5, 5));

            float distance = source.Position.DistanceTo(target.Position);

            Assert.Equal(0, distance, 0.001);
        }

        [Fact]
        public void DistanceCalculation_HorizontalDistance()
        {
            var source = CreateCombatant("source", new Vector3(0, 0, 0));
            var target = CreateCombatant("target", new Vector3(5, 0, 0));

            float distance = source.Position.DistanceTo(target.Position);

            Assert.Equal(5, distance, 0.001);
        }

        [Fact]
        public void DistanceCalculation_Works3D()
        {
            var source = CreateCombatant("source", new Vector3(0, 0, 0));
            var target = CreateCombatant("target", new Vector3(3, 4, 0)); // 3-4-5 triangle

            float distance = source.Position.DistanceTo(target.Position);

            Assert.Equal(5, distance, 0.001);
        }

        [Fact]
        public void DistanceCalculation_Full3D()
        {
            var source = CreateCombatant("source", new Vector3(0, 0, 0));
            var target = CreateCombatant("target", new Vector3(2, 2, 1)); // sqrt(9) = 3

            float distance = source.Position.DistanceTo(target.Position);

            Assert.Equal(3, distance, 0.001);
        }

        #endregion

        #region TargetValidator IsInRange Tests

        [Fact]
        public void TargetValidator_IsInRange_TrueWhenClose()
        {
            var validator = new TargetValidator();
            var source = CreateCombatant("source", new Vector3(0, 0, 0));
            var target = CreateCombatant("target", new Vector3(3, 0, 0));

            Assert.True(validator.IsInRange(source, target, 5));
        }

        [Fact]
        public void TargetValidator_IsInRange_FalseWhenFar()
        {
            var validator = new TargetValidator();
            var source = CreateCombatant("source", new Vector3(0, 0, 0));
            var target = CreateCombatant("target", new Vector3(10, 0, 0));

            Assert.False(validator.IsInRange(source, target, 5));
        }

        [Fact]
        public void TargetValidator_IsInRange_TrueAtExactRange()
        {
            var validator = new TargetValidator();
            var source = CreateCombatant("source", new Vector3(0, 0, 0));
            var target = CreateCombatant("target", new Vector3(5, 0, 0));

            Assert.True(validator.IsInRange(source, target, 5));
        }

        [Fact]
        public void TargetValidator_IsInRange_FalseJustOutside()
        {
            var validator = new TargetValidator();
            var source = CreateCombatant("source", new Vector3(0, 0, 0));
            var target = CreateCombatant("target", new Vector3(5.1f, 0, 0));

            Assert.False(validator.IsInRange(source, target, 5));
        }

        #endregion

        #region ValidateSingleTarget Range Check Tests

        [Fact]
        public void ValidateSingleTarget_InRange_IsValid()
        {
            var validator = new TargetValidator();
            var source = CreateCombatant("source", new Vector3(0, 0, 0), Faction.Player);
            var target = CreateCombatant("target", new Vector3(3, 0, 0), Faction.Hostile);
            var action = CreateAbility(5f);

            var result = validator.ValidateSingleTarget(action, source, target);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void ValidateSingleTarget_OutOfRange_IsInvalid()
        {
            var validator = new TargetValidator();
            var source = CreateCombatant("source", new Vector3(0, 0, 0), Faction.Player);
            var target = CreateCombatant("target", new Vector3(10, 0, 0), Faction.Hostile);
            var action = CreateAbility(5f);

            var result = validator.ValidateSingleTarget(action, source, target);

            Assert.False(result.IsValid);
            Assert.Contains("out of range", result.Reason);
        }

        [Fact]
        public void ValidateSingleTarget_ZeroRange_SkipsRangeCheck()
        {
            var validator = new TargetValidator();
            var source = CreateCombatant("source", new Vector3(0, 0, 0), Faction.Player);
            var target = CreateCombatant("target", new Vector3(100, 0, 0), Faction.Hostile);
            var action = CreateAbility(0f); // Range 0 means unlimited

            var result = validator.ValidateSingleTarget(action, source, target);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_MultipleTargets_FiltersOutOfRange()
        {
            var validator = new TargetValidator();
            var source = CreateCombatant("source", new Vector3(0, 0, 0), Faction.Player);
            var nearTarget = CreateCombatant("near", new Vector3(3, 0, 0), Faction.Hostile);
            var farTarget = CreateCombatant("far", new Vector3(10, 0, 0), Faction.Hostile);
            var action = CreateAbility(5f);
            action.MaxTargets = 5;

            var result = validator.Validate(
                action,
                source,
                new List<Combatant> { nearTarget, farTarget },
                new List<Combatant> { source, nearTarget, farTarget }
            );

            Assert.True(result.IsValid);
            Assert.Single(result.ValidTargets);
            Assert.Equal("near", result.ValidTargets[0].Id);
            Assert.Single(result.InvalidTargets);
            Assert.Contains("out of range", result.InvalidTargets[0].Reason);
        }

        #endregion
    }
}
