using System.Collections.Generic;
using Godot;
using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Environment;

namespace QDND.Tests.Unit
{
    public class LOSServiceTests
    {
        private LOSService CreateService()
        {
            return new LOSService();
        }

        private Combatant CreateCombatant(string id, float x, float y, float z)
        {
            var c = new Combatant(id, id, Faction.Hostile, 10, 10);
            c.Position = new Vector3(x, y, z);
            return c;
        }

        [Fact]
        public void CheckLOS_NoObstacles_HasLOS()
        {
            var service = CreateService();

            var result = service.CheckLOS(Vector3.Zero, new Vector3(10, 0, 0));

            Assert.True(result.HasLineOfSight);
            Assert.Equal(CoverLevel.None, result.Cover);
        }

        [Fact]
        public void CheckLOS_BeyondMaxRange_NoLOS()
        {
            var service = CreateService();
            service.MaxLOSRange = 50;

            var result = service.CheckLOS(Vector3.Zero, new Vector3(100, 0, 0));

            Assert.False(result.HasLineOfSight);
        }

        [Fact]
        public void CheckLOS_FullBlocker_NoLOS()
        {
            var service = CreateService();
            service.RegisterObstacle(new Obstacle
            {
                Id = "wall",
                Position = new Vector3(5, 0, 0),
                BlocksLOS = true,
                ProvidedCover = CoverLevel.Full
            });

            var result = service.CheckLOS(Vector3.Zero, new Vector3(10, 0, 0));

            Assert.False(result.HasLineOfSight);
            Assert.Equal(CoverLevel.Full, result.Cover);
            Assert.Contains("wall", result.Blockers);
        }

        [Fact]
        public void CheckLOS_HalfCover_HasLOSWithCover()
        {
            var service = CreateService();
            service.RegisterObstacle(new Obstacle
            {
                Id = "crate",
                Position = new Vector3(5, 0, 0),
                BlocksLOS = false,
                ProvidedCover = CoverLevel.Half
            });

            var result = service.CheckLOS(Vector3.Zero, new Vector3(10, 0, 0));

            Assert.True(result.HasLineOfSight);
            Assert.Equal(CoverLevel.Half, result.Cover);
        }

        [Fact]
        public void GetACBonus_HalfCover_Returns2()
        {
            var result = new LOSResult { Cover = CoverLevel.Half };
            Assert.Equal(2, result.GetACBonus());
        }

        [Fact]
        public void GetACBonus_ThreeQuarters_Returns5()
        {
            var result = new LOSResult { Cover = CoverLevel.ThreeQuarters };
            Assert.Equal(5, result.GetACBonus());
        }

        [Fact]
        public void CheckLOS_Combatants_OtherProvidesCover()
        {
            var service = CreateService();
            var attacker = CreateCombatant("attacker", 0, 0, 0);
            var blocker = CreateCombatant("blocker", 5, 0, 0);
            var target = CreateCombatant("target", 10, 0, 0);

            service.RegisterCombatant(attacker);
            service.RegisterCombatant(blocker);
            service.RegisterCombatant(target);

            var result = service.CheckLOS(attacker, target);

            Assert.True(result.HasLineOfSight);
            Assert.Equal(CoverLevel.Half, result.Cover);
            Assert.Contains("blocker", result.Blockers);
        }

        [Fact]
        public void HasLineOfSight_Clear_ReturnsTrue()
        {
            var service = CreateService();
            var a = CreateCombatant("a", 0, 0, 0);
            var b = CreateCombatant("b", 10, 0, 0);
            service.RegisterCombatant(a);
            service.RegisterCombatant(b);

            Assert.True(service.HasLineOfSight(a, b));
        }

