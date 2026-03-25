using System.Collections.Generic;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordMemberRoles
    {
        public DiscordMemberRoles(IReadOnlyCollection<string> memberRoleIds, IReadOnlyCollection<DiscordGuildRole> guildRoles)
        {
            MemberRoleIds = memberRoleIds;
            GuildRoles = guildRoles;
        }

        public IReadOnlyCollection<string> MemberRoleIds { get; }

        public IReadOnlyCollection<DiscordGuildRole> GuildRoles { get; }
    }
}
