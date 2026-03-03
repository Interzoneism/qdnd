using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace QDND.Data.Icons;

/// <summary>
/// Service that resolves BG3 icon names to atlas UV entries and Godot AtlasTexture objects.
/// Data layer (parsing) is testhost-safe. Texture layer requires Godot runtime.
/// </summary>
public class IconService
{
    // Icon name → atlas entry (case-insensitive lookup)
    private readonly Dictionary<string, IconAtlasEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    // Atlas name → loaded Texture2D (lazy-loaded DDS textures)
    private readonly Dictionary<string, Texture2D> _atlasTextures = new();

    // Icon name → created AtlasTexture (cache to avoid re-creation)
    private readonly Dictionary<string, AtlasTexture> _textureCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Known atlas LSX files to parse (filename without extension → atlas name).</summary>
    private static readonly (string FileName, string AtlasName)[] KnownAtlases =
    {
        ("Icons_Skills.lsx", "Icons_Skills"),
        ("Icons_Items.lsx", "Icons_Items"),
        ("Icons_Items_2.lsx", "Icons_Items_2"),
        ("Icons_Items_3.lsx", "Icons_Items_3"),
        ("Icons_Items_4.lsx", "Icons_Items_4"),
        ("Icons_Items_5.lsx", "Icons_Items_5"),
        ("Icons_Items_6.lsx", "Icons_Items_6"),
    };

    /// <summary>Number of icon entries loaded.</summary>
    public int EntryCount => _entries.Count;

    /// <summary>Number of distinct atlas files referenced.</summary>
    public int AtlasCount { get; private set; }

    /// <summary>
    /// Load all icon atlas LSX files from the BG3 data directory.
    /// Testhost-safe — only parses XML, no Godot calls.
    /// </summary>
    /// <param name="bg3DataPath">Absolute path to BG3_Data directory.</param>
    public void LoadFromBG3Data(string bg3DataPath)
    {
        string lsxDir = Path.Combine(bg3DataPath, "Shared", "Public", "Shared", "GUI");

        var loadedAtlases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (fileName, atlasName) in KnownAtlases)
        {
            string lsxPath = Path.Combine(lsxDir, fileName);
            if (!File.Exists(lsxPath))
            {
                RuntimeSafety.Log($"IconService: Atlas LSX not found (skipping): {fileName}");
                continue;
            }

            var entries = IconAtlasParser.Parse(lsxPath, atlasName);
            int added = 0;

            foreach (var entry in entries)
            {
                if (_entries.TryAdd(entry.Name, entry))
                {
                    added++;
                }
                else
                {
                    RuntimeSafety.LogWarning($"IconService: Duplicate icon '{entry.Name}' in {atlasName} (already from {_entries[entry.Name].AtlasFile})");
                }
            }

            if (added > 0)
                loadedAtlases.Add(atlasName);

            RuntimeSafety.Log($"IconService: Parsed {atlasName} — {added} icons added ({entries.Count} total in file)");
        }

        AtlasCount = loadedAtlases.Count;
    }

    /// <summary>
    /// Try to get the atlas entry for a given icon name.
    /// </summary>
    public bool TryGetEntry(string iconName, out IconAtlasEntry entry)
    {
        entry = null;
        if (string.IsNullOrEmpty(iconName))
            return false;
        return _entries.TryGetValue(iconName, out entry);
    }

    /// <summary>
    /// Check if an icon name exists in any loaded atlas.
    /// </summary>
    public bool HasIcon(string iconName)
    {
        if (string.IsNullOrEmpty(iconName))
            return false;
        return _entries.ContainsKey(iconName);
    }

    /// <summary>
    /// Get a Godot AtlasTexture for the given icon name. Requires Godot runtime.
    /// Returns null if the icon is not found or the DDS texture cannot be loaded.
    /// Caches created textures for reuse.
    /// </summary>
    public AtlasTexture GetIconTexture(string iconName)
    {
        if (!RuntimeSafety.ShouldUseGodotInterop)
            return null;

        if (string.IsNullOrEmpty(iconName))
            return null;

        if (_textureCache.TryGetValue(iconName, out var cached))
            return cached;

        if (!_entries.TryGetValue(iconName, out var entry))
            return null;

        // Lazy-load the DDS atlas texture
        if (!_atlasTextures.TryGetValue(entry.AtlasFile, out var ddsTexture))
        {
            string resPath = $"res://BG3_Data/Icons/Public/Shared/Assets/Textures/Icons/{entry.AtlasFile}.dds";
            ddsTexture = GD.Load<Texture2D>(resPath);

            if (ddsTexture == null)
            {
                RuntimeSafety.LogWarning($"IconService: Failed to load DDS atlas: {resPath}");
                return null;
            }

            _atlasTextures[entry.AtlasFile] = ddsTexture;
        }

        // Create AtlasTexture from UV coordinates
        var atlas = new AtlasTexture();
        atlas.Atlas = ddsTexture;
        atlas.Region = new Rect2(
            entry.U1 * ddsTexture.GetWidth(),
            entry.V1 * ddsTexture.GetHeight(),
            (entry.U2 - entry.U1) * ddsTexture.GetWidth(),
            (entry.V2 - entry.V1) * ddsTexture.GetHeight()
        );

        _textureCache[iconName] = atlas;
        return atlas;
    }
}
