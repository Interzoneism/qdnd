#nullable enable
using System;
using System.IO;
using System.Text.Json;

namespace QDND.Combat.Persistence;

/// <summary>
/// Handles reading and writing save files to disk.
/// </summary>
public class SaveFileManager
{
    private readonly string _basePath;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Create a SaveFileManager with the specified base path.
    /// For Godot, use ProjectSettings.GlobalizePath("user://saves/")
    /// </summary>
    public SaveFileManager(string basePath)
    {
        _basePath = basePath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Ensure directory exists
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    /// <summary>
    /// Sanitize and validate a filename to prevent path traversal.
    /// </summary>
    private string SanitizePath(string filename)
    {
        // Reject path separators
        if (filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
            throw new ArgumentException("Invalid filename: path traversal not allowed", nameof(filename));

        // Ensure .json extension
        if (!filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            filename = filename + ".json";

        var fullPath = Path.Combine(_basePath, filename);

        // Verify path is under basePath
        var normalizedBase = Path.GetFullPath(_basePath);
        var normalizedFull = Path.GetFullPath(fullPath);
        if (!normalizedFull.StartsWith(normalizedBase))
            throw new ArgumentException("Invalid filename: path outside save directory", nameof(filename));

        return fullPath;
    }

    /// <summary>
    /// Write a snapshot to file.
    /// </summary>
    public SaveResult WriteSnapshot(CombatSnapshot snapshot, string filename)
    {
        try
        {
            var path = SanitizePath(filename);
            var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
            File.WriteAllText(path, json);
            return SaveResult.Success(path);
        }
        catch (Exception ex)
        {
            return SaveResult.Failure($"Failed to write save: {ex.Message}");
        }
    }

    /// <summary>
    /// Read a snapshot from file.
    /// </summary>
    public LoadResult<CombatSnapshot> ReadSnapshot(string filename)
    {
        try
        {
            var path = SanitizePath(filename);
            if (!File.Exists(path))
                return LoadResult<CombatSnapshot>.Failure($"File not found: {path}");

            var json = File.ReadAllText(path);
            var snapshot = JsonSerializer.Deserialize<CombatSnapshot>(json, _jsonOptions);

            if (snapshot == null)
                return LoadResult<CombatSnapshot>.Failure("Failed to deserialize snapshot");

            return LoadResult<CombatSnapshot>.Success(snapshot);
        }
        catch (JsonException ex)
        {
            return LoadResult<CombatSnapshot>.Failure($"Invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            return LoadResult<CombatSnapshot>.Failure($"Failed to read save: {ex.Message}");
        }
    }

    /// <summary>
    /// List available save files.
    /// </summary>
    public string[] ListSaveFiles()
    {
        if (!Directory.Exists(_basePath))
            return Array.Empty<string>();
        return Directory.GetFiles(_basePath, "*.json");
    }

    /// <summary>
    /// Delete a save file.
    /// </summary>
    public bool DeleteSave(string filename)
    {
        try
        {
            var path = SanitizePath(filename);
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
            return false;
        }
        catch (ArgumentException)
        {
            // Invalid filename - treat as not found
            return false;
        }
    }
}

/// <summary>Result of a save operation.</summary>
public class SaveResult
{
    public bool IsSuccess { get; private set; }
    public string? Error { get; private set; }
    public string? FilePath { get; private set; }

    public static SaveResult Success(string path) => new() { IsSuccess = true, FilePath = path };
    public static SaveResult Failure(string error) => new() { IsSuccess = false, Error = error };
}

/// <summary>Result of a load operation.</summary>
public class LoadResult<T>
{
    public bool IsSuccess { get; private set; }
    public string? Error { get; private set; }
    public T? Value { get; private set; }

    public static LoadResult<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static LoadResult<T> Failure(string error) => new() { IsSuccess = false, Error = error };
}
