using System.Collections.Generic;
using Godot;
using Xunit;
using QDND.Combat.Services;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for GroundItemService.
    /// </summary>
    public class GroundItemServiceTests
    {
        private static GroundItemService.GroundWeapon MakeWeapon(
            string id, Vector3? position = null, float avgDmg = 5f,
            bool isRanged = false, string prevOwner = null, string prevFaction = null,
            string weaponType = "Longsword", string weaponCategory = "Martial")
        {
            return new GroundItemService.GroundWeapon
            {
                Id = id,
                Name = id,
                Position = position ?? Vector3.Zero,
                AverageDamage = avgDmg,
                IsRanged = isRanged,
                PreviousOwnerId = prevOwner,
                PreviousOwnerFaction = prevFaction,
                WeaponType = weaponType,
                WeaponCategory = weaponCategory
            };
        }

        [Fact]
        public void DropWeapon_AddsToList()
        {
            var service = new GroundItemService();
            var weapon = MakeWeapon("sword1");

            service.DropWeapon(weapon);

            Assert.Single(service.GetAllWeapons());
            Assert.Equal("sword1", service.GetAllWeapons()[0].Id);
        }

        [Fact]
        public void PickupWeapon_RemovesFromList()
        {
            var service = new GroundItemService();
            service.DropWeapon(MakeWeapon("sword1"));
            service.DropWeapon(MakeWeapon("bow1"));

            bool result = service.PickupWeapon("sword1");

            Assert.True(result);
            Assert.Single(service.GetAllWeapons());
            Assert.Equal("bow1", service.GetAllWeapons()[0].Id);
        }

        [Fact]
        public void PickupWeapon_NonExistent_ReturnsFalse()
        {
            var service = new GroundItemService();
            service.DropWeapon(MakeWeapon("sword1"));

            bool result = service.PickupWeapon("does_not_exist");

            Assert.False(result);
            Assert.Single(service.GetAllWeapons());
        }

        [Fact]
        public void GetWeaponsInRadius_FiltersCorrectly()
        {
            var service = new GroundItemService();
            service.DropWeapon(MakeWeapon("near", position: new Vector3(3, 0, 0)));
            service.DropWeapon(MakeWeapon("far", position: new Vector3(50, 0, 0)));

            var results = service.GetWeaponsInRadius(Vector3.Zero, 10f);

            Assert.Single(results);
            Assert.Equal("near", results[0].Id);
        }

        [Fact]
        public void GetWeaponsInRadius_UsesXZDistance()
        {
            var service = new GroundItemService();
            // Weapon at (3, 100, 0) — large Y distance but XZ distance is only 3
            service.DropWeapon(MakeWeapon("elevated", position: new Vector3(3, 100, 0)));
            // Weapon at (20, 0, 0) — XZ distance of 20
            service.DropWeapon(MakeWeapon("far_xz", position: new Vector3(20, 0, 0)));

            var results = service.GetWeaponsInRadius(Vector3.Zero, 10f);

            Assert.Single(results);
            Assert.Equal("elevated", results[0].Id);
        }

        [Fact]
        public void Clear_RemovesAll()
        {
            var service = new GroundItemService();
            service.DropWeapon(MakeWeapon("sword1"));
            service.DropWeapon(MakeWeapon("bow1"));
            service.DropWeapon(MakeWeapon("axe1"));

            service.Clear();

            Assert.Empty(service.GetAllWeapons());
        }
    }
}
