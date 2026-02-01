#nullable enable
using System;
using System.Collections.Generic;

namespace QDND.Tests.Simulation;

/// <summary>
/// Headless combat simulation runner for automated testing.
/// Can run complete combats and verify invariants without visual output.
/// </summary>
public class SimulationRunner
{
    private readonly InvariantChecker _invariantChecker = new();
    
    /// <summary>
    /// Run a simulation from a scenario.
    /// </summary>
    public SimulationResult Run(SimulationScenario scenario, int seed, int maxTurns = 100)
    {
        var result = new SimulationResult
        {
            ScenarioName = scenario.Name,
            Seed = seed,
            StartTime = DateTime.UtcNow
        };
        
        try
        {
            // Initialize simulation state
            var state = new SimulationState(seed);
            
            // Setup from scenario
            foreach (var combatant in scenario.Combatants)
            {
                state.AddCombatant(combatant);
            }
            
            // Run turns until completion or max turns
            while (!state.IsComplete && state.TurnCount < maxTurns)
            {
                state.ExecuteTurn();
                
                // Check invariants after each turn
                var violations = _invariantChecker.CheckAll(state);
                result.Violations.AddRange(violations);
                
                if (violations.Count > 0 && scenario.StopOnViolation)
                {
                    result.TerminationReason = "Invariant violation";
                    break;
                }
            }
            
            // Set results
            result.Completed = state.IsComplete;
            result.TurnCount = state.TurnCount;
            result.FinalStateHash = state.ComputeHash();
            
            if (!state.IsComplete && state.TurnCount >= maxTurns)
            {
                result.TerminationReason = "Max turns exceeded";
            }
            else if (result.TerminationReason == null)
            {
                result.TerminationReason = "Combat ended normally";
            }
        }
        catch (Exception ex)
        {
            result.Completed = false;
            result.TerminationReason = $"Exception: {ex.Message}";
            result.Exception = ex;
        }
        
        result.EndTime = DateTime.UtcNow;
        return result;
    }
    
    /// <summary>
    /// Run a simulation from a scenario file path.
    /// </summary>
    public SimulationResult Run(string scenarioPath, int seed, int maxTurns = 100)
    {
        var scenario = SimulationScenario.LoadFromFile(scenarioPath);
        return Run(scenario, seed, maxTurns);
    }
}

/// <summary>
/// Result of a simulation run.
/// </summary>
public class SimulationResult
{
    public string ScenarioName { get; set; } = "";
    public int Seed { get; set; }
    public bool Completed { get; set; }
    public string? TerminationReason { get; set; }
    public int TurnCount { get; set; }
    public string FinalStateHash { get; set; } = "";
    public List<InvariantViolation> Violations { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Exception? Exception { get; set; }
    
    public TimeSpan Duration => EndTime - StartTime;
}
