using System.Collections.Generic;
using FirstStepsTweaks.Discord.Messaging;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class DiscordMessageTranslatorTests
{
    private readonly DiscordMessageTranslator translator = new DiscordMessageTranslator();

    [Fact]
    public void ReplaceGameMentionsWithDiscordMentions_UsesConfiguredIds()
    {
        var map = new Dictionary<string, string>
        {
            ["alice"] = "12345"
        };

        string result = translator.ReplaceGameMentionsWithDiscordMentions("hello @alice", map);

        Assert.Equal("hello <@12345>", result);
    }

    [Fact]
    public void SanitizeDiscordContentForGame_ReplacesMentionAndRemovesDiscordTags()
    {
        var mentions = new[]
        {
            new DiscordMention { Id = "12345", Username = "alice" }
        };

        string result = translator.SanitizeDiscordContentForGame("hi <@12345> <#chan> <:wave:1>", mentions);

        Assert.Equal("hi @alice  ", result);
    }

    [Fact]
    public void StripVsFormatting_RemovesAngleBracketFormatting()
    {
        string result = translator.StripVsFormatting("<font color='red'>hello</font>");

        Assert.Equal("hello", result);
    }
}
