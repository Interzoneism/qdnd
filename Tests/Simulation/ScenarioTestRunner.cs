#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QDND.Tests.Simulation;

/// <summary>
/// Loads and runs scenario files for automated testing.
/// </summary>
public class ScenarioTestRunner
{
    private readonly SimulationRunner _runner = new();
    private readonly string _scenarioDirectory;
    
    public ScenarioTestRunner(string scenarioDirectory)
    {
        _scenarioDirectory = scenarioDirectory;
    }
    
    /// <summary>
    /// Get all scenario file paths from the directory.
    /// </summary>
    public IEnumerable<string> GetScenarioFiles()
    {
        if (!Directory.Exists(_scenarioDirectory))
            return Enumerable.Empty<string>();
        
        return Directory.GetFiles(_scenarioDirectory, "test_*.json");
    }
    
    /// <summary>
    /// Run a specific scenario file with the given seed.
    /// </summary>
    public SimulationResult RunScenario(string scenarioPath, int seed, int? maxTurns = null)
    {
        var scenario = SimulationScenario.LoadFromFile(scenarioPath);
        return _runner.Run(scenario, seed, maxTurns ?? scenario.DefaultMaxTurns);
    }
    
    /// <summary>
    /// Run all scenarios with given seed and return results.
    /// </summary>
    public List<ScenarioTestResult> RunAllScenarios(int seed)
    {
        var results = new List<ScenarioTestResult>();
        
        foreach (var path in GetScenarioFiles())
        {
            try
            {
                var result = RunScenario(path, seed);
                results.Add(new ScenarioTestResult
                {
                    ScenarioPath = path,
                    ScenarioName = result.ScenarioName,
                    Passed = result.Completed && result.Violations.Count == 0,
                    Result = result
                });
            }
            catch (Exception ex)
            {
                results.Add(new ScenarioTestResult
                {
                    ScenarioPath = path,
                    ScenarioName = Path.GetFileNameWithoutExtension(path),
                    Passed = false,
                    Error = ex.Message
                });
            }
        }
        
        return results;
    }
}

public class ScenarioTestResult
{
    public string ScenarioPath { get; set; } = "";
    public string ScenarioName { get; set; } = "";
    public bool Passed { get; set; }
    public SimulationResult? Result { get; set; }
    public string? Error { get; set; }
}
