using System;
using System.Collections.Generic;
using System.Linq;
using static QDND.Combat.Services.ScenarioBootService;

namespace QDND.Tools.AutoBattler
{
    /// <summary>
    /// Parses command-line arguments for auto-battle configuration.
    /// Pure .NET — no Godot dependencies. CombatArena applies the result.
    /// </summary>
    public static class AutoBattleCliParser
    {
        /// <summary>
        /// All values extracted from a successful --run-autobattle CLI parse.
        /// </summary>
        public class Result
        {
            /// <summary>Whether --random-scenario was specified.</summary>
            public bool UseRandomScenario { get; set; }

            /// <summary>Character level from --character-level (default 3).</summary>
            public int CharacterLevel { get; set; } = 3;

            /// <summary>Seed from --scenario-seed; null if not specified.</summary>
            public int? ScenarioSeedOverride { get; set; }

            /// <summary>Resolved scenario seed (accounts for dynamic-mode default seeding).</summary>
            public int ResolvedScenarioSeed { get; set; }

            /// <summary>Dynamic scenario mode parsed from --ff-* flags.</summary>
            public DynamicScenarioMode ScenarioMode { get; set; } = DynamicScenarioMode.None;

            /// <summary>Action ID from --ff-ability-test or --ff-action-test.</summary>
            public string ActionTestId { get; set; }

            /// <summary>Team size from --team-size (default 3).</summary>
            public int TeamSize { get; set; } = 3;

            /// <summary>Action IDs from --ff-action-batch.</summary>
            public List<string> ActionBatchIds { get; set; }

            /// <summary>Whether --full-fidelity was specified.</summary>
            public bool IsFullFidelity { get; set; }

            /// <summary>Whether --parity-report was specified.</summary>
            public bool IsParityReport { get; set; }

            /// <summary>
            /// Resolved arena-level ScenarioPath. Empty string when dynamic mode
            /// is active; the scenario arg value or defaultScenarioPath otherwise.
            /// </summary>
            public string ArenaScenarioPath { get; set; }

            /// <summary>Populated AutoBattleConfig ready to hand to the runtime.</summary>
            public AutoBattleConfig Config { get; set; }

            /// <summary>AI seed override from --seed; null if not specified.</summary>
            public int? AiSeedOverride { get; set; }

            /// <summary>
            /// Final value for the arena's RandomSeed field.
            /// Null means no CLI argument explicitly set it; leave the export value alone.
            /// </summary>
            public int? FinalRandomSeed { get; set; }

            /// <summary>Whether --verbose-ai-logs was specified.</summary>
            public bool VerboseAiLogs { get; set; }

            /// <summary>Whether --verbose-arena-logs was specified.</summary>
            public bool VerboseArenaLogs { get; set; }
        }

