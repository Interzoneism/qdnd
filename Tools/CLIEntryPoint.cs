using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Tools.AutoBattler;
using QDND.Tools.Simulation;

namespace QDND.Tools
{
    /// <summary>
    /// CLI entry point for automated runs. Parses command-line arguments after "--"
    /// and routes to the appropriate runner (headless tests, screenshot capture, etc.).
    /// 
    /// Usage:
    ///   godot --headless --path . -- --run-tests
    ///   godot --path . -- --screenshot --scene res://Combat/Arena/CombatArena.tscn --out artifacts/screens
    /// </summary>
    public partial class CLIEntryPoint : Node
    {
        private Dictionary<string, string> _args = new();
        private bool _hasCliMode = false;

        public override void _Ready()
        {
            ParseCommandLineArgs();

            if (!_hasCliMode)
            {
                // No CLI mode specified, let normal scene run
                GD.Print("[CLIEntryPoint] No CLI mode specified, running normally");
                return;
            }

            // Route to appropriate handler
            GD.Print($"[CLIEntryPoint] Checking modes: run-tests={_args.ContainsKey("run-tests")}, run-simulation={_args.ContainsKey("run-simulation")}, run-autobattle={_args.ContainsKey("run-autobattle")}");
            if (_args.ContainsKey("run-tests"))
            {
                RunHeadlessTests();
            }
            else if (_args.ContainsKey("run-simulation"))
            {
                RunSimulationTests();
            }
            else if (_args.ContainsKey("run-autobattle"))
            {
                RunAutoBattle();
            }
            else
            {
                GD.PrintErr("[CLIEntryPoint] Unknown CLI mode");
                ExitWithCode(1);
            }
        }

