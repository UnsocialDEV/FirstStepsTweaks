using System.Collections.Generic;

namespace FirstStepsTweaks.Services
{
    public sealed class DiscordDonatorPrivilegePlan
    {
        public DiscordDonatorPrivilegePlan(IReadOnlyCollection<string> privilegesToGrant, IReadOnlyCollection<string> privilegesToRevoke)
        {
            PrivilegesToGrant = privilegesToGrant;
            PrivilegesToRevoke = privilegesToRevoke;
        }

        public IReadOnlyCollection<string> PrivilegesToGrant { get; }

        public IReadOnlyCollection<string> PrivilegesToRevoke { get; }
    }
}