        /// <summary>
        /// Parses <paramref name="userArgs"/> and returns a <see cref="Result"/> when
        /// --run-autobattle is present, or <c>null</c> when it is absent.
        /// </summary>
        /// <param name="userArgs">Raw user args array from <c>OS.GetCmdlineUserArgs()</c>.</param>
        /// <param name="defaultScenarioPath">Current arena ScenarioPath (used when no --scenario arg).</param>
        public static Result TryParse(string[] userArgs, string defaultScenarioPath)
        {
            if (userArgs == null || userArgs.Length == 0)
                return null;

            var args = ParseUserArgs(userArgs);
            if (!args.ContainsKey("run-autobattle"))
                return null;

            var result = new Result
            {
                Config = new AutoBattleConfig()
            };

            if (args.ContainsKey("random-scenario"))
                result.UseRandomScenario = true;

            if (args.TryGetValue("character-level", out string levelValue) &&
                int.TryParse(levelValue, out int parsedLevel))
            {
                result.CharacterLevel = Math.Clamp(parsedLevel, 1, 12);
            }

            if (args.TryGetValue("scenario-seed", out string scenarioSeedValue) &&
                int.TryParse(scenarioSeedValue, out int scenarioSeed))
            {
                result.ScenarioSeedOverride = scenarioSeed;
                result.ResolvedScenarioSeed = scenarioSeed;
                result.FinalRandomSeed = scenarioSeed;
            }

            // Accept both --ff-ability-test (canonical, matches shell script) and --ff-action-test (legacy)
            if (!args.TryGetValue("ff-ability-test", out string actionToTest) ||
                string.IsNullOrWhiteSpace(actionToTest) || actionToTest == "true")
            {
                args.TryGetValue("ff-action-test", out actionToTest);
            }

            if (!string.IsNullOrWhiteSpace(actionToTest) && actionToTest != "true")
            {
                result.ScenarioMode = DynamicScenarioMode.ActionTest;
                result.ActionTestId = actionToTest.Trim();
            }
            else if (args.ContainsKey("ff-team-battle"))
            {
                result.ScenarioMode = DynamicScenarioMode.TeamBattle;
                if (args.TryGetValue("team-size", out string teamSizeValue) &&
                    int.TryParse(teamSizeValue, out int teamSize))
                {
                    result.TeamSize = Math.Clamp(teamSize, 1, 6);
                }
            }
            else if (args.TryGetValue("ff-action-batch", out string batchIds) &&
                     !string.IsNullOrWhiteSpace(batchIds) && batchIds != "true")
            {
                result.ScenarioMode = DynamicScenarioMode.ActionBatch;
                result.ActionBatchIds = batchIds
                    .Split(',')
                    .Select(id => id.Trim())
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToList();
                if (result.ActionBatchIds.Count == 0)
                    throw new InvalidOperationException(
                        "--ff-action-batch requires at least one comma-separated action ID.");
            }
            else if (args.ContainsKey("ff-short-gameplay"))
            {
                result.ScenarioMode = DynamicScenarioMode.ShortGameplay;
            }

            result.IsFullFidelity = args.ContainsKey("full-fidelity");
            if (result.IsFullFidelity)
            {
                result.Config.IsFullFidelity = true;
                // Full-fidelity mode needs startup grace for HUD/animation bootstrap.
                result.Config.WatchdogInitialActionGraceSeconds = 8.0f;
            }

            result.IsParityReport = args.ContainsKey("parity-report");

            if (result.ScenarioMode != DynamicScenarioMode.None)
            {
                result.UseRandomScenario = false;
                result.ArenaScenarioPath = string.Empty;
                result.Config.ScenarioPath = null;

                if (!result.ScenarioSeedOverride.HasValue)
                {
                    int resolved = result.ScenarioMode == DynamicScenarioMode.ActionTest
                        ? 1
                        : GenerateRuntimeSeed();
                    result.ResolvedScenarioSeed = resolved;
                    result.FinalRandomSeed = resolved;
                }
            }
            else if (args.TryGetValue("scenario", out string scenarioPath) &&
                     !string.IsNullOrEmpty(scenarioPath) && scenarioPath != "true")
            {
                result.ArenaScenarioPath = scenarioPath;
                result.Config.ScenarioPath = scenarioPath;
            }
            else
            {
                result.ArenaScenarioPath = defaultScenarioPath;
                result.Config.ScenarioPath = defaultScenarioPath;
            }

            if (args.TryGetValue("seed", out string seedValue) && int.TryParse(seedValue, out int seed))
            {
                result.AiSeedOverride = seed;
                result.Config.Seed = seed;
                result.FinalRandomSeed = seed;
            }

            if (args.TryGetValue("log-file", out string logFilePath) &&
                !string.IsNullOrEmpty(logFilePath) && logFilePath != "true")
            {
                result.Config.LogFilePath = logFilePath;
            }

            if (args.TryGetValue("max-rounds", out string maxRoundsValue) &&
                int.TryParse(maxRoundsValue, out int maxRounds))
            {
                result.Config.MaxRounds = maxRounds;
            }

            if (args.TryGetValue("max-turns", out string maxTurnsValue) &&
                int.TryParse(maxTurnsValue, out int maxTurns))
            {
                result.Config.MaxTurns = maxTurns;
            }

            if (args.TryGetValue("max-time-seconds", out string maxTimeValue) &&
                float.TryParse(maxTimeValue, out float maxTimeSeconds))
            {
                result.Config.MaxRuntimeSeconds = Math.Max(0.0f, maxTimeSeconds);
            }
            else if (args.TryGetValue("max-time", out string maxTimeAliasValue) &&
                     float.TryParse(maxTimeAliasValue, out float maxTimeAliasSeconds))
            {
                result.Config.MaxRuntimeSeconds = Math.Max(0.0f, maxTimeAliasSeconds);
            }

            if (args.TryGetValue("freeze-timeout", out string freezeTimeoutValue) &&
                float.TryParse(freezeTimeoutValue, out float freezeTimeout))
            {
                result.Config.WatchdogFreezeTimeoutSeconds = freezeTimeout;
            }

            if (args.TryGetValue("watchdog-startup-grace", out string startupGraceValue) &&
                float.TryParse(startupGraceValue, out float startupGrace))
            {
                result.Config.WatchdogInitialActionGraceSeconds = Math.Max(0.0f, startupGrace);
            }

            if (args.TryGetValue("loop-threshold", out string loopThresholdValue) &&
                int.TryParse(loopThresholdValue, out int loopThreshold))
            {
                result.Config.WatchdogLoopThreshold = loopThreshold;
            }

            result.VerboseAiLogs = args.ContainsKey("verbose-ai-logs");
            result.VerboseArenaLogs = args.ContainsKey("verbose-arena-logs");
            result.Config.VerboseDetailLogging = args.ContainsKey("verbose-detail-logs");
            result.Config.LogToStdout = !args.ContainsKey("quiet");

            return result;
        }

        /// <summary>
        /// Parses a raw user-args array into a key→value dictionary.
        /// Flags without a following value are stored as "true".
        /// </summary>
        public static Dictionary<string, string> ParseUserArgs(string[] userArgs)
        {
            var args = new Dictionary<string, string>();
            for (int i = 0; i < userArgs.Length; i++)
            {
                string arg = userArgs[i];
                if (!arg.StartsWith("--"))
                    continue;

                string key = arg.Substring(2);
                string value = "true";
                if (i + 1 < userArgs.Length && !userArgs[i + 1].StartsWith("--"))
                {
                    value = userArgs[i + 1];
                    i++;
                }
                args[key] = value;
            }
            return args;
        }

        /// <summary>
        /// Generates a non-deterministic runtime seed from TickCount + GUID.
        /// </summary>
        public static int GenerateRuntimeSeed()
        {
            return unchecked(System.Environment.TickCount ^ Guid.NewGuid().GetHashCode());
        }
    }
}
