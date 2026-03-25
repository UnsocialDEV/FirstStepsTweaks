using System;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordRelayMessageNormalizer
    {
        public string NormalizePlayerChat(string playerName, string message)
        {
            if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(playerName))
            {
                return message;
            }

            string speakerPrefix = playerName + ": ";
            if (message.StartsWith(speakerPrefix, StringComparison.Ordinal))
            {
                return message.Substring(speakerPrefix.Length);
            }

            int prefixIndex = message.IndexOf(speakerPrefix, StringComparison.Ordinal);
            if (prefixIndex <= 0)
            {
                return message;
            }

            return message.Substring(prefixIndex + speakerPrefix.Length);
        }
    }
}
