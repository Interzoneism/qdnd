#nullable enable
using System;
using System.Collections.Generic;
using QDND.Combat.Persistence;
using QDND.Combat.Rules;
using QDND.Tests.Simulation;
using QDND.Tools.Profiling;
using Xunit;
using Xunit.Abstractions;

namespace Tests.Performance;

public class PerformanceBenchmarks
{
    private readonly ITestOutputHelper _output;
    private readonly ProfilerHarness _harness = new(warmupIterations: 5);

    public PerformanceBenchmarks(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DiceRoller_RollD20_Benchmark()
    {
        var dice = new DiceRoller(12345);

        var metrics = _harness.Measure("DiceRoller.RollD20", () =>
        {
            for (int i = 0; i < 100; i++)
                dice.RollD20();
        }, iterations: 100);

        _output.WriteLine(metrics.ToString());

        // 100 rolls should be very fast
        Assert.True(metrics.P95Ms < 1.0, $"100 dice rolls took too long: {metrics.P95Ms}ms");
    }

    [Fact]
    public void Simulation_SingleTurn_Benchmark()
    {
        var metrics = _harness.MeasureWithSetup(
            "Simulation.ExecuteTurn",
            setup: () =>
            {
                var state = new SimulationState(42);
                state.AddCombatant(new ScenarioCombatant { Id = "a", Team = 1, MaxHP = 30, AC = 15, AttackBonus = 5 });
                state.AddCombatant(new ScenarioCombatant { Id = "b", Team = 2, MaxHP = 30, AC = 15, AttackBonus = 5 });
                return state;
            },
            operation: state => state.ExecuteTurn(),
            iterations: 500);

        _output.WriteLine(metrics.ToString());

        Assert.True(metrics.P95Ms < 1.0, $"Single turn took too long: {metrics.P95Ms}ms");
    }

    [Fact]
    public void Snapshot_Serialization_Benchmark()
    {
        var snapshot = CreateLargeSnapshot(20);

        var metrics = _harness.Measure("Snapshot.Serialize", () =>
        {
            System.Text.Json.JsonSerializer.Serialize(snapshot);
        }, iterations: 100);

        _output.WriteLine(metrics.ToString());

        // Serializing 20 combatants should be fast
        Assert.True(metrics.P95Ms < 5.0, $"Snapshot serialization too slow: {metrics.P95Ms}ms");
    }

    [Fact]
    public void Snapshot_Deserialization_Benchmark()
    {
        var snapshot = CreateLargeSnapshot(20);
        var json = System.Text.Json.JsonSerializer.Serialize(snapshot);

        var metrics = _harness.Measure("Snapshot.Deserialize", () =>
        {
            System.Text.Json.JsonSerializer.Deserialize<CombatSnapshot>(json);
        }, iterations: 100);

        _output.WriteLine(metrics.ToString());

        Assert.True(metrics.P95Ms < 5.0, $"Snapshot deserialization too slow: {metrics.P95Ms}ms");
    }

    [Fact]
    public void InvariantChecker_FullCheck_Benchmark()
    {
        var state = CreateLargeSimulationState(20);
        var checker = new InvariantChecker();

        var metrics = _harness.Measure("InvariantChecker.CheckAll", () =>
        {
            checker.CheckAll(state);
        }, iterations: 200);

        _output.WriteLine(metrics.ToString());

        Assert.True(metrics.P95Ms < 1.0, $"Invariant checking too slow: {metrics.P95Ms}ms");
    }

    [Fact]
    public void Simulation_FullCombat_Benchmark()
    {
        var runner = new SimulationRunner();
        var scenario = new SimulationScenario
        {
            Name = "LargeCombat",
            Combatants = new List<ScenarioCombatant>()
        };

        // Add 10 combatants
        for (int i = 0; i < 5; i++)
        {
            scenario.Combatants.Add(new ScenarioCombatant
            {
                Id = $"team1_{i}",
                Team = 1,
                MaxHP = 30,
                AC = 14,
                AttackBonus = 5,
                DamageBonus = 3
            });
            scenario.Combatants.Add(new ScenarioCombatant
            {
                Id = $"team2_{i}",
                Team = 2,
                MaxHP = 30,
                AC = 14,
                AttackBonus = 5,
                DamageBonus = 3
            });
        }

        var metrics = _harness.Measure("Simulation.FullCombat", () =>
        {
            runner.Run(scenario, 12345, maxTurns: 50);
        }, iterations: 20);

        _output.WriteLine(metrics.ToString());

        // 50-turn combat with 10 units should complete in reasonable time
        Assert.True(metrics.P95Ms < 50.0, $"Full combat simulation too slow: {metrics.P95Ms}ms");
    }

    [Fact]
    public void DeterministicExporter_Benchmark()
    {
        var snapshot = CreateLargeSnapshot(20);
        var exporter = new DeterministicExporter();

        var metrics = _harness.Measure("DeterministicExporter.Export", () =>
        {
            exporter.ExportSnapshot(snapshot);
        }, iterations: 100);

        _output.WriteLine(metrics.ToString());

        Assert.True(metrics.P95Ms < 10.0, $"Deterministic export too slow: {metrics.P95Ms}ms");
    }

    [Fact]
    public void FullBenchmarkSuite_AllOperations()
    {
        var suite = new BenchmarkSuite { SuiteName = "Combat Operations" };

        var dice = new DiceRoller(123);
        suite.AddBenchmark("100xRollD20", () =>
        {
            for (int i = 0; i < 100; i++) dice.RollD20();
        });

        var snapshot = CreateLargeSnapshot(10);
        suite.AddBenchmark("Snapshot.Serialize", () =>
        {
            System.Text.Json.JsonSerializer.Serialize(snapshot);
        });

        var results = suite.Run();
        results.PrintSummary(msg => _output.WriteLine(msg));

        Assert.Empty(results.Errors);
        Assert.True(results.Results.Count >= 2);
    }

    // Helpers

    private CombatSnapshot CreateLargeSnapshot(int combatantCount)
    {
        var snapshot = new CombatSnapshot
        {
            Version = 1,
            CombatState = "PlayerTurn",
            CurrentRound = 3,
            CurrentTurnIndex = 5,
            InitialSeed = 12345,
            RollIndex = 150,
            TurnOrder = new List<string>(),
            Combatants = new List<CombatantSnapshot>(),
            Surfaces = new List<SurfaceSnapshot>(),
            ActiveStatuses = new List<StatusSnapshot>(),
            ActionCooldowns = new List<CooldownSnapshot>()
        };

        for (int i = 0; i < combatantCount; i++)
        {
            var id = $"combatant_{i}";
            snapshot.TurnOrder.Add(id);
            snapshot.Combatants.Add(new CombatantSnapshot
            {
                Id = id,
                DefinitionId = "warrior",
                Name = $"Warrior {i}",
                Faction = i < combatantCount / 2 ? "ally" : "enemy",
                Team = i < combatantCount / 2 ? 1 : 2,
                PositionX = i * 5f,
                PositionY = 0,
                PositionZ = i % 5 * 3f,
                CurrentHP = 30 + i,
                MaxHP = 50,
                IsAlive = true,
                HasAction = true,
                HasBonusAction = true,
                HasReaction = true,
                RemainingMovement = 30,
                MaxMovement = 30
            });

            // Add some statuses
            if (i % 3 == 0)
            {
                snapshot.ActiveStatuses.Add(new StatusSnapshot
                {
                    Id = $"status_{i}",
                    StatusDefinitionId = "burning",
                    TargetCombatantId = id,
                    SourceCombatantId = snapshot.Combatants[0].Id,
                    StackCount = 1,
                    RemainingDuration = 3
                });
            }
        }

        // Add some surfaces
        for (int i = 0; i < 3; i++)
        {
            snapshot.Surfaces.Add(new SurfaceSnapshot
            {
                Id = $"surface_{i}",
                SurfaceType = "fire",
                PositionX = i * 10f,
                PositionY = 0,
                PositionZ = 5f,
                Radius = 3f,
                RemainingDuration = 5
            });
        }

        return snapshot;
    }

    private SimulationState CreateLargeSimulationState(int combatantCount)
    {
        var state = new SimulationState(12345);

        for (int i = 0; i < combatantCount; i++)
        {
            state.AddCombatant(new ScenarioCombatant
            {
                Id = $"combatant_{i}",
                Team = i % 2 + 1,
                MaxHP = 30,
                AC = 14,
                AttackBonus = 5,
                DamageBonus = 3
            });
        }

        return state;
    }
}
