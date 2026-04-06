using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Common;

namespace FirstStepsTweaks.Services
{
    public sealed class DonatorTierResolver
    {
        private readonly DonatorTierCatalog tierCatalog;
        private readonly IPlayerRoleCodeReader roleCodeReader;

        public DonatorTierResolver()
            : this(new DonatorTierCatalog(), new PlayerRoleCodeReader())
        {
        }

        public DonatorTierResolver(DonatorTierCatalog tierCatalog, IPlayerRoleCodeReader roleCodeReader)
        {
            this.tierCatalog = tierCatalog;
            this.roleCodeReader = roleCodeReader;
        }

        public string ResolveLabel(IPlayer player)
        {
            if (player == null)
            {
                return null;
            }

            return ResolveLabel(roleCodeReader.Read(player));
        }

        public string ResolveLabel(string roleCode)
        {
            return tierCatalog.FindByRoleCode(roleCode)?.Label;
        }

        public DonatorTier? ResolveTier(IPlayer player)
        {
            if (player == null)
            {
                return null;
            }

            return ResolveTier(roleCodeReader.Read(player));
        }

        public DonatorTier? ResolveTier(string roleCode)
        {
            return tierCatalog.FindByRoleCode(roleCode)?.Tier;
        }
    }
}
