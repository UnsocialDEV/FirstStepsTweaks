using System;
using System.Collections.Generic;

namespace FirstStepsTweaks.Services
{
    public sealed class DiscordDonatorRolePlanner
    {
        private readonly DonatorTierCatalog tierCatalog;

        public DiscordDonatorRolePlanner(DonatorTierCatalog tierCatalog)
        {
            this.tierCatalog = tierCatalog;
        }

        public DiscordDonatorRolePlan Plan(IReadOnlyCollection<string> discordRoleNames)
        {
            var normalizedDiscordRoleNames = new HashSet<string>(discordRoleNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            foreach (DonatorTierDefinition definition in tierCatalog.GetAll())
            {
                if (normalizedDiscordRoleNames.Contains(definition.DiscordRoleName))
                {
                    return new DiscordDonatorRolePlan(definition.RoleCode);
                }
            }

            return new DiscordDonatorRolePlan(null);
        }
    }
}
