using FirstStepsTweaks.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class RtpCenterResolver : IRtpCenterResolver
    {
        private readonly RtpConfig config;
        private readonly IWorldManagerAPI worldManager;

        public RtpCenterResolver(RtpConfig config, ICoreServerAPI api)
            : this(config, api?.WorldManager)
        {
        }

        public RtpCenterResolver(RtpConfig config, IWorldManagerAPI worldManager)
        {
            this.config = config ?? new RtpConfig();
            this.worldManager = worldManager;
        }

        public Vec2d Resolve(Vec3d currentPosition)
        {
            if (config.UsePlayerPositionAsCenter)
            {
                return currentPosition == null
                    ? null
                    : new Vec2d(currentPosition.X, currentPosition.Z);
            }

            return new Vec2d(
                GetHorizontalOffset(worldManager?.MapSizeX ?? 0),
                GetHorizontalOffset(worldManager?.MapSizeZ ?? 0));
        }

        private static int GetHorizontalOffset(int mapSize)
        {
            return mapSize > 0 ? mapSize / 2 : 0;
        }
    }
}
