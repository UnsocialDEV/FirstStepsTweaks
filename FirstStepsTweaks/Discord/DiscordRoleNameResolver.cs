using System;
using System.Collections.Generic;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordRoleNameResolver
    {
        public IReadOnlyCollection<string> ResolveRoleNames(IReadOnlyCollection<string> memberRoleIds, IReadOnlyCollection<DiscordGuildRole> guildRoles)
        {
            if (memberRoleIds == null || guildRoles == null || memberRoleIds.Count == 0 || guildRoles.Count == 0)
            {
                return Array.Empty<string>();
            }

            var resolvedRoleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var memberRoleIdSet = new HashSet<string>(memberRoleIds, StringComparer.OrdinalIgnoreCase);

            foreach (DiscordGuildRole guildRole in guildRoles)
            {
                if (guildRole == null || string.IsNullOrWhiteSpace(guildRole.Id) || string.IsNullOrWhiteSpace(guildRole.Name))
                {
                    continue;
                }

                if (!memberRoleIdSet.Contains(guildRole.Id))
                {
                    continue;
                }

                resolvedRoleNames.Add(guildRole.Name.Trim().ToLowerInvariant());
            }

            return resolvedRoleNames;
        }
    }
}
