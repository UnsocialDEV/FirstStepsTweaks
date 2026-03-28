using FirstStepsTweaks.Config;
using Vintagestory.API.Common;

namespace FirstStepsTweaks.Services
{
    public sealed class PlayerTeleportWarmupResolver
    {
        private readonly DonatorPrivilegeCatalog privilegeCatalog;

        public PlayerTeleportWarmupResolver()
            : this(new DonatorPrivilegeCatalog())
        {
        }

        public PlayerTeleportWarmupResolver(DonatorPrivilegeCatalog privilegeCatalog)
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
            int defaultWarmupSeconds = System.Math.Max(0, teleportConfig?.WarmupSeconds ?? 0);
            int donatorWarmupSeconds = System.Math.Max(0, teleportConfig?.DonatorWarmupSeconds ?? TeleportConfig.DefaultDonatorWarmupSeconds);

            foreach (DonatorPrivilegeDefinition definition in privilegeCatalog.GetAll())
            {
                if (hasPrivilege(definition.Privilege))
                {
                    return donatorWarmupSeconds;
                }
            }

            return defaultWarmupSeconds;
        }
    }
}
