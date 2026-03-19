using FirstStepsTweaks.Services;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Gravestones
{
    public interface IGraveRestorer
    {
        int DuplicateToPlayer(GraveData grave, IServerPlayer targetPlayer);

        bool TryRestore(GraveData grave, IServerPlayer targetPlayer, bool removeBlock, out int transferredStacks, out int failedStacks);
    }
}
