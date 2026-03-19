using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FirstStepsTweaks.Discord.Messaging
{
    public sealed class DiscordMessageTranslator : IDiscordMessageTranslator
    {
        public string StripVsFormatting(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return Regex.Replace(input, "<.*?>", string.Empty);
        }

        public string ReplaceGameMentionsWithDiscordMentions(string message, IDictionary<string, string> mentionMap)
        {
            if (string.IsNullOrWhiteSpace(message) || mentionMap == null || mentionMap.Count == 0)
            {
                return message;
            }

            return Regex.Replace(
                message,
                @"(?<!\w)@([A-Za-z0-9_.-]+)",
                match =>
                {
                    string key = match.Groups[1].Value;

                    if (!mentionMap.TryGetValue(key, out string discordId)
                        && !mentionMap.TryGetValue(key.ToLowerInvariant(), out discordId))
                    {
                        return match.Value;
                    }

                    if (string.IsNullOrWhiteSpace(discordId))
                    {
                        return match.Value;
                    }

                    return $"<@{discordId.Trim()}>";
                }
            );
        }

        public string SanitizeDiscordContentForGame(string content, IEnumerable<DiscordMention> mentions)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return content;
            }

            if (mentions != null)
            {
                foreach (DiscordMention mention in mentions)
                {
                    if (mention == null || string.IsNullOrWhiteSpace(mention.Id))
                    {
                        continue;
                    }

                    string mentionName = !string.IsNullOrWhiteSpace(mention.GlobalName)
                        ? mention.GlobalName
                        : (!string.IsNullOrWhiteSpace(mention.Username) ? mention.Username : "user");

                    content = content.Replace($"<@{mention.Id}>", $"@{mentionName}");
                    content = content.Replace($"<@!{mention.Id}>", $"@{mentionName}");
                }
            }

            return Regex.Replace(content, "<[^>]+>", string.Empty);
        }
    }
}
