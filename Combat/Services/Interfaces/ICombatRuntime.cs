using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Entities;

namespace QDND.Combat.Services
{
    public interface ICombatRuntime
    {
        bool IsAutoBattleMode { get; }
        bool UseBuiltInAI { get; }
        SceneTreeTimer CreateTimer(double seconds);
        Random GetRandom();
        IReadOnlyList<Combatant> GetCombatants();
    }
}
