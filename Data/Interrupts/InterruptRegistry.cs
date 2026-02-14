using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Data.Parsers;

namespace QDND.Data.Interrupts
{
    /// <summary>
    /// Centralized registry for all BG3 interrupt/reaction definitions.
    /// Provides indexed look-up by ID and by <see cref="BG3InterruptContext"/>,
    /// mirroring the pattern established by <c>PassiveRegistry</c> and <c>StatusRegistry</c>.
    ///
    /// Typical usage:
    /// <code>
    /// var registry = new InterruptRegistry();
    /// registry.LoadInterrupts("BG3_Data/Stats/Interrupt.txt");
    ///
    /// var counterspell = registry.GetInterrupt("Interrupt_Counterspell");
    /// var onHitReactions = registry.GetInterruptsByContext(BG3InterruptContext.OnCastHit);
    /// </code>
    /// </summary>
    public class InterruptRegistry
    {
        private readonly Dictionary<string, BG3InterruptData> _interrupts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<BG3InterruptContext, List<string>> _contextIndex = new();
        private readonly Dictionary<BG3InterruptContextScope, List<string>> _scopeIndex = new();
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        /// <summary>Total number of registered (non-stub) interrupts.</summary>
        public int Count => _interrupts.Count;

        /// <summary>Errors encountered during registration or loading.</summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>Warnings encountered during registration or loading.</summary>
        public IReadOnlyList<string> Warnings => _warnings;

        // ---------------------------------------------------------------
        //  Registration
        // ---------------------------------------------------------------

        /// <summary>
        /// Register a single interrupt definition.
        /// </summary>
        /// <param name="interrupt">The interrupt data to register.</param>
        /// <param name="overwrite">If true, replaces an existing entry with the same ID.</param>
        /// <returns>True if registration succeeded.</returns>
        public bool RegisterInterrupt(BG3InterruptData interrupt, bool overwrite = false)
        {
            if (interrupt == null)
            {
                _errors.Add("Cannot register null interrupt");
                return false;
            }

            if (string.IsNullOrEmpty(interrupt.InterruptId))
            {
                _errors.Add($"Cannot register interrupt with null/empty ID: {interrupt.DisplayName ?? "Unknown"}");
                return false;
            }

            if (_interrupts.ContainsKey(interrupt.InterruptId) && !overwrite)
            {
                _warnings.Add($"Interrupt '{interrupt.InterruptId}' already registered (use overwrite=true to replace)");
                return false;
            }

            // Unindex old entry if replacing
            if (_interrupts.ContainsKey(interrupt.InterruptId))
            {
                UnindexInterrupt(_interrupts[interrupt.InterruptId]);
            }

            _interrupts[interrupt.InterruptId] = interrupt;
            IndexInterrupt(interrupt);
            return true;
        }

        // ---------------------------------------------------------------
        //  Queries
        // ---------------------------------------------------------------

        /// <summary>
        /// Retrieve an interrupt by its unique ID (case-insensitive).
        /// </summary>
        /// <param name="interruptId">The interrupt entry name.</param>
        /// <returns>The interrupt data, or null if not found.</returns>
        public BG3InterruptData GetInterrupt(string interruptId)
        {
            if (string.IsNullOrEmpty(interruptId))
                return null;
            _interrupts.TryGetValue(interruptId, out var data);
            return data;
        }

        /// <summary>
        /// Check whether an interrupt ID is registered.
        /// </summary>
        public bool HasInterrupt(string interruptId)
        {
            return !string.IsNullOrEmpty(interruptId) && _interrupts.ContainsKey(interruptId);
        }

        /// <summary>
        /// Get all interrupts that fire on the given <see cref="BG3InterruptContext"/>.
        /// </summary>
        /// <param name="context">The trigger context to filter by.</param>
        /// <returns>List of matching interrupts (may be empty, never null).</returns>
        public List<BG3InterruptData> GetInterruptsByContext(BG3InterruptContext context)
        {
            if (!_contextIndex.TryGetValue(context, out var ids))
                return new List<BG3InterruptData>();

            return ids
                .Where(id => _interrupts.ContainsKey(id))
                .Select(id => _interrupts[id])
                .ToList();
        }

        /// <summary>
        /// Get all interrupts with the given <see cref="BG3InterruptContextScope"/>.
        /// </summary>
        /// <param name="scope">The scope to filter by.</param>
        /// <returns>List of matching interrupts.</returns>
        public List<BG3InterruptData> GetInterruptsByScope(BG3InterruptContextScope scope)
        {
            if (!_scopeIndex.TryGetValue(scope, out var ids))
                return new List<BG3InterruptData>();

            return ids
                .Where(id => _interrupts.ContainsKey(id))
                .Select(id => _interrupts[id])
                .ToList();
        }

        /// <summary>
        /// Get all interrupts that cost a reaction action point.
        /// </summary>
        public List<BG3InterruptData> GetReactionCostInterrupts()
        {
            return _interrupts.Values
                .Where(i => i.CostsReaction)
                .ToList();
        }

