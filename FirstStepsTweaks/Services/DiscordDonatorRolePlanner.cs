using System;
using System.Collections.Generic;

namespace FirstStepsTweaks.Services
{
    public sealed class DiscordDonatorRolePlanner
    {
        private readonly DonatorPrivilegeCatalog privilegeCatalog;

        public DiscordDonatorRolePlanner(DonatorPrivilegeCatalog privilegeCatalog)
        {
            this.privilegeCatalog = privilegeCatalog;
        }

        public DiscordDonatorRolePlan Plan(IReadOnlyCollection<string> discordRoleNames)
        {
            var normalizedDiscordRoleNames = new HashSet<string>(discordRoleNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            foreach (DonatorPrivilegeDefinition definition in privilegeCatalog.GetAll())
            {
                if (!normalizedDiscordRoleNames.Contains(definition.RoleName))
                {
                    continue;
                }

                return new DiscordDonatorRolePlan(definition.InGameRoleCode);
            }

            return new DiscordDonatorRolePlan(null);
        }
    }
}
