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
            GD.Print("=== IN-ENGINE AUTO-BATTLE RUN ===");
            GD.Print($"Timestamp: {DateTime.UtcNow:O}");
            GD.Print("NOTE: Running the REAL CombatArena.tscn scene with AI control.");
            GD.Print("");

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
                config.ScenarioPath = "res://Data/Scenarios/autobattle_4v4.json";
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

            if (HasArg("freeze-timeout") && float.TryParse(GetArg("freeze-timeout"), out float freezeTimeout))
            {
                config.WatchdogFreezeTimeoutSeconds = freezeTimeout;
            }

            if (HasArg("loop-threshold") && int.TryParse(GetArg("loop-threshold"), out int loopThreshold))
            {
                config.WatchdogLoopThreshold = loopThreshold;
            }

            config.LogToStdout = !HasArg("quiet");

            GD.Print($"Seed: {config.Seed}");
            GD.Print($"Scenario: {config.ScenarioPath ?? "arena default"}");
            GD.Print($"Log file: {config.LogFilePath}");
            GD.Print($"Max rounds: {config.MaxRounds}");
            GD.Print($"Max turns: {config.MaxTurns}");
            GD.Print($"Watchdog freeze timeout: {config.WatchdogFreezeTimeoutSeconds}s");
            GD.Print($"Watchdog loop threshold: {config.WatchdogLoopThreshold}");
            GD.Print("");

            // Create the AutoBattlerManager as a Node and add to scene tree
            var manager = new AutoBattlerManager();
            AddChild(manager);
            
            // Start the auto-battle - this will load the real CombatArena scene
            // and the battle will run via Godot's main loop
            // Results will be printed and Quit() called when done
            manager.StartAutoBattle(this, config);
            
            // Do NOT call ExitWithCode here - the battle runs asynchronously
            // The AutoBattlerManager will call GetTree().Quit() when done
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
