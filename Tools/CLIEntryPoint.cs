using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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
            GD.Print($"[CLIEntryPoint] Checking modes: run-tests={_args.ContainsKey("run-tests")}, run-simulation={_args.ContainsKey("run-simulation")}");
            if (_args.ContainsKey("run-tests"))
            {
                RunHeadlessTests();
            }
            else if (_args.ContainsKey("run-simulation"))
            {
                RunSimulationTests();
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
            runner.AddSmokeTests();

            // RunAllTests needs this node as parent to add CombatArena to scene tree
            var (allPassed, results) = runner.RunAllTests(this);

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
