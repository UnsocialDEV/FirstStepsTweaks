using System.Collections.Generic;

namespace FirstStepsTweaks.Services
{
    public sealed class DiscordDonatorPrivilegePlan
    {
        public DiscordDonatorPrivilegePlan(IReadOnlyCollection<string> targetPrivileges)
        {
            TargetPrivileges = targetPrivileges;
        }

        public IReadOnlyCollection<string> TargetPrivileges { get; }
    }
}
