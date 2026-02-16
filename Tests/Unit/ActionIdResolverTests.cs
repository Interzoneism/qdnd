using System.Collections.Generic;
using Xunit;
using QDND.Data;

namespace QDND.Tests.Unit
{
    public class ActionIdResolverTests
    {
        [Fact]
        public void Resolve_LegacyId_PrefersCanonicalDataAction()
        {
            var dataIds = new[] { "fireball" };
            var bg3Ids = new[] { "Projectile_Fireball" };
            var resolver = new ActionIdResolver(dataIds, bg3Ids);

            var result = resolver.Resolve("Projectile_Fireball");

            Assert.True(result.IsResolved);
            Assert.Equal("fireball", result.ResolvedId);
            Assert.True(result.ExistsInDataRegistry);
        }

        [Fact]
        public void Resolve_CunningActionShape_NormalizesToCanonical()
        {
            var dataIds = new[] { "cunning_action_dash" };
            var resolver = new ActionIdResolver(dataIds);

            var result = resolver.Resolve("Shout_Dash_CunningAction");

            Assert.True(result.IsResolved);
            Assert.Equal("cunning_action_dash", result.ResolvedId);
            Assert.True(result.ExistsInDataRegistry);
        }

        [Fact]
        public void Resolve_Bg3OnlyId_ResolvesWhenNotInData()
        {
            var resolver = new ActionIdResolver(
                dataActionIds: new HashSet<string>(),
                bg3ActionIds: new[] { "Shout_GoblinWarcry" });

            var result = resolver.Resolve("Shout_GoblinWarcry");

            Assert.True(result.IsResolved);
            Assert.Equal("Shout_GoblinWarcry", result.ResolvedId);
            Assert.True(result.ExistsInBg3Registry);
            Assert.False(result.ExistsInDataRegistry);
        }

        [Fact]
        public void Resolve_UnknownId_RemainsUnresolved()
        {
            var resolver = new ActionIdResolver(
                dataActionIds: new[] { "main_hand_attack" },
                bg3ActionIds: new[] { "Projectile_Fireball" });

            var result = resolver.Resolve("Totally_Unknown_Action_987");

            Assert.False(result.IsResolved);
            Assert.Null(result.ResolvedId);
            Assert.False(result.ExistsInDataRegistry);
            Assert.False(result.ExistsInBg3Registry);
        }
    }
}
