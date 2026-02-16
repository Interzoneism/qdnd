using System;
using System.Linq;
using QDND.Tests.Helpers;
using Xunit;

namespace QDND.Tests.Integration
{
    public class FunctorCoverageTests
    {
        [Fact]
        public void Top20FunctorsByFrequency_AreSupported()
        {
            var metrics = new FunctorCoverageAnalyzer().Analyze(topN: 20);

            Assert.NotEmpty(metrics.TopFunctors);

            foreach (var functor in metrics.TopFunctors)
            {
                Assert.True(
                    functor.FullyHandled,
                    $"Top functor '{functor.Name}' is not fully handled ({functor.HandledCount}/{functor.Count}).");
            }
        }

        [Fact]
        public void SpellFunctorCoverage_Reaches70Percent()
        {
            var metrics = new FunctorCoverageAnalyzer().Analyze(topN: 20);

            Console.WriteLine($"Functor spell coverage: {metrics.FullyHandledSpells}/{metrics.SpellsWithFunctors} ({metrics.SpellCoveragePct:P1})");
            Console.WriteLine("Top functors:");
            foreach (var functor in metrics.TopFunctors.Take(20))
            {
                Console.WriteLine($"  - {functor.Name}: {functor.HandledCount}/{functor.Count}");
            }

            Assert.True(
                metrics.SpellCoveragePct >= 0.70,
                $"Functor spell coverage is below threshold: {metrics.SpellCoveragePct:P1} < 70.0%.");
        }
    }
}
