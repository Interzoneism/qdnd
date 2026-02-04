#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace QDND.Editor;

/// <summary>
/// Helper utilities for editor tools.
/// </summary>
public static class EditorHelpers
{
    /// <summary>
    /// Load all JSON files from a directory.
    /// </summary>
    public static List<(string Path, T Data)> LoadAllFromDirectory<T>(string directory)
    {
        var results = new List<(string, T)>();

        if (!Directory.Exists(directory))
            return results;

        foreach (var file in Directory.GetFiles(directory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var data = JsonSerializer.Deserialize<T>(json, JsonOptions);
                if (data != null)
                {
                    results.Add((file, data));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load {file}: {ex.Message}");
            }
        }

        return results;
    }

    /// <summary>
    /// Save data to JSON file.
    /// </summary>
    public static bool SaveToFile<T>(string path, T data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save {path}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Validate a file path is within the project.
    /// </summary>
    public static bool IsValidProjectPath(string path, string projectRoot)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetFullPath(projectRoot);
        return full.StartsWith(root);
    }

    /// <summary>
    /// Get relative path from project root.
    /// </summary>
    public static string GetRelativePath(string fullPath, string projectRoot)
    {
        return Path.GetRelativePath(projectRoot, fullPath);
    }

    private static JsonSerializerOptions JsonOptions => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
