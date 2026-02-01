#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using QDND.Combat.Persistence;
using QDND.Combat.Rules;
using QDND.Tests.Simulation;
using QDND.Tools.Profiling;

namespace Tests.Performance;

/// <summary>
/// Benchmark runner for CI integration.
/// </summary>
public class CIBenchmarkRunner
{
    private readonly ProfilerHarness _harness;
    private readonly string _outputDir;
    
    public CIBenchmarkRunner(string outputDir)
    {
        _harness = new ProfilerHarness(warmupIterations: 5);
        _outputDir = outputDir;
        Directory.CreateDirectory(_outputDir);
    }
    
    /// <summary>
    /// Run all benchmarks and save results.
    /// </summary>
    public BenchmarkResults RunAll()
    {
        var suite = new BenchmarkSuite { SuiteName = "QDND Combat Benchmarks" };
        
        // Dice rolling
        var dice = new DiceRoller(12345);
        suite.AddBenchmark("DiceRoller.100xRollD20", () =>
        {
            for (int i = 0; i < 100; i++) dice.RollD20();
        }, iterations: 100);
        
        // Snapshot serialization
        var snapshot = CreateTestSnapshot(20);
        suite.AddBenchmark("Snapshot.Serialize", () =>
        {
            System.Text.Json.JsonSerializer.Serialize(snapshot);
        }, iterations: 100);
        
        // Snapshot deserialization
        var json = System.Text.Json.JsonSerializer.Serialize(snapshot);
        suite.AddBenchmark("Snapshot.Deserialize", () =>
        {
            System.Text.Json.JsonSerializer.Deserialize<CombatSnapshot>(json);
        }, iterations: 100);
        
        // Simulation turn
        suite.AddBenchmark("Simulation.Turn", () =>
        {
            var state = new SimulationState(42);
            state.AddCombatant(new ScenarioCombatant { Id = "a", Team = 1, MaxHP = 30 });
            state.AddCombatant(new ScenarioCombatant { Id = "b", Team = 2, MaxHP = 30 });
            state.ExecuteTurn();
        }, iterations: 200);
        
        // Invariant checking
        var testState = CreateTestSimulationState(20);
        var checker = new InvariantChecker();
        suite.AddBenchmark("InvariantChecker.CheckAll", () =>
        {
            checker.CheckAll(testState);
        }, iterations: 200);
        
        // Full combat simulation
        var runner = new SimulationRunner();
        var scenario = CreateTestScenario(10);
        suite.AddBenchmark("Simulation.FullCombat50Turns", () =>
        {
            runner.Run(scenario, 12345, maxTurns: 50);
        }, iterations: 20);
        
        // Deterministic export
        var exporter = new DeterministicExporter();
        suite.AddBenchmark("DeterministicExporter.Export", () =>
        {
            exporter.ExportSnapshot(snapshot);
        }, iterations: 100);
        
        return suite.Run();
    }
    
    /// <summary>
    /// Run benchmarks and check for regressions against baseline.
    /// </summary>
    public (BenchmarkResults Results, List<BenchmarkRegression> Regressions) RunWithRegressionCheck()
    {
        var results = RunAll();
        
        var baselinePath = Path.Combine(_outputDir, "baseline.json");
        var regressions = BenchmarkReporter.CompareToBaseline(results, baselinePath);
        
        return (results, regressions);
    }
    
    /// <summary>
    /// Save the current results and optionally update baseline.
    /// </summary>
    public void SaveResults(BenchmarkResults results, bool updateBaseline = false)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var resultsPath = Path.Combine(_outputDir, $"benchmark_{timestamp}.json");
        
        BenchmarkReporter.SaveResults(results, resultsPath);
        
        if (updateBaseline)
        {
            var baselinePath = Path.Combine(_outputDir, "baseline.json");
            BenchmarkReporter.UpdateBaseline(results, baselinePath);
        }
    }
    
    // Helpers
    
    private CombatSnapshot CreateTestSnapshot(int combatantCount)
    {
        var snapshot = new CombatSnapshot
        {
            Version = 1,
            CombatState = "PlayerTurn",
            CurrentRound = 1,
            TurnOrder = new List<string>(),
            Combatants = new List<CombatantSnapshot>()
        };
        
        for (int i = 0; i < combatantCount; i++)
        {
            snapshot.TurnOrder.Add($"c{i}");
            snapshot.Combatants.Add(new CombatantSnapshot
            {
                Id = $"c{i}",
                MaxHP = 30,
                CurrentHP = 30,
                IsAlive = true
            });
        }
        
        return snapshot;
    }
    
    private SimulationState CreateTestSimulationState(int combatantCount)
    {
        var state = new SimulationState(12345);
        for (int i = 0; i < combatantCount; i++)
        {
            state.AddCombatant(new ScenarioCombatant
            {
                Id = $"c{i}",
                Team = i % 2 + 1,
                MaxHP = 30
            });
        }
        return state;
    }
    
    private SimulationScenario CreateTestScenario(int combatantCount)
    {
        var scenario = new SimulationScenario
        {
            Name = "BenchmarkScenario",
            Combatants = new List<ScenarioCombatant>()
        };
        
        for (int i = 0; i < combatantCount; i++)
        {
            scenario.Combatants.Add(new ScenarioCombatant
            {
                Id = $"c{i}",
                Team = i % 2 + 1,
                MaxHP = 30,
                AC = 14,
                AttackBonus = 5
            });
        }
        
        return scenario;
    }
}
