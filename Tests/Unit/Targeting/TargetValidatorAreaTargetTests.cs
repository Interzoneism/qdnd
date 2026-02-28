using System.Collections.Generic;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Environment;
using QDND.Combat.Targeting;
using QDND.Tests.Helpers;
using Xunit;

namespace QDND.Tests.Unit.Targeting
{
    public class TargetValidatorAreaTargetTests
    {
        [Fact]
        public void ResolveAreaTargets_ConeUsesSourceForLineOfEffect()
        {
            var source = TestHelpers.MakeCombatant(id: "source", faction: QDND.Combat.Entities.Faction.Player);
            source.Position = new Vector3(0f, 0f, 0f);

            var target = TestHelpers.MakeCombatant(id: "target", faction: QDND.Combat.Entities.Faction.Hostile);
            target.Position = new Vector3(3f, 0f, 0f);

            var los = new LOSService();
            // Blocks LOS from cast point (x=4.5) back to target (x=3.0), but not from source (x=0) to target.
            los.RegisterObstacle(new Obstacle
            {
                Id = "cast_point_blocker",
                Position = new Vector3(3.75f, 0f, 0f),
                Width = 1f,
                Height = 3f,
                BlocksLOS = true
            });

            var validator = new TargetValidator(los, c => c.Position);
            var action = new ActionDefinition
            {
                Id = "burning_hands",
                TargetType = TargetType.Cone,
                TargetFilter = TargetFilter.Enemies,
                Range = 4.5f,
                ConeAngle = 60f
            };

            var targets = validator.ResolveAreaTargets(
                action,
                source,
                new Vector3(4.5f, 0f, 0f),
                new List<QDND.Combat.Entities.Combatant> { source, target },
                c => c.Position);

            Assert.Contains(target, targets);
        }

        [Fact]
        public void ResolveAreaTargets_PointCanHitTargetAtCastPoint()
        {
            var source = TestHelpers.MakeCombatant(id: "source", faction: QDND.Combat.Entities.Faction.Player);
            source.Position = new Vector3(0f, 0f, 0f);

            var target = TestHelpers.MakeCombatant(id: "target", faction: QDND.Combat.Entities.Faction.Hostile);
            target.Position = new Vector3(2f, 0f, 0f);

            var los = new LOSService();
            var validator = new TargetValidator(los, c => c.Position);
            var action = new ActionDefinition
            {
                Id = "point_damage",
                TargetType = TargetType.Point,
                TargetFilter = TargetFilter.Enemies,
                AreaRadius = 0f
            };

            var targets = validator.ResolveAreaTargets(
                action,
                source,
                target.Position,
                new List<QDND.Combat.Entities.Combatant> { source, target },
                c => c.Position);

            Assert.Contains(target, targets);
        }
    }
}
