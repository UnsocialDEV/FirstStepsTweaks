using System;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Common;

namespace FirstStepsTweaks.Services
{
    public sealed class PlayerHomeLimitResolver
    {
        private readonly DonatorTierResolver tierResolver;

        public PlayerHomeLimitResolver()
            : this(new DonatorTierResolver(new DonatorTierCatalog(), new PlayerRoleCodeReader()))
        {
        }

        public PlayerHomeLimitResolver(DonatorTierResolver tierResolver)
        {
            this.tierResolver = tierResolver;
        }

        public int Resolve(IPlayer player, TeleportConfig teleportConfig)
        {
            if (player == null)
            {
                return Resolve(roleCode: null, teleportConfig);
            }

            DonatorTier? tier = tierResolver.ResolveTier(player);
            HomeLimitConfig limits = teleportConfig?.HomeLimits ?? new HomeLimitConfig();
            return GetTierLimit(tier, limits);
        }

        public int Resolve(string roleCode, TeleportConfig teleportConfig)
        {
            HomeLimitConfig limits = teleportConfig?.HomeLimits ?? new HomeLimitConfig();
            DonatorTier? tier = tierResolver.ResolveTier(roleCode);
            return GetTierLimit(tier, limits);
        }

        private static int GetTierLimit(DonatorTier? tier, HomeLimitConfig limits)
        {
            return tier switch
            {
                DonatorTier.Supporter => Math.Max(1, limits.Supporter),
                DonatorTier.Contributor => Math.Max(1, limits.Contributor),
                DonatorTier.Sponsor => Math.Max(1, limits.Sponsor),
                DonatorTier.Patron => Math.Max(1, limits.Patron),
                DonatorTier.Founder => Math.Max(1, limits.Founder),
                _ => Math.Max(1, limits.Default)
            };
        }
    }
}
