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

        public DiscordDonatorPrivilegePlan Plan(IReadOnlyCollection<string> discordRoleNames, Func<string, bool> hasPrivilege)
        {
            var normalizedDiscordRoleNames = new HashSet<string>(discordRoleNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var privilegesToGrant = new List<string>();
            var privilegesToRevoke = new List<string>();
            string highestMatchingPrivilege = null;

            foreach (DonatorPrivilegeDefinition definition in privilegeCatalog.GetAll())
            {
                if (!normalizedDiscordRoleNames.Contains(definition.RoleName))
                {
                    continue;
                }

                highestMatchingPrivilege = definition.Privilege;
                break;
            }

            foreach (DonatorPrivilegeDefinition definition in privilegeCatalog.GetAll())
            {
                bool shouldHavePrivilege = string.Equals(definition.Privilege, highestMatchingPrivilege, StringComparison.OrdinalIgnoreCase);
                bool currentlyHasPrivilege = hasPrivilege?.Invoke(definition.Privilege) == true;

                if (shouldHavePrivilege && !currentlyHasPrivilege)
                {
                    privilegesToGrant.Add(definition.Privilege);
                    continue;
                }

                if (!shouldHavePrivilege && currentlyHasPrivilege)
                {
                    privilegesToRevoke.Add(definition.Privilege);
                }
            }

            return new DiscordDonatorPrivilegePlan(privilegesToGrant, privilegesToRevoke);
        }
    }
}
