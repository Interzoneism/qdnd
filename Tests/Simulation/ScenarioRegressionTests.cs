#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace QDND.Tests.Simulation;

public class ScenarioRegressionTests
{
    private readonly ITestOutputHelper _output;
    private readonly string _scenarioDir;
    private readonly ScenarioTestRunner _runner;

    public ScenarioRegressionTests(ITestOutputHelper output)
    {
        _output = output;

        // Find scenario directory - try multiple paths
        var possiblePaths = new[]
        {
            "Data/Scenarios",
            "../../../Data/Scenarios",
            "../../../../Data/Scenarios",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data/Scenarios"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../Data/Scenarios"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../Data/Scenarios")
        };

        _scenarioDir = possiblePaths.FirstOrDefault(Directory.Exists) ?? "Data/Scenarios";
        _runner = new ScenarioTestRunner(_scenarioDir);
    }

    public static IEnumerable<object[]> GetScenarioFiles()
    {
        var possiblePaths = new[]
        {
            "Data/Scenarios",
            "../../../Data/Scenarios",
            "../../../../Data/Scenarios"
        };

        var scenarioDir = possiblePaths.FirstOrDefault(Directory.Exists) ?? "Data/Scenarios";

        if (!Directory.Exists(scenarioDir))
            yield break;

        foreach (var file in Directory.GetFiles(scenarioDir, "test_*.json"))
        {
            yield return new object[] { file };
        }
    }

    [Theory]
    [MemberData(nameof(GetScenarioFiles))]
    public void Scenario_RunsWithoutViolations(string scenarioPath)
    {
        _output.WriteLine($"Running scenario: {scenarioPath}");

        var result = _runner.RunScenario(scenarioPath, seed: 12345, maxTurns: 50);

        _output.WriteLine($"Result: {result.TerminationReason}");
        _output.WriteLine($"Turns: {result.TurnCount}");

        foreach (var violation in result.Violations)
        {
            _output.WriteLine($"Violation: {violation.Message}");
        }

        Assert.Empty(result.Violations);
    }

    [Theory]
    [MemberData(nameof(GetScenarioFiles))]
    public void Scenario_DeterministicWithSameSeed(string scenarioPath)
    {
        var result1 = _runner.RunScenario(scenarioPath, seed: 54321, maxTurns: 30);
        var result2 = _runner.RunScenario(scenarioPath, seed: 54321, maxTurns: 30);

        Assert.Equal(result1.FinalStateHash, result2.FinalStateHash);
        Assert.Equal(result1.TurnCount, result2.TurnCount);
    }

    [Fact]
    public void AllScenarios_CompleteOrMaxTurns()
    {
        if (!Directory.Exists(_scenarioDir))
        {
            _output.WriteLine($"Scenario directory not found: {_scenarioDir}");
            return; // Skip if no scenarios
        }

        var results = _runner.RunAllScenarios(seed: 12345);

        foreach (var result in results)
        {
            _output.WriteLine($"{result.ScenarioName}: {(result.Passed ? "PASS" : "FAIL")}");
            if (!result.Passed && result.Error != null)
                _output.WriteLine($"  Error: {result.Error}");
        }

        var failures = results.Where(r => !r.Passed && r.Error != null).ToList();
        Assert.Empty(failures);
    }

    [Fact]
    public void SaveLoadScenario_PreservesState()
    {
        var scenarioPath = Path.Combine(_scenarioDir, "test_save_load.json");
        if (!File.Exists(scenarioPath))
        {
            _output.WriteLine($"Scenario not found: {scenarioPath}");
            return;
        }

        var result = _runner.RunScenario(scenarioPath, seed: 99999);

        Assert.Empty(result.Violations);
        _output.WriteLine($"Save/Load scenario completed in {result.TurnCount} turns");
    }

    [Fact]
    public void AIDecisionScenario_DeterministicResults()
    {
        var scenarioPath = Path.Combine(_scenarioDir, "test_ai_decisions.json");
        if (!File.Exists(scenarioPath))
        {
            _output.WriteLine($"Scenario not found: {scenarioPath}");
            return;
        }

        // Run same scenario 3 times with same seed
        var hashes = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var result = _runner.RunScenario(scenarioPath, seed: 77777, maxTurns: 30);
            hashes.Add(result.FinalStateHash);
            _output.WriteLine($"Run {i + 1}: Hash={result.FinalStateHash}, Turns={result.TurnCount}");
        }

        // All runs should produce same hash
        Assert.True(hashes.All(h => h == hashes[0]), "All runs should produce identical state hash");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(12345)]
    [InlineData(99999)]
    [InlineData(555)]
    public void MultiSeed_NoViolationsAcrossSeeds(int seed)
    {
        var scenarioPath = Path.Combine(_scenarioDir, "test_save_load.json");
        if (!File.Exists(scenarioPath))
        {
            return; // Skip if scenario missing
        }

        var result = _runner.RunScenario(scenarioPath, seed, maxTurns: 40);

        Assert.Empty(result.Violations);
    }
}
