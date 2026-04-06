using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Common;

namespace FirstStepsTweaks.Services
{
    public sealed class PlayerTeleportWarmupResolver
    {
        private readonly DonatorTierResolver tierResolver;

        public PlayerTeleportWarmupResolver()
            : this(new DonatorTierResolver(new DonatorTierCatalog(), new PlayerRoleCodeReader()))
        {
        }

        public PlayerTeleportWarmupResolver(DonatorTierResolver tierResolver)
        {
            this.tierResolver = tierResolver;
        }

        public int Resolve(IPlayer player, TeleportConfig teleportConfig)
        {
            if (player == null)
            {
                return Resolve(roleCode: null, teleportConfig);
            }

            return Resolve(tierResolver.ResolveTier(player), teleportConfig);
        }

        public int Resolve(string roleCode, TeleportConfig teleportConfig)
        {
            return Resolve(tierResolver.ResolveTier(roleCode), teleportConfig);
        }

        private static int Resolve(DonatorTier? tier, TeleportConfig teleportConfig)
        {
            int defaultWarmupSeconds = System.Math.Max(0, teleportConfig?.WarmupSeconds ?? 0);
            int donatorWarmupSeconds = System.Math.Max(0, teleportConfig?.DonatorWarmupSeconds ?? TeleportConfig.DefaultDonatorWarmupSeconds);

            if (tier.HasValue)
            {
                return donatorWarmupSeconds;
            }

            return defaultWarmupSeconds;
        }
    }
}
