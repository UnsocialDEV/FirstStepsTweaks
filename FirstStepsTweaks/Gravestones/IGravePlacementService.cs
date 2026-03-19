using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Gravestones
{
    public interface IGravePlacementService
    {
        GravePlacementResult FindPlacementPosition(IServerPlayer player, BlockPos deathPos, Block graveBlock);

        Vec3d FindSafeTeleportTarget(GraveData grave);
    }
}