        [Fact]
        public void GetVisibleTargets_ReturnsInRange()
        {
            var service = CreateService();
            var attacker = CreateCombatant("attacker", 0, 0, 0);
            var near = CreateCombatant("near", 10, 0, 0);
            var far = CreateCombatant("far", 100, 0, 0);

            service.RegisterCombatant(attacker);
            service.RegisterCombatant(near);
            service.RegisterCombatant(far);

            var visible = service.GetVisibleTargets(attacker, 30);

            Assert.Single(visible);
            Assert.Equal("near", visible[0].Id);
        }

        [Fact]
        public void IsFlanked_TwoOpposite_ReturnsTrue()
        {
            var service = CreateService();
            var target = CreateCombatant("target", 0, 0, 0);
            var attackers = new List<Combatant>
            {
                CreateCombatant("a1", 5, 0, 0),  // East
                CreateCombatant("a2", -5, 0, 0)  // West
            };

            Assert.True(service.IsFlanked(target, attackers));
        }

        [Fact]
        public void IsFlanked_SameSide_ReturnsFalse()
        {
            var service = CreateService();
            var target = CreateCombatant("target", 0, 0, 0);
            var attackers = new List<Combatant>
            {
                CreateCombatant("a1", 5, 0, 3),  // NE
                CreateCombatant("a2", 5, 0, -3) // SE
            };

            Assert.False(service.IsFlanked(target, attackers));
        }

        [Fact]
        public void RemoveObstacle_Works()
        {
            var service = CreateService();
            service.RegisterObstacle(new Obstacle { Id = "wall", Position = new Vector3(5, 0, 0), BlocksLOS = true });

            service.RemoveObstacle("wall");

            var result = service.CheckLOS(Vector3.Zero, new Vector3(10, 0, 0));
            Assert.True(result.HasLineOfSight);
        }

        [Fact]
        public void HeightDifference_Calculated()
        {
            var service = CreateService();

            var result = service.CheckLOS(Vector3.Zero, new Vector3(10, 5, 0));

            Assert.Equal(5, result.HeightDifference);
        }

        [Fact]
        public void GetCover_ReturnsCoverLevel()
        {
            var service = CreateService();
            var attacker = CreateCombatant("attacker", 0, 0, 0);
            var blocker = CreateCombatant("blocker", 5, 0, 0);
            var target = CreateCombatant("target", 10, 0, 0);

            service.RegisterCombatant(attacker);
            service.RegisterCombatant(blocker);
            service.RegisterCombatant(target);

            var cover = service.GetCover(attacker, target);

            Assert.Equal(CoverLevel.Half, cover);
        }

        [Fact]
        public void GetSaveBonus_MatchesACBonus()
        {
            var halfCover = new LOSResult { Cover = CoverLevel.Half };
            var threeQuarters = new LOSResult { Cover = CoverLevel.ThreeQuarters };

            Assert.Equal(halfCover.GetACBonus(), halfCover.GetSaveBonus());
            Assert.Equal(threeQuarters.GetACBonus(), threeQuarters.GetSaveBonus());
        }

        [Fact]
        public void ClearObstacles_RemovesAll()
        {
            var service = CreateService();
            service.RegisterObstacle(new Obstacle { Id = "wall1", Position = new Vector3(5, 0, 0), BlocksLOS = true });
            service.RegisterObstacle(new Obstacle { Id = "wall2", Position = new Vector3(15, 0, 0), BlocksLOS = true });

            service.ClearObstacles();

            var result = service.CheckLOS(Vector3.Zero, new Vector3(20, 0, 0));
            Assert.True(result.HasLineOfSight);
        }

        [Fact]
        public void IsFlanked_SingleAttacker_ReturnsFalse()
        {
            var service = CreateService();
            var target = CreateCombatant("target", 0, 0, 0);
            var attackers = new List<Combatant>
            {
                CreateCombatant("a1", 5, 0, 0)
            };

            Assert.False(service.IsFlanked(target, attackers));
        }

        [Fact]
        public void Distance_Calculated()
        {
            var service = CreateService();

            var result = service.CheckLOS(Vector3.Zero, new Vector3(3, 0, 4));

            Assert.Equal(5, result.Distance);
        }
    }
}
