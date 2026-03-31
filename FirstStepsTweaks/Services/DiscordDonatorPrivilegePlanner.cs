using System;
using System.Collections.Generic;

namespace FirstStepsTweaks.Services
{
    public sealed class DiscordDonatorPrivilegePlanner
    {
        private readonly DonatorPrivilegeCatalog privilegeCatalog;

        public DiscordDonatorPrivilegePlanner(DonatorPrivilegeCatalog privilegeCatalog)
        {
            this.privilegeCatalog = privilegeCatalog;
        }

        public DiscordDonatorPrivilegePlan Plan(IReadOnlyCollection<string> discordRoleNames)
        {
            var normalizedDiscordRoleNames = new HashSet<string>(discordRoleNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var targetPrivileges = new List<string>();
            bool matchedTier = false;

            foreach (DonatorPrivilegeDefinition definition in privilegeCatalog.GetAll())
            {
                if (!matchedTier && !normalizedDiscordRoleNames.Contains(definition.RoleName))
                {
                    continue;
                }

                matchedTier = true;
                targetPrivileges.Add(definition.Privilege);
            }

            return new DiscordDonatorPrivilegePlan(targetPrivileges);
        }
    }
}
