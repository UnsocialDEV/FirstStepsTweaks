using System;
using FirstStepsTweaks.Config;

namespace FirstStepsTweaks.Services
{
    public sealed class DonatorTierPrefixRenderer
    {
        public string Render(string tierLabel, ChatConfig config)
        {
            if (string.IsNullOrWhiteSpace(tierLabel))
            {
                return string.Empty;
            }

            return tierLabel switch
            {
                "Supporter" => ResolvePrefix(config?.SupporterPrefix, "•S"),
                "Contributor" => ResolvePrefix(config?.ContributorPrefix, "•C"),
                "Sponsor" => ResolvePrefix(config?.SponsorPrefix, "•SP"),
                "Patron" => ResolvePrefix(config?.PatronPrefix, "•P"),
                "Founder" => ResolvePrefix(config?.FounderPrefix, "•F"),
                _ => tierLabel
            };
        }

        private static string ResolvePrefix(string configuredPrefix, string fallbackPrefix)
        {
            return string.IsNullOrWhiteSpace(configuredPrefix)
                ? fallbackPrefix
                : configuredPrefix;
        }
    }
}
