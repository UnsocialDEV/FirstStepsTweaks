using System;
using FirstStepsTweaks.Config;
using Vintagestory.API.Common;

namespace FirstStepsTweaks.Services
{
    public sealed class PlayerHomeLimitResolver
    {
        private readonly DonatorPrivilegeCatalog privilegeCatalog;

        public PlayerHomeLimitResolver()
            : this(new DonatorPrivilegeCatalog())
        {
        }

        public PlayerHomeLimitResolver(DonatorPrivilegeCatalog privilegeCatalog)
        {
            this.privilegeCatalog = privilegeCatalog;
        }

        public int Resolve(IPlayer player, TeleportConfig teleportConfig)
        {
            if (player == null)
            {
                return Resolve(_ => false, teleportConfig);
            }

            return Resolve(player.HasPrivilege, teleportConfig);
        }

        public int Resolve(System.Func<string, bool> hasPrivilege, TeleportConfig teleportConfig)
        {
            HomeLimitConfig limits = teleportConfig?.HomeLimits ?? new HomeLimitConfig();

            foreach (DonatorPrivilegeDefinition definition in privilegeCatalog.GetAll())
            {
                if (!hasPrivilege(definition.Privilege))
                {
                    continue;
                }

                return GetTierLimit(definition.Tier, limits);
            }

            return Math.Max(1, limits.Default);
        }

        private static int GetTierLimit(DonatorTier tier, HomeLimitConfig limits)
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
