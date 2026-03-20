using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Teleport
{
    public sealed class TeleportColumnSafetyScanner : ITeleportColumnSafetyScanner
    {
        private static readonly int[] YSearchOffsets = { 0, 1, -1, 2, -2, 3, -3, 4, -4, 5, -5, 6, -6, 7, -7, 8, -8 };

        private readonly ICoreServerAPI api;

        public TeleportColumnSafetyScanner(ICoreServerAPI api)
        {
            this.api = api;
        }

        public Vec3d FindSafeDestination(int x, int z, int referenceY, int dimension)
        {
            if (api?.World?.BlockAccessor == null)
            {
                return null;
            }

            foreach (int yOffset in YSearchOffsets)
            {
                int y = referenceY + yOffset;
                BlockPos feetPos = new BlockPos(x, y, z, dimension);
                BlockPos headPos = feetPos.UpCopy(1);
                BlockPos groundPos = feetPos.DownCopy(1);

                Block feetBlock = api.World.BlockAccessor.GetBlock(feetPos);
                Block headBlock = api.World.BlockAccessor.GetBlock(headPos);
                Block groundBlock = api.World.BlockAccessor.GetBlock(groundPos);

                if (!IsPassableTeleportSpace(feetBlock) || !IsPassableTeleportSpace(headBlock))
                {
                    continue;
                }

                if (!IsSafeTeleportGround(groundBlock))
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
