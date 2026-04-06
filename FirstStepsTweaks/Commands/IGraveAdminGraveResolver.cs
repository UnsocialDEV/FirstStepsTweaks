using FirstStepsTweaks.Services;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public interface IGraveAdminGraveResolver
    {
        bool TryResolveTargetedGraveId(IServerPlayer player, out string graveId, out string message);

        bool TryGetActiveGrave(string graveId, out GraveData grave);
    }
}
