using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QDND.Data.Parsers;

namespace QDND.Data.AI
{
    /// <summary>
    /// Registry and inheritance resolver for BG3 AI archetypes and combos.
    /// </summary>
    public class BG3AIRegistry
    {
        private readonly Dictionary<string, BG3AIArchetypeDefinition> _archetypesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _aliasToId = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _ambiguousAliases = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, float>> _resolvedSettings = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<BG3AISurfaceComboDefinition> _surfaceCombos = new();
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        /// <summary>Parsed archetypes keyed by stable ID.</summary>
        public IReadOnlyDictionary<string, BG3AIArchetypeDefinition> Archetypes => _archetypesById;

        /// <summary>Parsed surface combos.</summary>
        public IReadOnlyList<BG3AISurfaceComboDefinition> SurfaceCombos => _surfaceCombos;

        /// <summary>Errors encountered during load/resolve.</summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>Warnings encountered during load/resolve.</summary>
        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>
        /// Load BG3 AI archetypes and combos from the BG3_Data/AI directory.
        /// </summary>
        public bool LoadFromDirectory(string aiDirectory)
        {
            Clear();

            if (!Directory.Exists(aiDirectory))
            {
                _errors.Add($"BG3 AI directory not found: {aiDirectory}");
                return false;
            }

            var archetypesDirectory = Path.Combine(aiDirectory, "Archetypes");
            var archetypeParser = new BG3AIArchetypeParser();
            var definitions = archetypeParser.ParseDirectory(archetypesDirectory);

            foreach (var error in archetypeParser.Errors)
            {
                _errors.Add(error);
            }

            foreach (var warning in archetypeParser.Warnings)
            {
                _warnings.Add(warning);
            }

            foreach (var definition in definitions)
            {
                RegisterArchetype(definition);
            }

            var comboPath = Path.Combine(aiDirectory, "combos.txt");
            var comboParser = new BG3AIComboParser();
            var combos = comboParser.ParseFile(comboPath);
            _surfaceCombos.AddRange(combos);

            foreach (var error in comboParser.Errors)
            {
                _errors.Add(error);
            }

            foreach (var warning in comboParser.Warnings)
            {
                _warnings.Add(warning);
            }

            // Force full resolution upfront so unresolved parent/cycle errors are surfaced on load.
            foreach (var archetypeId in _archetypesById.Keys.ToList())
            {
                TryGetResolvedSettings(archetypeId, out _);
            }

            return _errors.Count == 0;
        }

        /// <summary>
        /// Returns true when an archetype exists by ID or unique alias.
        /// </summary>
        public bool HasArchetype(string idOrAlias)
        {
            return TryResolveId(idOrAlias, out _);
        }

        /// <summary>
        /// Try to get the resolved settings for an archetype.
        /// </summary>
        public bool TryGetResolvedSettings(string idOrAlias, out Dictionary<string, float> settings)
        {
            settings = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            if (!TryResolveId(idOrAlias, out var archetypeId))
            {
                _warnings.Add($"Unknown AI archetype '{idOrAlias}'");
                return false;
            }

            var resolved = ResolveArchetypeSettings(archetypeId, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            if (resolved == null)
            {
                return false;
            }

            settings = new Dictionary<string, float>(resolved, StringComparer.OrdinalIgnoreCase);
            return true;
        }

        /// <summary>
        /// Merge a base archetype with optional overlay archetypes.
        /// Overlay keys overwrite existing keys.
        /// </summary>
        public bool TryGetMergedSettings(string baseArchetypeIdOrAlias, out Dictionary<string, float> settings, params string[] overlayArchetypes)
        {
            settings = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            if (!TryGetResolvedSettings(baseArchetypeIdOrAlias, out var baseSettings))
            {
                return false;
            }

            settings = baseSettings;

            if (overlayArchetypes == null || overlayArchetypes.Length == 0)
            {
                return true;
            }

            foreach (var overlay in overlayArchetypes)
            {
                if (string.IsNullOrWhiteSpace(overlay))
                {
                    continue;
                }

                if (!TryGetResolvedSettings(overlay, out var overlaySettings))
                {
                    continue;
                }

                foreach (var kvp in overlaySettings)
                {
                    settings[kvp.Key] = kvp.Value;
                }
            }

            return true;
        }

        private void RegisterArchetype(BG3AIArchetypeDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                _warnings.Add("Skipping invalid null/empty AI archetype definition");
                return;
            }

            _archetypesById[definition.Id] = definition;

            var alias = Path.GetFileName(definition.Id);
            if (string.IsNullOrEmpty(alias))
            {
                return;
            }

            if (_ambiguousAliases.Contains(alias))
            {
                return;
            }

            if (_aliasToId.TryGetValue(alias, out var existingId) &&
                !string.Equals(existingId, definition.Id, StringComparison.OrdinalIgnoreCase))
            {
                _aliasToId.Remove(alias);
                _ambiguousAliases.Add(alias);
                return;
            }

            _aliasToId[alias] = definition.Id;
        }

