namespace QDND.Data.AI
{
    /// <summary>
    /// Surface interaction combo parsed from BG3 AI combos.txt.
    /// </summary>
    public class BG3AISurfaceComboDefinition
    {
        public string Type { get; set; } = string.Empty;
        public string Start { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string Cause { get; set; } = string.Empty;
    }
}
