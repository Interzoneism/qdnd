using System;
using Xunit;
using QDND.Data.Actions;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for CreateSurface functor parsing with correct BG3 argument order: (radius, duration, surfaceType).
    /// </summary>
    public class CreateSurfaceParserTests
    {
        [Fact]
        public void ParseSingleEffect_CreateSurface_BasicPattern()
        {
            // Arrange: CreateSurface(4,3,SpikeGrowth) → radius=4, duration=3, type=SpikeGrowth
            string functor = "CreateSurface(4,3,SpikeGrowth)";

            // Act
            var effect = SpellEffectConverter.ParseSingleEffect(functor);

            // Assert
            Assert.NotNull(effect);
            Assert.Equal("spawn_surface", effect.Type);
            Assert.Equal(4f, effect.Value); // radius
            Assert.Equal(3, effect.StatusDuration); // duration
            Assert.True(effect.Parameters.ContainsKey("surface_type"));
            Assert.Equal("spikegrowth", effect.Parameters["surface_type"]);
        }

        [Fact]
        public void ParseSingleEffect_CreateSurface_Fire()
        {
            // Arrange: CreateSurface(2,2,Fire) → radius=2, duration=2, type=Fire
            string functor = "CreateSurface(2,2,Fire)";

            // Act
            var effect = SpellEffectConverter.ParseSingleEffect(functor);

            // Assert
            Assert.NotNull(effect);
            Assert.Equal("spawn_surface", effect.Type);
            Assert.Equal(2f, effect.Value);
            Assert.Equal(2, effect.StatusDuration);
            Assert.Equal("fire", effect.Parameters["surface_type"]);
        }

        [Fact]
        public void ParseSingleEffect_CreateSurface_EmptyDuration()
        {
            // Arrange: CreateSurface(3,,Fire) → radius=3, duration=0 (empty), type=Fire
            string functor = "CreateSurface(3,,Fire)";

            // Act
            var effect = SpellEffectConverter.ParseSingleEffect(functor);

            // Assert
            Assert.NotNull(effect);
            Assert.Equal("spawn_surface", effect.Type);
            Assert.Equal(3f, effect.Value);
            Assert.Equal(0, effect.StatusDuration); // Empty duration should default to 0
            Assert.Equal("fire", effect.Parameters["surface_type"]);
        }

        [Fact]
        public void ParseSingleEffect_CreateSurface_ZeroDuration()
        {
            // Arrange: CreateSurface(2,0,Acid) → radius=2, duration=0, type=Acid
            string functor = "CreateSurface(2,0,Acid)";

            // Act
            var effect = SpellEffectConverter.ParseSingleEffect(functor);

            // Assert
            Assert.NotNull(effect);
            Assert.Equal("spawn_surface", effect.Type);
            Assert.Equal(2f, effect.Value);
            Assert.Equal(0, effect.StatusDuration);
            Assert.Equal("acid", effect.Parameters["surface_type"]);
        }

        [Fact]
        public void ParseSingleEffect_CreateSurface_DecimalRadius()
        {
            // Arrange: CreateSurface(1.3,10,Lava) → radius=1.3, duration=10, type=Lava
            string functor = "CreateSurface(1.3,10,Lava)";

            // Act
            var effect = SpellEffectConverter.ParseSingleEffect(functor);

            // Assert
            Assert.NotNull(effect);
            Assert.Equal("spawn_surface", effect.Type);
            Assert.Equal(1.3f, effect.Value);
            Assert.Equal(10, effect.StatusDuration);
            Assert.Equal("lava", effect.Parameters["surface_type"]);
        }

        [Fact]
        public void ParseSingleEffect_CreateSurface_FogCloud()
        {
            // Arrange: CreateSurface(6,3,FogCloud) → radius=6, duration=3, type=FogCloud
            string functor = "CreateSurface(6,3,FogCloud)";

            // Act
            var effect = SpellEffectConverter.ParseSingleEffect(functor);

            // Assert
            Assert.NotNull(effect);
            Assert.Equal("spawn_surface", effect.Type);
            Assert.Equal(6f, effect.Value);
            Assert.Equal(3, effect.StatusDuration);
            Assert.Equal("fogcloud", effect.Parameters["surface_type"]);
        }

        [Fact]
        public void ParseSingleEffect_CreateSurface_WithGROUNDPrefix()
        {
            // Arrange: GROUND:CreateSurface(4,3,SpikeGrowth) - unwrap GROUND: prefix
            string functor = "GROUND:CreateSurface(4,3,SpikeGrowth)";

            // Act
            var effect = SpellEffectConverter.ParseSingleEffect(functor);

            // Assert
            Assert.NotNull(effect);
            Assert.Equal("spawn_surface", effect.Type);
            Assert.Equal(4f, effect.Value);
            Assert.Equal(3, effect.StatusDuration);
            Assert.Equal("spikegrowth", effect.Parameters["surface_type"]);
        }

        [Fact]
        public void ParseSingleEffect_CreateSurface_WithTrailingFlag()
        {
            // Arrange: optional trailing args should not pollute surface_type
            string functor = "CreateSurface(3,-1,Fire,true)";

            // Act
            var effect = SpellEffectConverter.ParseSingleEffect(functor);

            // Assert
            Assert.NotNull(effect);
            Assert.Equal("spawn_surface", effect.Type);
            Assert.Equal(3f, effect.Value);
            Assert.Equal(-1, effect.StatusDuration);
            Assert.Equal("fire", effect.Parameters["surface_type"]);
        }
    }
}
