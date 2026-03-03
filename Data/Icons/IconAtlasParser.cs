using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using QDND.Data.Parsers;

namespace QDND.Data.Icons;

/// <summary>
/// Parses BG3 icon atlas LSX files (e.g. Icons_Skills.lsx, Icons_Items.lsx)
/// to extract icon name → UV coordinate mappings.
/// </summary>
public static class IconAtlasParser
{
    /// <summary>
    /// Parse an icon atlas LSX file and return all icon entries.
    /// </summary>
    /// <param name="lsxFilePath">Absolute path to the LSX file.</param>
    /// <param name="atlasName">Name of the atlas (e.g. "Icons_Skills"), stored on each entry.</param>
    /// <returns>List of parsed icon atlas entries.</returns>
    public static List<IconAtlasEntry> Parse(string lsxFilePath, string atlasName)
    {
        var entries = new List<IconAtlasEntry>();

        if (!System.IO.File.Exists(lsxFilePath))
        {
            throw new System.IO.FileNotFoundException($"IconAtlasParser: LSX file not found: {lsxFilePath}", lsxFilePath);
        }

        XDocument doc;
        try
        {
            doc = XDocument.Load(lsxFilePath);
        }
        catch (System.Exception ex)
        {
            RuntimeSafety.LogError($"IconAtlasParser: Failed to load {lsxFilePath}: {ex.Message}");
            return entries;
        }

        // Navigate to region[@id='IconUVList'] → root → children → node[@id='IconUV']
        foreach (var region in doc.Descendants("region"))
        {
            if (region.Attribute("id")?.Value != "IconUVList")
                continue;

            foreach (var iconNode in region.Descendants("node"))
            {
                if (iconNode.Attribute("id")?.Value != "IconUV")
                    continue;

                string mapKey = LsxParser.GetAttributeValue(iconNode, "MapKey");
                if (string.IsNullOrEmpty(mapKey))
                    continue;

                string u1Str = LsxParser.GetAttributeValue(iconNode, "U1");
                string v1Str = LsxParser.GetAttributeValue(iconNode, "V1");
                string u2Str = LsxParser.GetAttributeValue(iconNode, "U2");
                string v2Str = LsxParser.GetAttributeValue(iconNode, "V2");

                if (u1Str == null || v1Str == null || u2Str == null || v2Str == null)
                {
                    RuntimeSafety.LogWarning($"IconAtlasParser: Missing UV values for '{mapKey}' in {atlasName}");
                    continue;
                }

                if (!float.TryParse(u1Str, NumberStyles.Float, CultureInfo.InvariantCulture, out float u1) ||
                    !float.TryParse(v1Str, NumberStyles.Float, CultureInfo.InvariantCulture, out float v1) ||
                    !float.TryParse(u2Str, NumberStyles.Float, CultureInfo.InvariantCulture, out float u2) ||
                    !float.TryParse(v2Str, NumberStyles.Float, CultureInfo.InvariantCulture, out float v2))
                {
                    RuntimeSafety.LogWarning($"IconAtlasParser: Invalid UV float for '{mapKey}' in {atlasName}");
                    continue;
                }

                entries.Add(new IconAtlasEntry
                {
                    Name = mapKey,
                    AtlasFile = atlasName,
                    U1 = u1,
                    V1 = v1,
                    U2 = u2,
                    V2 = v2,
                });
            }
        }

        return entries;
    }
}