        private void ParseCommandLineArgs()
        {
            var cmdArgs = OS.GetCmdlineArgs();
            var userArgs = OS.GetCmdlineUserArgs(); // Args after "--"

            GD.Print($"[CLIEntryPoint] Raw args: {string.Join(" ", cmdArgs)}");
            GD.Print($"[CLIEntryPoint] User args: {string.Join(" ", userArgs)}");

            // Parse user args (after "--")
            for (int i = 0; i < userArgs.Length; i++)
            {
                string arg = userArgs[i];

                if (arg.StartsWith("--"))
                {
                    string key = arg.Substring(2);
                    string value = "true";

                    // Check if next arg is a value (not a flag)
                    if (i + 1 < userArgs.Length && !userArgs[i + 1].StartsWith("--"))
                    {
                        value = userArgs[i + 1];
                        i++;
                    }

                    _args[key] = value;
                    _hasCliMode = true;
                }
            }

            GD.Print($"[CLIEntryPoint] Parsed args: {string.Join(", ", _args.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }

        private void RunHeadlessTests()
        {
            GD.Print("=== HEADLESS TEST RUN ===");
            GD.Print($"Timestamp: {DateTime.UtcNow:O}");

            var runner = new HeadlessTestRunner();
            var result = runner.RunAllTests();

            GD.Print("");
            GD.Print($"=== TEST SUMMARY ===");
            GD.Print($"Total: {result.Total}");
            GD.Print($"Passed: {result.Passed}");
            GD.Print($"Failed: {result.Failed}");
            GD.Print($"Skipped: {result.Skipped}");
            GD.Print("");

            if (result.Failed > 0)
            {
                GD.Print("FAILED");
                foreach (var failure in result.Failures)
                {
                    GD.PrintErr($"  - {failure}");
                }
                ExitWithCode(1);
            }
            else
            {
                GD.Print("OK");
                ExitWithCode(0);
            }
        }

        private void RunSimulationTests()
        {
            GD.Print("=== SIMULATION TEST RUN ===");
            GD.Print($"Timestamp: {DateTime.UtcNow:O}");

            var runner = new SimulationTestRunner();
            
            // Check for manifest or test directory argument
            string manifestPath = GetArg("manifest");
            string testDir = GetArg("test-dir");
            bool useGoldenPath = HasArg("golden-path");
            bool noSmoke = HasArg("no-smoke");
            
            // Load tests based on arguments
            if (!string.IsNullOrEmpty(manifestPath))
            {
                GD.Print($"Loading manifest: {manifestPath}");
                runner.LoadFromManifest(manifestPath);
            }
            else if (!string.IsNullOrEmpty(testDir))
            {
                GD.Print($"Loading manifests from directory: {testDir}");
                runner.LoadFromDirectory(testDir);
            }
            else if (useGoldenPath)
            {
                GD.Print("Loading Golden Path test suite...");
                runner.AddGoldenPathTests();
            }
            else
            {
                // Default: load golden path manifest if it exists, otherwise use hardcoded tests
                string defaultManifest = "res://Data/SimulationTests/golden_path.manifest.json";
                try
                {
                    runner.LoadFromManifest(defaultManifest);
                    GD.Print($"Loaded default manifest: {defaultManifest}");
                }
                catch (Exception ex)
                {
                    GD.Print($"Default manifest not found ({ex.Message}), using built-in tests");
                    if (!noSmoke)
                    {
                        runner.AddSmokeTests();
                    }
                    runner.AddGoldenPathTests();
                }
            }
            
            // Add smoke tests unless disabled
            if (!noSmoke && !HasArg("manifest") && !HasArg("test-dir"))
            {
                // runner.AddSmokeTests();  // Already added via manifest or golden path
            }
            
            GD.Print($"Running {runner} tests...");
            GD.Print("");

            // RunAllTests needs this node as parent to add CombatArena to scene tree
            var (allPassed, results) = runner.RunAllTests(this);

            // Print both human-readable and JSON output
            runner.PrintHumanReadableSummary();
            GD.Print("");
            GD.Print("=== JSON OUTPUT ===");
            runner.PrintResults();

            GD.Print("");
            if (allPassed)
            {
                GD.Print("SIMULATION TESTS: OK");
                ExitWithCode(0);
            }
            else
            {
                GD.Print("SIMULATION TESTS: FAILED");
                ExitWithCode(1);
            }
        }

        private void ExitWithCode(int code)
        {
            GD.Print($"[CLIEntryPoint] Exiting with code {code}");
            GetTree().Quit(code);
        }

        private void RunAutoBattle()
        {
            GD.Print("=== AUTO-BATTLE RUN ===");
            GD.Print($"Timestamp: {DateTime.UtcNow:O}");

            var config = new AutoBattleConfig();

            // Parse arguments
            if (HasArg("seed") && int.TryParse(GetArg("seed"), out int seed))
            {
                config.Seed = seed;
            }

            string scenario = GetArg("scenario");
            if (!string.IsNullOrEmpty(scenario) && scenario != "true")
            {
                config.ScenarioPath = scenario;
            }
            else
            {
                // Default scenario
                config.ScenarioPath = ProjectSettings.GlobalizePath("res://Data/Scenarios/autobattle_4v4.json");
            }

            string logFile = GetArg("log-file");
            if (!string.IsNullOrEmpty(logFile) && logFile != "true")
            {
                config.LogFilePath = logFile;
            }
            else
            {
                config.LogFilePath = "combat_log.jsonl";
            }

            if (HasArg("max-rounds") && int.TryParse(GetArg("max-rounds"), out int maxRounds))
            {
                config.MaxRounds = maxRounds;
            }

            if (HasArg("max-turns") && int.TryParse(GetArg("max-turns"), out int maxTurns))
            {
                config.MaxTurns = maxTurns;
            }

            config.LogToStdout = !HasArg("quiet");

            GD.Print($"Seed: {config.Seed}");
            GD.Print($"Scenario: {config.ScenarioPath ?? "default 4v4"}");
            GD.Print($"Log file: {config.LogFilePath}");
            GD.Print($"Max rounds: {config.MaxRounds}");
            GD.Print($"Max turns: {config.MaxTurns}");
            GD.Print("");

            var manager = new AutoBattlerManager();
            var result = manager.Run(config);

            GD.Print("");
            GD.Print("╔═══════════════════════════════════════════════════╗");
            GD.Print("║             AUTO-BATTLE RESULTS                   ║");
            GD.Print("╚═══════════════════════════════════════════════════╝");
            GD.Print("");
            GD.Print($"  Winner:       {result.Winner ?? "N/A"}");
            GD.Print($"  Total Turns:  {result.TotalTurns}");
            GD.Print($"  Total Rounds: {result.TotalRounds}");
            GD.Print($"  Duration:     {result.DurationMs}ms");
            GD.Print($"  Completed:    {result.Completed}");
            GD.Print($"  End Reason:   {result.EndReason}");
            GD.Print($"  Log Entries:  {result.LogEntryCount}");
            GD.Print($"  Seed:         {result.Seed}");
            GD.Print("");

            if (result.SurvivingUnits.Count > 0)
            {
                GD.Print("  Surviving Units:");
                foreach (var unit in result.SurvivingUnits)
                {
                    GD.Print($"    - {unit}");
                }
            }

            GD.Print("");

            if (result.Completed)
            {
                GD.Print("AUTO-BATTLE: OK");
                ExitWithCode(0);
            }
            else
            {
                GD.Print("AUTO-BATTLE: FAILED");
                ExitWithCode(1);
            }
        }

        public string GetArg(string key, string defaultValue = null)
        {
            return _args.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public bool HasArg(string key)
        {
            return _args.ContainsKey(key);
        }
    }
}
