using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Coordinates
{
    public interface IWorldCoordinateReader
    {
        Vec3d GetExactPosition(IServerPlayer player);

        Vec3d GetExactPosition(Entity entity);

        BlockPos GetBlockPosition(IServerPlayer player);

        BlockPos GetBlockPosition(Entity entity);

        int? GetDimension(IServerPlayer player);

        int? GetDimension(Entity entity);
    }
}
