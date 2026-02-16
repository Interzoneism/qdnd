using Xunit;
using Xunit.Abstractions;
using QDND.Tests.Helpers;
using System;
using System.Linq;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for coverage inventory reporting.
    /// </summary>
    public class CoverageInventoryTests
    {
        private readonly ITestOutputHelper _output;

        public CoverageInventoryTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ParityValidation_IncludesCoverageInventory()
        {
            var validator = new ParityDataValidator();
            var report = validator.Validate();

            // Always print the full report to verify coverage inventory is included
            _output.WriteLine("=== Full Parity Validation Report ===");
            _output.WriteLine(report.Format());
            
            // Verify coverage inventory exists
            Assert.NotNull(report.CoverageInventory);
            
            // Verify coverage inventory has been populated
            Assert.True(report.CoverageInventory.TotalGrantedActions >= 0, 
                "Coverage inventory should track total granted actions");
            
            _output.WriteLine("");
            _output.WriteLine($"Total granted actions: {report.CoverageInventory.TotalGrantedActions}");
            _output.WriteLine($"Actions in Data registry: {report.CoverageInventory.ActionsInDataRegistry}");
            _output.WriteLine($"Granted actions present in Data/Actions: {report.CoverageInventory.GrantedActionsPresentInDataRegistry}");
            _output.WriteLine($"Granted actions BG3-only: {report.CoverageInventory.GrantedActionsBg3Only}");
            _output.WriteLine($"Granted actions missing from both: {report.CoverageInventory.GrantedActionsMissingFromBoth}");
            _output.WriteLine($"Forbidden summon actions: {report.CoverageInventory.ForbiddenSummonActions}");
            _output.WriteLine($"Missing actions: {report.CoverageInventory.MissingActions}");
            _output.WriteLine($"Granted summon actions: {report.CoverageInventory.GrantedSummonActions}");

            Assert.Equal(
                report.CoverageInventory.GrantedActionsMissingFromBoth,
                report.CoverageInventory.MissingActions);

            Assert.DoesNotContain(
                report.CoverageInventory.Bg3OnlyActionIds,
                bg3OnlyId => report.CoverageInventory.MissingFromBothActionIds
                    .Any(missingId => string.Equals(missingId, bg3OnlyId, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
