using FirstStepsTweaks.Config;

namespace FirstStepsTweaks.Services
{
    public sealed class DonatorChatPrefixApplicator
    {
        private readonly DonatorTierResolver donatorTierResolver;
        private readonly DonatorChatMessageFormatter donatorChatMessageFormatter;

        public DonatorChatPrefixApplicator()
        {
            donatorTierResolver = new DonatorTierResolver();
            donatorChatMessageFormatter = new DonatorChatMessageFormatter();
        }

        public string Apply(string message, System.Func<string, bool> hasPrivilege, ChatConfig config)
        {
            if (!config.EnableDonatorPrefixes || string.IsNullOrWhiteSpace(message) || message.StartsWith("/"))
            {
                return message;
            }

            var tierLabel = donatorTierResolver.ResolveLabel(hasPrivilege);
            if (string.IsNullOrWhiteSpace(tierLabel))
            {
                return message;
            }

            return donatorChatMessageFormatter.Format(message, tierLabel, config);
        }
    }
}
