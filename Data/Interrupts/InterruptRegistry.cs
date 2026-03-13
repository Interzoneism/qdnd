using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QDND.Data;
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
    /// registry.LoadInterrupts("BG3_Data/Shared/Public/Shared/Stats/Generated/Data/Interrupt.txt");
    ///
    /// var counterspell = registry.GetInterrupt("Interrupt_Counterspell");
    /// var onHitReactions = registry.GetInterruptsByContext(BG3InterruptContext.OnCastHit);
    /// </code>
    /// </summary>
    public class InterruptRegistry : Registry<BG3InterruptData>
    {
        private readonly Dictionary<BG3InterruptContext, List<string>> _contextIndex = new();
        private readonly Dictionary<BG3InterruptContextScope, List<string>> _scopeIndex = new();

        protected override string GetId(BG3InterruptData item) => item.InterruptId;

        protected override void AfterRegister(BG3InterruptData interrupt)
        {
            IndexInterrupt(interrupt);
        }

        protected override void AfterUnregister(BG3InterruptData interrupt)
        {
            UnindexInterrupt(interrupt);
        }

        protected override void OnClear()
        {
            _contextIndex.Clear();
            _scopeIndex.Clear();
        }

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
                AddError("Cannot register null interrupt");
                return false;
            }

            if (string.IsNullOrEmpty(interrupt.InterruptId))
            {
                AddError($"Cannot register interrupt with null/empty ID: {interrupt.DisplayName ?? "Unknown"}");
                return false;
            }

            return BaseRegister(interrupt, overwrite, "interrupt");
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
            return Get(interruptId);
        }

        /// <summary>
        /// Check whether an interrupt ID is registered.
        /// </summary>
        public bool HasInterrupt(string interruptId)
        {
            return !string.IsNullOrEmpty(interruptId) && Has(interruptId);
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
                .Where(id => Items.ContainsKey(id))
                .Select(id => Items[id])
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
                .Where(id => Items.ContainsKey(id))
                .Select(id => Items[id])
                .ToList();
        }

        /// <summary>
        /// Get all interrupts that cost a reaction action point.
        /// </summary>
        public List<BG3InterruptData> GetReactionCostInterrupts()
        {
            return Items.Values
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
            return Items.Values
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
            return GetAll().ToList();
        }

        // ---------------------------------------------------------------
        //  Loading
        // ---------------------------------------------------------------

        /// <summary>
        /// Parse and register interrupts from one or more BG3 Interrupt.txt files.
        /// Resolves inheritance automatically.
        /// </summary>
        /// <param name="filePaths">Paths to Interrupt.txt files (Shared first, then SharedDev).</param>
        /// <returns>Number of interrupts successfully registered.</returns>
        public int LoadInterrupts(params string[] filePaths)
        {
            if (filePaths == null || filePaths.Length == 0)
            {
                AddError("No interrupt file paths provided");
                return 0;
            }

            var parser = new BG3InterruptParser();
            var allParsed = new List<BG3InterruptData>();

            foreach (var filePath in filePaths)
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    continue;

                if (!File.Exists(filePath))
                {
                    AddWarning($"Interrupt file not found: {filePath}");
                    continue;
                }

                var parsed = parser.ParseFile(filePath);
                allParsed.AddRange(parsed);
            }

            // Resolve inheritance
            parser.ResolveInheritance();

            int registered = 0;
            foreach (var interrupt in allParsed)
            {
                // Skip context-only stubs (e.g., "Interrupt_ON_SPELL_CAST")
                if (interrupt.IsContextStub)
                    continue;

                if (RegisterInterrupt(interrupt, overwrite: false))
                    registered++;
            }

            foreach (var error in parser.Errors)
            {
                AddError(error);
            }

            foreach (var warning in parser.Warnings)
            {
                AddWarning(warning);
            }

            Console.WriteLine($"[InterruptRegistry] Loaded {registered} interrupts from {filePaths.Length} source file(s) ({Count} total, {parser.Warnings.Count} parser warning(s))");

            return registered;
        }

        // ---------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------

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

            int withReactionCost = Items.Values.Count(i => i.CostsReaction);
            int withSpellSlotCost = Items.Values.Count(i => i.CostsSpellSlot);
            int withRoll = Items.Values.Count(i => i.HasRoll);

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

            lines.Add($"  Errors: {Errors.Count}");
            lines.Add($"  Warnings: {Warnings.Count}");

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
