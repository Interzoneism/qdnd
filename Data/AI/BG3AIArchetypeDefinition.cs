using System.Collections.Generic;

namespace QDND.Data.AI
{
    /// <summary>
    /// Modifier operation in BG3 archetype files.
    /// </summary>
    public enum BG3AIModifierOperation
    {
        Set,
        Add,
        Subtract
    }

    /// <summary>
    /// Single numeric modifier parsed from an archetype file.
    /// </summary>
    public class BG3AIModifier
    {
        public string Key { get; set; } = string.Empty;
        public float Value { get; set; }
        public BG3AIModifierOperation Operation { get; set; } = BG3AIModifierOperation.Set;
        public int LineNumber { get; set; }
    }

    /// <summary>
    /// Parsed BG3 AI archetype definition.
    /// </summary>
    public class BG3AIArchetypeDefinition
    {
        /// <summary>
        /// Stable archetype ID (relative file path without extension, e.g. "melee" or "TACTICIAN/base").
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Original file path.
        /// </summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>
        /// Parent archetype references from USING directives.
        /// </summary>
        public List<string> Parents { get; } = new();

        /// <summary>
        /// Ordered modifiers declared in this file.
        /// </summary>
        public List<BG3AIModifier> Modifiers { get; } = new();
    }
}
