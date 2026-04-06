using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Coordinates
{
    public sealed class WorldCoordinateDisplayFormatter : IWorldCoordinateDisplayFormatter
    {
        private readonly IWorldManagerAPI worldManager;

        public WorldCoordinateDisplayFormatter(ICoreServerAPI api)
            : this(api?.WorldManager)
        {
        }

        public WorldCoordinateDisplayFormatter(IWorldManagerAPI worldManager)
        {
            this.worldManager = worldManager;
        }

        public Vec3d ToDisplayPosition(Vec3d worldPosition)
        {
            if (worldPosition == null)
            {
                return null;
            }

            return new Vec3d(
                worldPosition.X - GetHorizontalOffset(worldManager?.MapSizeX ?? 0),
                worldPosition.Y,
                worldPosition.Z - GetHorizontalOffset(worldManager?.MapSizeZ ?? 0));
        }

        public BlockPos ToDisplayPosition(BlockPos worldPosition)
        {
            if (worldPosition == null)
            {
                return null;
            }

            return new BlockPos(
                worldPosition.X - GetHorizontalOffset(worldManager?.MapSizeX ?? 0),
                worldPosition.Y,
                worldPosition.Z - GetHorizontalOffset(worldManager?.MapSizeZ ?? 0),
                worldPosition.dimension);
        }

        public string FormatBlockPosition(int dimension, int x, int y, int z)
        {
            return FormatBlockPosition(new BlockPos(x, y, z, dimension));
        }

        public string FormatBlockPosition(BlockPos worldPosition)
        {
            BlockPos displayPosition = ToDisplayPosition(worldPosition);
            if (displayPosition == null)
            {
                return string.Empty;
            }

            return $"{displayPosition.dimension}:{displayPosition.X},{displayPosition.Y},{displayPosition.Z}";
        }

        public string FormatBlockPositionWithoutDimension(BlockPos worldPosition)
        {
            BlockPos displayPosition = ToDisplayPosition(worldPosition);
            if (displayPosition == null)
            {
                return string.Empty;
            }

            return $"{displayPosition.X},{displayPosition.Y},{displayPosition.Z}";
        }

        private static int GetHorizontalOffset(int mapSize)
        {
            return mapSize > 0 ? mapSize / 2 : 0;
        }
    }
}
