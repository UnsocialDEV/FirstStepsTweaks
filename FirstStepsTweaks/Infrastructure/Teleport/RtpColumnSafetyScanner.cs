using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Teleport
{
    public sealed class RtpColumnSafetyScanner : IRtpColumnSafetyScanner
    {
        private readonly ICoreServerAPI api;

        public RtpColumnSafetyScanner(ICoreServerAPI api)
        {
            this.api = api;
        }

        public Vec3d FindSafeDestination(int x, int z, int dimension)
        {
            if (api?.World?.BlockAccessor == null)
            {
                return null;
            }

            int terrainHeight = api.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos(x, 0, z, dimension));
            return FindSafeDestination(
                x,
                z,
                dimension,
                terrainHeight,
                pos => IsPassableTeleportSpace(api.World.BlockAccessor.GetBlock(pos)),
                pos => IsSafeTeleportGround(api.World.BlockAccessor.GetBlock(pos)));
        }

        internal static Vec3d FindSafeDestination(
            int x,
            int z,
            int dimension,
            int terrainHeight,
            System.Func<BlockPos, bool> isPassableTeleportSpace,
            System.Func<BlockPos, bool> isSafeTeleportGround)
        {
            int scanStartY = Math.Max(2, terrainHeight + 6);
            int scanEndY = Math.Max(2, terrainHeight - 20);

            for (int y = scanStartY; y >= scanEndY; y--)
            {
                BlockPos feetPos = new BlockPos(x, y, z, dimension);
                BlockPos headPos = feetPos.UpCopy(1);
                BlockPos groundPos = feetPos.DownCopy(1);

                if (!isPassableTeleportSpace(feetPos) || !isPassableTeleportSpace(headPos))
                {
                    continue;
                }

                if (!isSafeTeleportGround(groundPos))
                {
                    continue;
                }

                return new Vec3d(x + 0.5, y, z + 0.5);
            }

            return null;
        }

        private static bool IsPassableTeleportSpace(Block block)
        {
            return block != null && (block.BlockId == 0 || block.Replaceable >= 6000);
        }

        private static bool IsSafeTeleportGround(Block block)
        {
            return block != null && block.BlockId != 0 && block.Replaceable < 6000;
        }
    }
}
