using System;
using FirstStepsTweaks.Config;

namespace FirstStepsTweaks.Services
{
    public sealed class DonatorChatMessageFormatter
    {
        private readonly DonatorTierPrefixRenderer prefixRenderer;

        public DonatorChatMessageFormatter()
        {
            prefixRenderer = new DonatorTierPrefixRenderer();
        }

        public string Format(string message, string tierLabel, ChatConfig config)
        {
            var prefixTemplate = string.IsNullOrWhiteSpace(config?.DonatorPrefixFormat)
                ? "{tier}"
                : config.DonatorPrefixFormat;

            if (string.Equals(prefixTemplate, "[{tier}]", StringComparison.Ordinal))
            {
                prefixTemplate = "{tier}";
            }

            var prefix = prefixTemplate.Replace("{tier}", prefixRenderer.Render(tierLabel, config), StringComparison.Ordinal);
            return $"{prefix} {message}";
        }
    }
}
