using QDND.Combat.VFX;

namespace QDND.Combat.Services
{
    public interface IVfxRuleResolver
    {
        VfxResolvedSpec Resolve(VfxRequest request);
    }

    public interface IVfxPlaybackService
    {
        void Handle(VfxRequest request);
    }
}
