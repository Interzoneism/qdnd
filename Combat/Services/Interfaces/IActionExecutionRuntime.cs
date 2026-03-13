using Godot;

namespace QDND.Combat.Services
{
    public interface IActionExecutionRuntime
    {
        SceneTreeTimer CreateTimer(double seconds);
        void CheckAndEndCombat();
    }
}
