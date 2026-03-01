using QDND.Combat.Statuses;
using QDND.Data.Statuses;
using Xunit;

namespace QDND.Tests.Unit
{
    public class StatusPresentationPolicyTests
    {
        [Fact]
        public void ResolveDisplayName_UsesRawName_WhenNotPlaceholder()
        {
            string name = StatusPresentationPolicy.ResolveDisplayName("Bless", "BLESS");
            Assert.Equal("Bless", name);
        }

        [Fact]
        public void ResolveDisplayName_FallsBackToHumanizedId_WhenPlaceholder()
        {
            string name = StatusPresentationPolicy.ResolveDisplayName("%%% EMPTY", "WEAPON_ACTION_BOOST");
            Assert.Equal("Weapon Action Boost", name);
        }

        [Fact]
        public void ApplyStatusPropertyFlags_ParsesCaseInsensitiveFlags()
        {
            var def = new StatusDefinition { Id = "test", Name = "Test" };
            StatusPresentationPolicy.ApplyStatusPropertyFlags(
                def,
                "DisableOverhead; DisableCombatlog; DisablePortraitIndicator");

            Assert.True(def.HasStatusPropertyFlag("disableoverhead"));
            Assert.True(def.HasStatusPropertyFlag("DisableCombatlog"));
            Assert.True(def.HasStatusPropertyFlag("DISABLEPORTRAITINDICATOR"));
        }

        [Fact]
        public void VisibilityMethods_RespectDisableFlags()
        {
            var def = new StatusDefinition { Id = "test", Name = "Test" };
            StatusPresentationPolicy.ApplyStatusPropertyFlags(
                def,
                "DisableOverhead;DisableCombatlog;DisablePortraitIndicator");

            Assert.False(StatusPresentationPolicy.ShowInOverhead(def));
            Assert.False(StatusPresentationPolicy.ShowInCombatLog(def));
            Assert.False(StatusPresentationPolicy.ShowInPortraitIndicators(def));
        }

        [Fact]
        public void ConvertToStatusDefinition_CarriesFlagsAndResolvedName()
        {
            var bg3 = new BG3StatusData
            {
                StatusId = "WEAPON_ACTION_BOOST",
                DisplayName = "%%% EMPTY",
                StatusPropertyFlags = "DisableOverhead;DisableCombatlog",
                StatusType = BG3StatusType.BOOST
            };

            var def = QDND.Data.Statuses.BG3StatusIntegration.ConvertToStatusDefinition(bg3);

            Assert.Equal("Weapon Action Boost", def.Name);
            Assert.True(def.HasStatusPropertyFlag("DisableOverhead"));
            Assert.True(def.HasStatusPropertyFlag("DisableCombatlog"));
        }
    }
}
