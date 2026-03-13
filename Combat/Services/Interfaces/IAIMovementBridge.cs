using System.Collections.Generic;
using Godot;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Movement;

namespace QDND.Combat.Services
{
    public interface IAIMovementBridge
    {
        JumpPathResult BuildJumpPath(Combatant combatant, Vector3 targetGridPos);
        float GetJumpDistanceLimit(Combatant combatant);
        bool ExecuteAIMovementWithFallback(Combatant actor, AIAction action, List<AIAction> allActions);
        bool ExecuteDash(Combatant actor);
        bool ExecuteDisengage(Combatant actor);
    }
}