        /// <summary>
        /// Search interrupts by name or ID (case-insensitive substring match).
        /// </summary>
        /// <param name="query">Search term.</param>
        /// <returns>List of matching interrupts.</returns>
        public List<BG3InterruptData> SearchInterrupts(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<BG3InterruptData>();

            query = query.ToLowerInvariant();
            return _interrupts.Values
                .Where(i =>
                    i.InterruptId.ToLowerInvariant().Contains(query) ||
                    (i.DisplayName?.ToLowerInvariant().Contains(query) ?? false) ||
                    (i.Description?.ToLowerInvariant().Contains(query) ?? false))
                .ToList();
        }

        /// <summary>
        /// Get all registered interrupts.
        /// </summary>
        public List<BG3InterruptData> GetAllInterrupts()
        {
            return _interrupts.Values.ToList();
        }

        // ---------------------------------------------------------------
        //  Loading
        // ---------------------------------------------------------------

        /// <summary>
        /// Parse and register all interrupts from a BG3 Interrupt.txt file.
        /// Resolves inheritance automatically.
        /// </summary>
        /// <param name="filePath">Path to the Interrupt.txt file.</param>
        /// <returns>Number of interrupts successfully registered.</returns>
        public int LoadInterrupts(string filePath)
        {
            var parser = new BG3InterruptParser();
            var parsed = parser.ParseFile(filePath);

            // Resolve inheritance
            parser.ResolveInheritance();

            int registered = 0;
            foreach (var interrupt in parsed)
            {
                // Skip context-only stubs (e.g., "Interrupt_ON_SPELL_CAST")
                if (interrupt.IsContextStub)
                    continue;

                if (RegisterInterrupt(interrupt, overwrite: true))
                    registered++;
            }

            _errors.AddRange(parser.Errors);
            _warnings.AddRange(parser.Warnings);

            Godot.GD.Print($"[InterruptRegistry] Loaded {registered} interrupts from {filePath} ({_interrupts.Count} total, {parser.Warnings.Count} warnings)");

            return registered;
        }

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

        /// <summary>
        /// Clear all registered interrupts and reset indices.
        /// </summary>
        public void Clear()
        {
            _interrupts.Clear();
            _contextIndex.Clear();
            _scopeIndex.Clear();
            _errors.Clear();
            _warnings.Clear();
        }

        /// <summary>
        /// Get summary statistics about the registry contents.
        /// </summary>
        public string GetStats()
        {
            var ctxCounts = new Dictionary<string, int>();
            foreach (var (ctx, ids) in _contextIndex)
            {
                ctxCounts[ctx.ToString()] = ids.Count;
            }

            int withReactionCost = _interrupts.Values.Count(i => i.CostsReaction);
            int withSpellSlotCost = _interrupts.Values.Count(i => i.CostsSpellSlot);
            int withRoll = _interrupts.Values.Count(i => i.HasRoll);

            var lines = new List<string>
            {
                $"InterruptRegistry Stats:",
                $"  Total: {Count}",
                $"  With Reaction Cost: {withReactionCost}",
                $"  With Spell Slot Cost: {withSpellSlotCost}",
                $"  With Roll Check: {withRoll}",
                $"  By Context:"
            };

            foreach (var (ctx, count) in ctxCounts.OrderByDescending(c => c.Value))
            {
                lines.Add($"    {ctx}: {count}");
            }

            lines.Add($"  Errors: {_errors.Count}");
            lines.Add($"  Warnings: {_warnings.Count}");

            return string.Join("\n", lines);
        }

        // ---------------------------------------------------------------
        //  Indexing helpers
        // ---------------------------------------------------------------

        private void IndexInterrupt(BG3InterruptData interrupt)
        {
            if (interrupt.InterruptContext != BG3InterruptContext.Unknown)
            {
                if (!_contextIndex.TryGetValue(interrupt.InterruptContext, out var ctxList))
                {
                    ctxList = new List<string>();
                    _contextIndex[interrupt.InterruptContext] = ctxList;
                }
                ctxList.Add(interrupt.InterruptId);
            }

            if (interrupt.InterruptContextScope != BG3InterruptContextScope.Unknown)
            {
                if (!_scopeIndex.TryGetValue(interrupt.InterruptContextScope, out var scopeList))
                {
                    scopeList = new List<string>();
                    _scopeIndex[interrupt.InterruptContextScope] = scopeList;
                }
                scopeList.Add(interrupt.InterruptId);
            }
        }

        private void UnindexInterrupt(BG3InterruptData interrupt)
        {
            if (interrupt.InterruptContext != BG3InterruptContext.Unknown &&
                _contextIndex.TryGetValue(interrupt.InterruptContext, out var ctxList))
            {
                ctxList.Remove(interrupt.InterruptId);
                if (ctxList.Count == 0)
                    _contextIndex.Remove(interrupt.InterruptContext);
            }

            if (interrupt.InterruptContextScope != BG3InterruptContextScope.Unknown &&
                _scopeIndex.TryGetValue(interrupt.InterruptContextScope, out var scopeList))
            {
                scopeList.Remove(interrupt.InterruptId);
                if (scopeList.Count == 0)
                    _scopeIndex.Remove(interrupt.InterruptContextScope);
            }
        }
    }
}
