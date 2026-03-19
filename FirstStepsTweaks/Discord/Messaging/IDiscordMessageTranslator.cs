using System.Collections.Generic;

namespace FirstStepsTweaks.Discord.Messaging
{
    public interface IDiscordMessageTranslator
    {
        string StripVsFormatting(string input);
        string ReplaceGameMentionsWithDiscordMentions(string message, IDictionary<string, string> mentionMap);
        string SanitizeDiscordContentForGame(string content, IEnumerable<DiscordMention> mentions);
    }
}
