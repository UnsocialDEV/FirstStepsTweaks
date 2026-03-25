using FirstStepsTweaks.Discord;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class DiscordLinkServiceTests
{
    private readonly DateTime nowUtc = new(2026, 03, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateLinkCode_SavesPendingCodeForPlayer()
    {
        var linkedStore = new FakeLinkedAccountStore();
        var pendingStore = new FakePendingDiscordLinkCodeStore();
        var service = new DiscordLinkService(linkedStore, pendingStore, new DiscordLinkCodeMessageParser(), 15);

        DiscordLinkCodeIssue issue = service.CreateLinkCode("player-1", nowUtc);

        Assert.Single(pendingStore.GetPendingCodes(nowUtc));
        Assert.Contains(issue.Code, pendingStore.GetPendingCodes(nowUtc));
    }

    [Fact]
    public void TryCompleteLink_RejectsExpiredCode()
    {
        var linkedStore = new FakeLinkedAccountStore();
        var pendingStore = new FakePendingDiscordLinkCodeStore();
        pendingStore.SaveCode("ABC123", new PendingDiscordLinkCodeRecord("player-1", nowUtc.AddMinutes(-1).Ticks));
        var service = new DiscordLinkService(linkedStore, pendingStore, new DiscordLinkCodeMessageParser(), 15);

        bool result = service.TryCompleteLink("discord-1", "ABC123", nowUtc, out _);

        Assert.False(result);
        Assert.Null(linkedStore.GetLinkedDiscordUserId("player-1"));
    }

    [Fact]
    public void TryCompleteLink_LinksPlayerAndConsumesCode()
    {
        var linkedStore = new FakeLinkedAccountStore();
        var pendingStore = new FakePendingDiscordLinkCodeStore();
        pendingStore.SaveCode("ABC123", new PendingDiscordLinkCodeRecord("player-1", nowUtc.AddMinutes(10).Ticks));
        var service = new DiscordLinkService(linkedStore, pendingStore, new DiscordLinkCodeMessageParser(), 15);

        bool result = service.TryCompleteLink("discord-1", "link ABC123", nowUtc, out string playerUid);

        Assert.True(result);
        Assert.Equal("player-1", playerUid);
        Assert.Equal("discord-1", linkedStore.GetLinkedDiscordUserId("player-1"));
        Assert.Empty(pendingStore.GetPendingCodes(nowUtc));
    }

    private sealed class FakeLinkedAccountStore : IDiscordLinkedAccountStore
    {
        private readonly Dictionary<string, string> links = new(StringComparer.OrdinalIgnoreCase);

        public string GetLinkedDiscordUserId(string playerUid)
        {
            return links.TryGetValue(playerUid, out string discordUserId) ? discordUserId : null;
        }

        public void SetLinkedDiscordUserId(string playerUid, string discordUserId)
        {
            links[playerUid] = discordUserId;
        }

        public void ClearLinkedDiscordUserId(string playerUid)
        {
            links.Remove(playerUid);
        }
    }

    private sealed class FakePendingDiscordLinkCodeStore : IPendingDiscordLinkCodeStore
    {
        private readonly Dictionary<string, PendingDiscordLinkCodeRecord> pendingCodes = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> GetPendingCodes(DateTime nowUtc)
        {
            RemoveExpired(nowUtc);
            return pendingCodes.Keys.ToArray();
        }

        public bool TryGetCode(string code, DateTime nowUtc, out PendingDiscordLinkCodeRecord record)
        {
            RemoveExpired(nowUtc);
            return pendingCodes.TryGetValue(code, out record);
        }

        public void SaveCode(string code, PendingDiscordLinkCodeRecord record)
        {
            pendingCodes[code] = record;
        }

        public void RemoveCode(string code)
        {
            pendingCodes.Remove(code);
        }

        public void RemoveCodesForPlayer(string playerUid)
        {
            string[] matchingCodes = pendingCodes
                .Where(entry => entry.Value.PlayerUid == playerUid)
                .Select(entry => entry.Key)
                .ToArray();

            foreach (string code in matchingCodes)
            {
                pendingCodes.Remove(code);
            }
        }

        private void RemoveExpired(DateTime nowUtc)
        {
            string[] expiredCodes = pendingCodes
                .Where(entry => new DateTime(entry.Value.ExpiresAtUtcTicks, DateTimeKind.Utc) <= nowUtc)
                .Select(entry => entry.Key)
                .ToArray();

            foreach (string code in expiredCodes)
            {
                pendingCodes.Remove(code);
            }
        }
    }
}
