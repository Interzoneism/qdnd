using System;
using System.Collections.Generic;

namespace QDND.Tools
{
    /// <summary>
    /// Feature flags for debugging and testing.
    /// Can be toggled at runtime or set via configuration.
    /// </summary>
    public static class DebugFlags
    {
        private static readonly Dictionary<string, bool> _flags = new();
        private static readonly Dictionary<string, int> _intValues = new();
        private static bool _isHeadless;
        private static bool _headlessCached;

        /// <summary>
        /// Force all attack rolls to hit (ignore AC/DC).
        /// </summary>
        public static bool ForceHit
        {
            get => GetFlag(nameof(ForceHit));
            set => SetFlag(nameof(ForceHit), value);
        }

        /// <summary>
        /// Force all attack rolls to miss.
        /// </summary>
        public static bool ForceMiss
        {
            get => GetFlag(nameof(ForceMiss));
            set => SetFlag(nameof(ForceMiss), value);
        }

        /// <summary>
        /// Force all attack rolls to be critical hits.
        /// </summary>
        public static bool ForceCrit
        {
            get => GetFlag(nameof(ForceCrit));
            set => SetFlag(nameof(ForceCrit), value);
        }

        /// <summary>
        /// Force a specific dice roll result (1-20 for d20).
        /// Set to 0 to disable.
        /// </summary>
        public static int ForcedDiceRoll
        {
            get => GetInt(nameof(ForcedDiceRoll));
            set => SetInt(nameof(ForcedDiceRoll), value);
        }

        /// <summary>
        /// Skip all animation timelines (instant resolution).
        /// </summary>
        public static bool SkipAnimations
        {
            get => GetFlag(nameof(SkipAnimations));
            set => SetFlag(nameof(SkipAnimations), value);
        }

        /// <summary>
        /// Enable verbose combat logging with full breakdowns.
        /// </summary>
        public static bool VerboseLogging
        {
            get => GetFlag(nameof(VerboseLogging), defaultValue: true);
            set => SetFlag(nameof(VerboseLogging), value);
        }

        /// <summary>
        /// Show hit chance calculations in combat log.
        /// </summary>
        public static bool ShowHitChances
        {
            get => GetFlag(nameof(ShowHitChances));
            set => SetFlag(nameof(ShowHitChances), value);
        }

        /// <summary>
        /// Disable fog of war (all units visible).
        /// </summary>
        public static bool DisableFogOfWar
        {
            get => GetFlag(nameof(DisableFogOfWar));
            set => SetFlag(nameof(DisableFogOfWar), value);
        }

        /// <summary>
        /// Show AI decision scoring in log.
        /// </summary>
        public static bool ShowAIScoring
        {
            get => GetFlag(nameof(ShowAIScoring));
            set => SetFlag(nameof(ShowAIScoring), value);
        }

        /// <summary>
        /// Pause before each AI action for inspection.
        /// </summary>
        public static bool PauseOnAITurn
        {
            get => GetFlag(nameof(PauseOnAITurn));
            set => SetFlag(nameof(PauseOnAITurn), value);
        }

        /// <summary>
        /// Enable invariant checks that may be slow.
        /// </summary>
        public static bool EnableInvariantChecks
        {
            get => GetFlag(nameof(EnableInvariantChecks));
            set => SetFlag(nameof(EnableInvariantChecks), value);
        }

        /// <summary>
        /// Log every rule event dispatch.
        /// </summary>
        public static bool LogRuleEvents
        {
            get => GetFlag(nameof(LogRuleEvents));
            set => SetFlag(nameof(LogRuleEvents), value);
        }

        /// <summary>
        /// True when running in Godot's headless mode (no GPU/display).
        /// Cached on first access.
        /// </summary>
        public static bool IsHeadless
        {
            get
            {
                if (!_headlessCached)
                {
                    _isHeadless = Godot.DisplayServer.GetName() == "headless";
                    _headlessCached = true;
                }
                return _isHeadless;
            }
        }

        /// <summary>
        /// True when running in auto-battle mode (set by CombatArena on startup).
        /// </summary>
        public static bool IsAutoBattle
        {
            get => GetFlag(nameof(IsAutoBattle));
            set => SetFlag(nameof(IsAutoBattle), value);
        }

        /// <summary>
        /// True when running in full-fidelity auto-battle mode.
        /// All game components (HUD, animations, visuals) run as in normal play.
        /// The AI interacts through UI-aware paths rather than bypassing the UI.
        /// </summary>
        public static bool IsFullFidelity
        {
            get => GetFlag(nameof(IsFullFidelity));
            set => SetFlag(nameof(IsFullFidelity), value);
        }

        /// <summary>
        /// True when parity metrics collection is enabled.
        /// Enables detailed tracking of abilities, effects, statuses, and surfaces.
        /// </summary>
        public static bool ParityReportMode
        {
            get => GetFlag(nameof(ParityReportMode));
            set => SetFlag(nameof(ParityReportMode), value);
        }

        /// <summary>
        /// True when visuals should be skipped (headless or autobattle with SkipAnimations).
        /// </summary>
        public static bool ShouldSkipVisuals => IsHeadless || SkipAnimations;

        // --- Core accessors ---

        public static bool GetFlag(string name, bool defaultValue = false)
        {
            return _flags.TryGetValue(name, out var value) ? value : defaultValue;
        }

        public static void SetFlag(string name, bool value)
        {
            _flags[name] = value;
            if (VerboseLogging && name != nameof(VerboseLogging))
            {
                Godot.GD.Print($"[DebugFlag] {name} = {value}");
            }
        }

        public static int GetInt(string name, int defaultValue = 0)
        {
            return _intValues.TryGetValue(name, out var value) ? value : defaultValue;
        }

        public static void SetInt(string name, int value)
        {
            _intValues[name] = value;
            if (VerboseLogging)
            {
                Godot.GD.Print($"[DebugFlag] {name} = {value}");
            }
        }

        /// <summary>
        /// Reset all flags to defaults.
        /// </summary>
        public static void ResetAll()
        {
            _flags.Clear();
            _intValues.Clear();
            _headlessCached = false;
            Godot.GD.Print("[DebugFlag] All flags reset to defaults");
        }

        /// <summary>
        /// Get all current flag states for logging/debugging.
        /// </summary>
        public static Dictionary<string, object> GetAllFlags()
        {
            var result = new Dictionary<string, object>();
            foreach (var (key, value) in _flags)
                result[key] = value;
            foreach (var (key, value) in _intValues)
                result[key] = value;
            return result;
        }

        /// <summary>
        /// Apply flags from a dictionary (for loading from config).
        /// </summary>
        public static void ApplyFromDictionary(Dictionary<string, object> config)
        {
            foreach (var (key, value) in config)
            {
                if (value is bool b)
                    SetFlag(key, b);
                else if (value is int i)
                    SetInt(key, i);
                else if (value is long l)
                    SetInt(key, (int)l);
            }
        }
    }
}
