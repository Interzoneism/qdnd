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

            Assert.False(report.HasErrors, report.Format());
        }
    }
}
