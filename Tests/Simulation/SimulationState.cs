#nullable enable
using System;
using System.Collections.Generic;
using QDND.Combat.Rules;

namespace QDND.Tests.Simulation;

/// <summary>
/// Lightweight combat state for headless simulation.
/// </summary>
public class SimulationState
{
    private readonly DiceRoller _dice;
    private readonly List<SimulatedCombatant> _combatants = new();
    private int _currentTurnIndex = 0;

    public int Seed { get; }
    public int TurnCount { get; private set; }
    public int RoundCount { get; private set; } = 1;
    public bool IsComplete => GetLivingTeams().Count <= 1;
    public IReadOnlyList<SimulatedCombatant> Combatants => _combatants;

    public SimulationState(int seed)
    {
        Seed = seed;
        _dice = new DiceRoller(seed);
    }

    public void AddCombatant(ScenarioCombatant config)
    {
        _combatants.Add(new SimulatedCombatant
        {
            Id = config.Id,
            Team = config.Team,
            MaxHP = config.MaxHP,
            CurrentHP = config.MaxHP,
            AC = config.AC,
            AttackBonus = config.AttackBonus,
            DamageBonus = config.DamageBonus,
            Initiative = _dice.RollD20() + config.InitiativeBonus
        });
    }

    public void ExecuteTurn()
    {
        if (IsComplete) return;

        // Sort by initiative on first turn
        if (TurnCount == 0)
        {
            _combatants.Sort((a, b) => b.Initiative.CompareTo(a.Initiative));
        }

        // Get current combatant
        var current = GetCurrentCombatant();
        if (current == null || !current.IsAlive)
        {
            AdvanceTurn();
            return;
        }

        // Simple AI: attack first enemy
        var target = FindFirstEnemy(current.Team);
        if (target != null)
        {
            ExecuteAttack(current, target);
        }

        AdvanceTurn();
    }

    private SimulatedCombatant? GetCurrentCombatant()
    {
        var living = _combatants.FindAll(c => c.IsAlive);
        if (living.Count == 0) return null;
        return living[_currentTurnIndex % living.Count];
    }

    private void AdvanceTurn()
    {
        TurnCount++;
        _currentTurnIndex++;

        var living = _combatants.FindAll(c => c.IsAlive);
        if (_currentTurnIndex >= living.Count)
        {
            _currentTurnIndex = 0;
            RoundCount++;
        }
    }

    private SimulatedCombatant? FindFirstEnemy(int myTeam)
    {
        return _combatants.Find(c => c.IsAlive && c.Team != myTeam);
    }

    private void ExecuteAttack(SimulatedCombatant attacker, SimulatedCombatant target)
    {
        var roll = _dice.RollD20();
        var total = roll + attacker.AttackBonus;

        if (total >= target.AC)
        {
            // Hit - roll damage
            var damage = _dice.Roll(1, 8, attacker.DamageBonus);
            target.CurrentHP -= damage;
        }
    }

    private HashSet<int> GetLivingTeams()
    {
        var teams = new HashSet<int>();
        foreach (var c in _combatants)
        {
            if (c.IsAlive) teams.Add(c.Team);
        }
        return teams;
    }

    public string ComputeHash()
    {
        // Simple hash based on state
        var hash = Seed * 31 + TurnCount;
        foreach (var c in _combatants)
        {
            hash = hash * 31 + c.CurrentHP;
            hash = hash * 31 + (c.IsAlive ? 1 : 0);
        }
        return hash.ToString("X8");
    }
}

/// <summary>
/// A combatant in the simulation.
/// </summary>
public class SimulatedCombatant
{
    public string Id { get; set; } = "";
    public int Team { get; set; }
    public int MaxHP { get; set; }
    public int CurrentHP { get; set; }
    public int AC { get; set; } = 10;
    public int AttackBonus { get; set; }
    public int DamageBonus { get; set; }
    public int Initiative { get; set; }

    public bool IsAlive => CurrentHP > 0;
}
