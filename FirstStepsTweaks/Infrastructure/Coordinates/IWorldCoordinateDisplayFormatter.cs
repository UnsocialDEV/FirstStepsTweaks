using Vintagestory.API.MathTools;

namespace FirstStepsTweaks.Infrastructure.Coordinates
{
    public interface IWorldCoordinateDisplayFormatter
    {
        Vec3d ToDisplayPosition(Vec3d worldPosition);

        BlockPos ToDisplayPosition(BlockPos worldPosition);

        string FormatBlockPosition(int dimension, int x, int y, int z);

        string FormatBlockPosition(BlockPos worldPosition);

        string FormatBlockPositionWithoutDimension(BlockPos worldPosition);
    }
}
