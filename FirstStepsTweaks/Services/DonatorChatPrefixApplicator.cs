using FirstStepsTweaks.Config;
using Vintagestory.API.Common;

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

        public string Apply(string message, IPlayer player, ChatConfig config)
        {
            if (!config.EnableDonatorPrefixes || string.IsNullOrWhiteSpace(message) || message.StartsWith("/"))
            {
                return message;
            }

            string tierLabel = donatorTierResolver.ResolveLabel(player);
            if (string.IsNullOrWhiteSpace(tierLabel))
            {
                return message;
            }

            return donatorChatMessageFormatter.Format(message, tierLabel, config);
        }
    }
}
