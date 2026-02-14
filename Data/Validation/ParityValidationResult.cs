using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QDND.Data.Validation
{
    /// <summary>
    /// A single parity validation error.
    /// </summary>
    /// <param name="Category">Error category (e.g., "ActionRegistry", "Scenario", "StatusCrossRef").</param>
    /// <param name="Message">Human-readable description of the problem.</param>
    /// <param name="File">Optional file path where the error was detected.</param>
    public record ParityError(string Category, string Message, string File = null);

    /// <summary>
    /// A single parity validation warning (non-fatal).
    /// </summary>
    /// <param name="Category">Warning category.</param>
    /// <param name="Message">Human-readable description.</param>
    public record ParityWarning(string Category, string Message);

    /// <summary>
    /// Aggregate result of a parity validation run across all registries and scenarios.
    /// </summary>
    public class ParityValidationResult
    {
        /// <summary>True if no errors were found (warnings are acceptable).</summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>Errors that must be fixed.</summary>
        public List<ParityError> Errors { get; } = new();

        /// <summary>Warnings that should be reviewed but are not blocking.</summary>
        public List<ParityWarning> Warnings { get; } = new();

        /// <summary>Total number of individual checks performed.</summary>
        public int TotalChecks { get; set; }

        /// <summary>Print a human-readable summary to the console.</summary>
        public string GetSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Parity Validation Summary ===");
            sb.AppendLine($"Total checks : {TotalChecks}");
            sb.AppendLine($"Errors       : {Errors.Count}");
            sb.AppendLine($"Warnings     : {Warnings.Count}");
            sb.AppendLine($"Result       : {(IsValid ? "PASS" : "FAIL")}");

            if (Errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("--- Errors ---");
                foreach (var e in Errors)
                {
                    string filePart = string.IsNullOrEmpty(e.File) ? "" : $" ({e.File})";
                    sb.AppendLine($"  [{e.Category}]{filePart} {e.Message}");
                }
            }

            if (Warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("--- Warnings ---");
                foreach (var w in Warnings)
                {
                    sb.AppendLine($"  [{w.Category}] {w.Message}");
                }
            }

            return sb.ToString();
        }
    }
}
