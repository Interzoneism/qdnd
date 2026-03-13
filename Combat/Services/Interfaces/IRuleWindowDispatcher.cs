using QDND.Combat.Entities;
using QDND.Combat.Rules;

namespace QDND.Combat.Services
{
    public interface IRuleWindowDispatcher
    {
        void Dispatch(RuleWindow window, Combatant source, Combatant target);
    }
}
