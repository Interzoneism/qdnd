namespace QDND.Data.Icons;

/// <summary>
/// A single icon UV mapping from a BG3 atlas LSX file.
/// Maps an icon name to UV coordinates within a DDS atlas texture.
/// </summary>
public class IconAtlasEntry
{
    /// <summary>MapKey, e.g. "Action_AbsolutePower".</summary>
    public string Name { get; set; }

    /// <summary>Atlas file name without extension, e.g. "Icons_Skills".</summary>
    public string AtlasFile { get; set; }

    public float U1 { get; set; }
    public float V1 { get; set; }
    public float U2 { get; set; }
    public float V2 { get; set; }
}
