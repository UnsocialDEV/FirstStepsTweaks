using System.Reflection;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Discord.Transport;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class DiscordLinkPollerTests
{
    [Fact]
    public async Task PollOnceAsync_WhenNoSavedLastMessageId_PrimesNewestMessageWithoutReplying()
    {
        var webhookClient = new FakeDiscordWebhookClient(
            """
            [
              { "id": "300", "content": "ABC123", "author": { "id": "discord-1" } },
              { "id": "200", "content": "older", "author": { "id": "discord-2" } }
            ]
            """);
        var lastMessageStore = new FakeDiscordLinkLastMessageStore();
        var poller = CreatePoller(webhookClient, lastMessageStore);

        await InvokePollOnceAsync(poller);

        Assert.Equal("300", lastMessageStore.SavedLastMessageId);
        Assert.Equal(1, webhookClient.GetCallCount);
        Assert.Equal(0, webhookClient.PostBotJsonCallCount);
    }

    [Fact]
    public async Task PollOnceAsync_WhenLinkedPlayerIsOnline_GrantsRewardOnce()
    {
        var webhookClient = new FakeDiscordWebhookClient(
            """
            [
              { "id": "101", "content": "ABC123", "author": { "id": "discord-1" } }
            ]
            """);
        var lastMessageStore = new FakeDiscordLinkLastMessageStore("100");
        var linkedAccountStore = new FakeLinkedAccountStore();
        var pendingCodeStore = new FakePendingDiscordLinkCodeStore();
        pendingCodeStore.SaveCode("ABC123", new PendingDiscordLinkCodeRecord("player-1", DateTime.UtcNow.AddMinutes(10).Ticks));
        var itemGiver = new FakeDiscordLinkRewardItemGiver();
        var rewardStateStore = new FakeDiscordLinkRewardStateStore();
        var player = CreatePlayer("player-1", "Ava");
        var playerLookup = new FakePlayerLookup(player);
        var messenger = new FakePlayerMessenger();
        var poller = CreatePoller(webhookClient, lastMessageStore, linkedAccountStore, pendingCodeStore, rewardStateStore, itemGiver, playerLookup, messenger);

        await InvokePollOnceAsync(poller);

        Assert.Equal("discord-1", linkedAccountStore.GetLinkedDiscordUserId("player-1"));
        Assert.Equal(1, itemGiver.GiveCount);
        Assert.True(rewardStateStore.HasClaimed("player-1"));
        Assert.Contains("You received 10 rusty gears.", messenger.LastDualMessage);
    }

    [Fact]
    public async Task PollOnceAsync_WhenLinkedPlayerIsOffline_QueuesRewardForNextJoin()
    {
        var webhookClient = new FakeDiscordWebhookClient(
            """
            [
              { "id": "101", "content": "ABC123", "author": { "id": "discord-1" } }
            ]
            """);
        var lastMessageStore = new FakeDiscordLinkLastMessageStore("100");
        var linkedAccountStore = new FakeLinkedAccountStore();
        var pendingCodeStore = new FakePendingDiscordLinkCodeStore();
        pendingCodeStore.SaveCode("ABC123", new PendingDiscordLinkCodeRecord("player-1", DateTime.UtcNow.AddMinutes(10).Ticks));
        var itemGiver = new FakeDiscordLinkRewardItemGiver();
        var rewardStateStore = new FakeDiscordLinkRewardStateStore();
        var poller = CreatePoller(
            webhookClient,
            lastMessageStore,
            linkedAccountStore,
            pendingCodeStore,
            rewardStateStore,
            itemGiver,
            new FakePlayerLookup(null),
            new FakePlayerMessenger());

        await InvokePollOnceAsync(poller);

        Assert.Equal(0, itemGiver.GiveCount);
        Assert.True(rewardStateStore.HasPendingReward("player-1"));
        Assert.False(rewardStateStore.HasClaimed("player-1"));
    }

    [Fact]
    public async Task PollOnceAsync_WhenRewardAlreadyClaimed_DoesNotGrantSecondTime()
    {
        var webhookClient = new FakeDiscordWebhookClient(
            """
            [
              { "id": "101", "content": "ABC123", "author": { "id": "discord-1" } }
            ]
            """);
        var lastMessageStore = new FakeDiscordLinkLastMessageStore("100");
        var linkedAccountStore = new FakeLinkedAccountStore();
        var pendingCodeStore = new FakePendingDiscordLinkCodeStore();
        pendingCodeStore.SaveCode("ABC123", new PendingDiscordLinkCodeRecord("player-1", DateTime.UtcNow.AddMinutes(10).Ticks));
        var itemGiver = new FakeDiscordLinkRewardItemGiver();
        var rewardStateStore = new FakeDiscordLinkRewardStateStore();
        rewardStateStore.MarkClaimed("player-1");
        var poller = CreatePoller(
            webhookClient,
            lastMessageStore,
            linkedAccountStore,
            pendingCodeStore,
            rewardStateStore,
            itemGiver,
            new FakePlayerLookup(CreatePlayer("player-1", "Ava")),
            new FakePlayerMessenger());

        await InvokePollOnceAsync(poller);

        Assert.Equal(0, itemGiver.GiveCount);
        Assert.True(rewardStateStore.HasClaimed("player-1"));
    }

    private static DiscordLinkPoller CreatePoller(
        FakeDiscordWebhookClient webhookClient,
        FakeDiscordLinkLastMessageStore lastMessageStore,
        FakeLinkedAccountStore? linkedAccountStore = null,
        FakePendingDiscordLinkCodeStore? pendingCodeStore = null,
        FakeDiscordLinkRewardStateStore? rewardStateStore = null,
        FakeDiscordLinkRewardItemGiver? rewardItemGiver = null,
        FakePlayerLookup? playerLookup = null,
        FakePlayerMessenger? messenger = null)
    {
        linkedAccountStore ??= new FakeLinkedAccountStore();
        pendingCodeStore ??= new FakePendingDiscordLinkCodeStore();
        rewardStateStore ??= new FakeDiscordLinkRewardStateStore();
        rewardItemGiver ??= new FakeDiscordLinkRewardItemGiver();
        playerLookup ??= new FakePlayerLookup(null);
        messenger ??= new FakePlayerMessenger();

        return new DiscordLinkPoller(
            null!,
            new DiscordBridgeConfig
            {
                BotToken = "token",
                LinkChannelId = "channel",
                PollMs = 1000
            },
            webhookClient,
            lastMessageStore,
            new DiscordLinkService(
                linkedAccountStore,
                pendingCodeStore,
                new DiscordLinkCodeMessageParser(),
                15),
            new DiscordLinkRewardService(rewardStateStore, rewardItemGiver),
            new DiscordLinkCodeMessageParser(),
            playerLookup,
            new PlayerDonatorRoleSyncService(
                null!,
                new DiscordBridgeConfig
                {
                    EnableRoleSync = true,
                    BotToken = "token",
                    GuildId = "guild"
                },
                linkedAccountStore,
                new FakeDiscordMemberRoleClient(),
                new DiscordRoleNameResolver(),
                new DiscordDonatorRolePlanner(new DonatorPrivilegeCatalog()),
                new FakePlayerRoleCodeReader(),
                new FakePlayerRoleAssigner(),
                new FakePlayerDefaultRoleResetter(),
                messenger),
            messenger);
    }

    private static Task InvokePollOnceAsync(DiscordLinkPoller poller)
    {
        MethodInfo method = typeof(DiscordLinkPoller).GetMethod("PollOnceAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)method.Invoke(poller, null)!;
    }

    private sealed class FakeDiscordWebhookClient : IDiscordWebhookClient
    {
        private readonly string responseBody;

        public FakeDiscordWebhookClient(string responseBody)
        {
            this.responseBody = responseBody;
        }

        public int GetCallCount { get; private set; }

        public int PostBotJsonCallCount { get; private set; }

        public Task PostJsonAsync(string url, string json)
        {
            return Task.CompletedTask;
        }

        public Task<DiscordHttpResponse> PostBotJsonAsync(string url, string botToken, string json)
        {
            PostBotJsonCallCount++;
            return Task.FromResult(new DiscordHttpResponse
            {
                StatusCode = 200,
                Body = "{}"
            });
        }

        public Task<DiscordHttpResponse> GetAsync(string url, string botToken)
        {
            GetCallCount++;
            return Task.FromResult(new DiscordHttpResponse
            {
                StatusCode = 200,
                Body = responseBody
            });
        }
    }

    private sealed class FakeDiscordLinkLastMessageStore : IDiscordLinkLastMessageStore
    {
        private readonly string? initialLastMessageId;

        public FakeDiscordLinkLastMessageStore(string? initialLastMessageId = null)
        {
            this.initialLastMessageId = initialLastMessageId;
        }

        public string? SavedLastMessageId { get; private set; }

        public string Load()
        {
            return initialLastMessageId;
        }

        public void Save(string lastMessageId)
        {
            SavedLastMessageId = lastMessageId;
        }

        public void Clear()
        {
            SavedLastMessageId = null;
        }
    }

    private sealed class FakeLinkedAccountStore : IDiscordLinkedAccountStore
    {
        private readonly Dictionary<string, string> links = new(StringComparer.OrdinalIgnoreCase);

        public string GetLinkedDiscordUserId(string playerUid)
        {
            return links.TryGetValue(playerUid, out string discordUserId) ? discordUserId : null;
        }

        public IReadOnlyDictionary<string, string> GetAllLinkedDiscordUserIds()
        {
            return links;
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

        public IReadOnlyDictionary<string, PendingDiscordLinkCodeRecord> GetPendingCodeRecords(DateTime nowUtc)
        {
            RemoveExpired(nowUtc);
            return pendingCodes;
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

    private sealed class FakePlayerLookup : IPlayerLookup
    {
        private readonly IServerPlayer? player;

        public FakePlayerLookup(IServerPlayer? player)
        {
            this.player = player;
        }

        public IServerPlayer FindOnlinePlayerByUid(string uid)
        {
            return player != null && string.Equals(player.PlayerUID, uid, StringComparison.OrdinalIgnoreCase)
                ? player
                : null;
        }

        public IServerPlayer FindOnlinePlayerByName(string name)
        {
            return null;
        }
    }

    private sealed class FakeDiscordMemberRoleClient : IDiscordMemberRoleClient
    {
        public Task<DiscordMemberRoles> GetMemberRolesAsync(DiscordBridgeConfig config, string discordUserId)
        {
            return Task.FromResult(new DiscordMemberRoles(Array.Empty<string>(), Array.Empty<DiscordGuildRole>()));
        }
    }

    private sealed class FakePlayerRoleCodeReader : IPlayerRoleCodeReader
    {
        public string Read(IServerPlayer player)
        {
            return null;
        }
    }

    private sealed class FakePlayerRoleAssigner : IPlayerRoleAssigner
    {
        public void Assign(IServerPlayer player, string roleCode)
        {
        }
    }

    private sealed class FakePlayerDefaultRoleResetter : IPlayerDefaultRoleResetter
    {
        public void Reset(IServerPlayer player)
        {
        }

        public string GetDefaultRoleCode()
        {
            return "default";
        }
    }

    private sealed class FakePlayerMessenger : IPlayerMessenger
    {
        public string? LastDualMessage { get; private set; }

        public void SendInfo(IServerPlayer player, string message, int groupId, int chatType)
        {
        }

        public void SendGeneral(IServerPlayer player, string message, int groupId, int chatType)
        {
        }

        public void SendDual(IServerPlayer player, string message, int infoChatType, int generalChatType)
        {
            LastDualMessage = message;
        }

        public void SendDual(IServerPlayer player, string message, int infoGroupId, int infoChatType, int generalGroupId, int generalChatType)
        {
            LastDualMessage = message;
        }

        public void SendIngameError(IServerPlayer player, string code, string message)
        {
        }
    }

    private sealed class FakeDiscordLinkRewardStateStore : IDiscordLinkRewardStateStore
    {
        private readonly HashSet<string> claimed = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> pending = new(StringComparer.OrdinalIgnoreCase);

        public bool HasClaimed(string playerUid)
        {
            return claimed.Contains(playerUid);
        }

        public void MarkClaimed(string playerUid)
        {
            claimed.Add(playerUid);
        }

        public void ClearClaimed(string playerUid)
        {
            claimed.Remove(playerUid);
        }

        public bool HasPendingReward(string playerUid)
        {
            return pending.Contains(playerUid);
        }

        public void MarkPendingReward(string playerUid)
        {
            pending.Add(playerUid);
        }

        public void ClearPendingReward(string playerUid)
        {
            pending.Remove(playerUid);
        }

        public IReadOnlyCollection<string> GetClaimedPlayerUids()
        {
            return claimed.ToArray();
        }

        public IReadOnlyCollection<string> GetPendingRewardPlayerUids()
        {
            return pending.ToArray();
        }
    }

    private sealed class FakeDiscordLinkRewardItemGiver : IDiscordLinkRewardItemGiver
    {
        public int GiveCount { get; private set; }

        public void Give(IServerPlayer player)
        {
            GiveCount++;
        }
    }

    private static IServerPlayer CreatePlayer(string playerUid, string playerName)
    {
        var proxy = DispatchProxy.Create<IServerPlayer, TestServerPlayerProxy>();
        ((TestServerPlayerProxy)(object)proxy).Values["get_PlayerUID"] = playerUid;
        ((TestServerPlayerProxy)(object)proxy).Values["get_PlayerName"] = playerName;
        return proxy;
    }

    private class TestServerPlayerProxy : DispatchProxy
    {
        public Dictionary<string, object> Values { get; } = new(StringComparer.Ordinal);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null)
            {
                return null;
            }

            if (Values.TryGetValue(targetMethod.Name, out object value))
            {
                return value;
            }

            if (targetMethod.ReturnType == typeof(void))
            {
                return null;
            }

            return targetMethod.ReturnType.IsValueType
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
