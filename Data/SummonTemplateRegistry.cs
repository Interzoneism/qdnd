using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QDND.Data
{
    /// <summary>
    /// Data class representing a summon creature template.
    /// Maps a templateId to the actions, tags, and metadata the summoned entity should receive.
    /// </summary>
    public class SummonTemplate
    {
        [JsonPropertyName("templateId")]
        public string TemplateId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("knownActions")]
        public List<string> KnownActions { get; set; } = new();

        [JsonPropertyName("tags")]
        public HashSet<string> Tags { get; set; } = new();

        [JsonPropertyName("size")]
        public string Size { get; set; } = "Medium";
    }

    /// <summary>
    /// Static registry that maps templateId → SummonTemplate.
    /// Loaded from a JSON file at startup. Testhost-safe (uses System.IO via RuntimeSafety).
    /// </summary>
    public static class SummonTemplateRegistry
    {
        private static readonly Dictionary<string, SummonTemplate> _templates = new(StringComparer.OrdinalIgnoreCase);
        private static bool _initialized;

        /// <summary>Number of templates currently loaded.</summary>
        public static int Count => _templates.Count;

        /// <summary>
        /// Load summon templates from a JSON file.
        /// Safe to call from both Godot and testhost environments.
        /// </summary>
        /// <param name="jsonPath">
        /// Path to the JSON file. Supports both absolute paths and res:// paths.
        /// </param>
        public static void Initialize(string jsonPath)
        {
            _templates.Clear();
            _initialized = false;

            if (!RuntimeSafety.TryReadText(jsonPath, out var json))
            {
                RuntimeSafety.LogWarning($"[SummonTemplateRegistry] Template file not found: {jsonPath}");
                _initialized = true;
                return;
            }

            try
            {
                var wrapper = JsonSerializer.Deserialize<TemplateFileWrapper>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (wrapper?.Templates != null)
                {
                    foreach (var template in wrapper.Templates)
                    {
                        if (string.IsNullOrEmpty(template.TemplateId))
                        {
                            RuntimeSafety.LogWarning("[SummonTemplateRegistry] Skipping template with empty templateId");
                            continue;
                        }

                        _templates[template.TemplateId] = template;
                    }
                }

                RuntimeSafety.Log($"[SummonTemplateRegistry] Loaded {_templates.Count} summon templates from {jsonPath}");
            }
            catch (Exception ex)
            {
                RuntimeSafety.LogError($"[SummonTemplateRegistry] Failed to parse {jsonPath}: {ex.Message}");
            }

            _initialized = true;
        }

        /// <summary>
        /// Try to retrieve a summon template by its templateId.
        /// </summary>
        public static bool TryGetTemplate(string templateId, out SummonTemplate template)
        {
            if (!_initialized)
            {
                RuntimeSafety.LogWarning("[SummonTemplateRegistry] Accessed before initialization");
            }

            if (string.IsNullOrEmpty(templateId))
            {
                template = null;
                return false;
            }

            return _templates.TryGetValue(templateId, out template);
        }

        /// <summary>Reset the registry (useful for tests).</summary>
        public static void Clear()
        {
            _templates.Clear();
            _initialized = false;
        }

        private class TemplateFileWrapper
        {
            [JsonPropertyName("templates")]
            public List<SummonTemplate> Templates { get; set; } = new();
        }
    }
}