        private bool TryResolveId(string idOrAlias, out string resolvedId)
        {
            resolvedId = string.Empty;
            if (string.IsNullOrWhiteSpace(idOrAlias))
            {
                return false;
            }

            var normalized = NormalizeId(idOrAlias);
            if (_archetypesById.ContainsKey(normalized))
            {
                resolvedId = normalized;
                return true;
            }

            var alias = Path.GetFileName(normalized);
            if (_aliasToId.TryGetValue(alias, out var aliasId))
            {
                resolvedId = aliasId;
                return true;
            }

            return false;
        }

        private Dictionary<string, float> ResolveArchetypeSettings(string archetypeId, HashSet<string> resolutionStack)
        {
            if (_resolvedSettings.TryGetValue(archetypeId, out var cached))
            {
                return cached;
            }

            if (!_archetypesById.TryGetValue(archetypeId, out var definition))
            {
                _errors.Add($"Cannot resolve unknown archetype '{archetypeId}'");
                return null;
            }

            if (!resolutionStack.Add(archetypeId))
            {
                _errors.Add($"Cycle detected while resolving AI archetypes at '{archetypeId}'");
                return null;
            }

            var resolved = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            var parentIds = ResolveParentIds(definition);
            if (parentIds.Count == 0 &&
                !string.Equals(definition.Id, "base", StringComparison.OrdinalIgnoreCase) &&
                _archetypesById.ContainsKey("base"))
            {
                parentIds.Add("base");
            }

            foreach (var parentId in parentIds)
            {
                var parentSettings = ResolveArchetypeSettings(parentId, resolutionStack);
                if (parentSettings == null)
                {
                    continue;
                }

                foreach (var kvp in parentSettings)
                {
                    resolved[kvp.Key] = kvp.Value;
                }
            }

            foreach (var modifier in definition.Modifiers)
            {
                resolved.TryGetValue(modifier.Key, out var current);
                var next = modifier.Operation switch
                {
                    BG3AIModifierOperation.Add => current + modifier.Value,
                    BG3AIModifierOperation.Subtract => current - modifier.Value,
                    _ => modifier.Value
                };
                resolved[modifier.Key] = next;
            }

            resolutionStack.Remove(archetypeId);
            _resolvedSettings[archetypeId] = resolved;
            return resolved;
        }

        private List<string> ResolveParentIds(BG3AIArchetypeDefinition definition)
        {
            var parentIds = new List<string>();

            foreach (var rawParent in definition.Parents)
            {
                var parentId = ResolveParentReference(definition.Id, rawParent);
                if (string.IsNullOrEmpty(parentId))
                {
                    _warnings.Add($"Archetype '{definition.Id}' references unknown parent '{rawParent}'");
                    continue;
                }

                parentIds.Add(parentId);
            }

            return parentIds;
        }

        private string ResolveParentReference(string currentId, string rawParent)
        {
            if (string.IsNullOrWhiteSpace(rawParent))
            {
                return string.Empty;
            }

            var normalizedParent = NormalizeId(rawParent);

            // 1) Exact ID match first.
            if (_archetypesById.ContainsKey(normalizedParent))
            {
                return normalizedParent;
            }

            // 2) Try sibling path if parent reference is local (no '/').
            if (!normalizedParent.Contains('/'))
            {
                var slashIndex = currentId.LastIndexOf('/');
                if (slashIndex >= 0)
                {
                    var sibling = $"{currentId.Substring(0, slashIndex + 1)}{normalizedParent}";
                    if (_archetypesById.ContainsKey(sibling))
                    {
                        return sibling;
                    }
                }
            }

            // 3) Fallback to unique alias.
            var alias = Path.GetFileName(normalizedParent);
            if (_aliasToId.TryGetValue(alias, out var aliasId))
            {
                return aliasId;
            }

            return string.Empty;
        }

        private static string NormalizeId(string rawId)
        {
            return rawId
                .Replace('\\', '/')
                .Trim()
                .Trim('/');
        }

        private void Clear()
        {
            _archetypesById.Clear();
            _aliasToId.Clear();
            _ambiguousAliases.Clear();
            _resolvedSettings.Clear();
            _surfaceCombos.Clear();
            _errors.Clear();
            _warnings.Clear();
        }
    }
}
