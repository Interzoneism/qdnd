using System;
using Xunit;
using QDND.Tests.Helpers;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Parity validation gate: verifies data/runtime consistency for canonical combat data.
    /// </summary>
    public class ParityValidationTests
    {
        [Fact]
        public void CanonicalData_ParityValidation_Passes()
        {
            var validator = new ParityDataValidator();
            var report = validator.Validate();

            // Always output coverage inventory to CI logs
            if (report.CoverageInventory != null)
            {
                Console.WriteLine("");
                Console.WriteLine("=== Action Coverage Inventory ===");
                Console.WriteLine($"Total actions granted across scenarios: {report.CoverageInventory.TotalGrantedActions}");
                Console.WriteLine($"Actions available in Data/Actions: {report.CoverageInventory.ActionsInDataRegistry}");
                Console.WriteLine($"Forbidden summon actions: {report.CoverageInventory.ForbiddenSummonActions}");
                Console.WriteLine($"Missing actions (granted but not in registry): {report.CoverageInventory.MissingActions}");
                
                if (report.CoverageInventory.GrantedSummonActions > 0)
                {
                    Console.WriteLine($"WARNING: Summon actions granted in scenarios: {report.CoverageInventory.GrantedSummonActions}");
                    Console.WriteLine($"  IDs: {string.Join(", ", report.CoverageInventory.GrantedSummonActionIds)}");
                }
            }

            Assert.False(report.HasErrors, report.Format());
        }
    }
}
