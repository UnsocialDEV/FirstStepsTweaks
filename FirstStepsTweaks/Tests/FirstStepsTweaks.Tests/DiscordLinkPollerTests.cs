using System.Reflection;
using System.Text.Json;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Discord.Transport;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
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

    [Fact]
    public async Task PollOnceAsync_WhenSavedCursorHasFullPageAndPartialSecondPage_ProcessesBothPages()
    {
        var statusTracker = new DiscordLinkPollerStatusTracker();
        var (api, logger) = CreateApiWithLogger();
        var webhookClient = new FakeDiscordWebhookClient(
            new DiscordHttpResponse
            {
                StatusCode = 200,
                Body = CreateMessagePageJson(Enumerable.Range(101, 100).Reverse().Select(index => (index.ToString(), index == 150 ? "ABC123" : "ignore", "discord-1", false)))
            },
            new DiscordHttpResponse
            {
                StatusCode = 200,
                Body = CreateMessagePageJson(new[]
                {
                    ("201", "ignore", "discord-1", false),
                    ("200", "ignore", "discord-1", false),
                    ("199", "ignore", "discord-1", false)
                })
            });
        var lastMessageStore = new FakeDiscordLinkLastMessageStore("100");
        var linkedAccountStore = new FakeLinkedAccountStore();
        var pendingCodeStore = new FakePendingDiscordLinkCodeStore();
        pendingCodeStore.SaveCode("ABC123", new PendingDiscordLinkCodeRecord("player-1", DateTime.UtcNow.AddMinutes(10).Ticks));
        var poller = CreatePoller(
            webhookClient,
            lastMessageStore,
            linkedAccountStore,
            pendingCodeStore,
            new FakeDiscordLinkRewardStateStore(),
            new FakeDiscordLinkRewardItemGiver(),
            new FakePlayerLookup(CreatePlayer("player-1", "Ava")),
            new FakePlayerMessenger(),
            api: api,
            statusTracker: statusTracker);

        await InvokePollOnceAsync(poller);

        Assert.Equal("201", lastMessageStore.SavedLastMessageId);
        Assert.Equal("discord-1", linkedAccountStore.GetLinkedDiscordUserId("player-1"));
        Assert.DoesNotContain(logger.WarningMessages, message => message.Contains("fast-forwarded stale backlog"));
        Assert.Equal(2, statusTracker.Capture().LastProcessedPageCount);
        Assert.Equal(103, statusTracker.Capture().LastProcessedMessageCount);
        Assert.False(statusTracker.Capture().LastPollReachedProcessingCap);
    }

    [Fact]
    public async Task PollOnceAsync_WhenSavedCursorHasMultipleFullPages_PreservesOldestToNewestOrderAcrossPages()
    {
        var webhookClient = new FakeDiscordWebhookClient(
            new DiscordHttpResponse
            {
                StatusCode = 200,
                Body = CreateMessagePageJson(Enumerable.Range(101, 100).Reverse().Select(index => (index.ToString(), index == 105 ? "ABC123" : index == 150 ? "DEF456" : "ignore", "discord-1", false)))
            },
            new DiscordHttpResponse
            {
                StatusCode = 200,
                Body = CreateMessagePageJson(Enumerable.Range(201, 100).Reverse().Select(index => (index.ToString(), index == 205 ? "GHI789" : "ignore", "discord-1", false)))
            },
            new DiscordHttpResponse
            {
                StatusCode = 200,
                Body = "[]"
            });
        var lastMessageStore = new FakeDiscordLinkLastMessageStore("100");
        var linkedAccountStore = new FakeLinkedAccountStore();
        var pendingCodeStore = new FakePendingDiscordLinkCodeStore();
        pendingCodeStore.SaveCode("ABC123", new PendingDiscordLinkCodeRecord("player-1", DateTime.UtcNow.AddMinutes(10).Ticks));
        pendingCodeStore.SaveCode("DEF456", new PendingDiscordLinkCodeRecord("player-2", DateTime.UtcNow.AddMinutes(10).Ticks));
        pendingCodeStore.SaveCode("GHI789", new PendingDiscordLinkCodeRecord("player-3", DateTime.UtcNow.AddMinutes(10).Ticks));
        var poller = CreatePoller(webhookClient, lastMessageStore, linkedAccountStore, pendingCodeStore);

        await InvokePollOnceAsync(poller);

        Assert.Equal("discord-1", linkedAccountStore.GetLinkedDiscordUserId("player-1"));
        Assert.Equal("discord-1", linkedAccountStore.GetLinkedDiscordUserId("player-2"));
        Assert.Equal("discord-1", linkedAccountStore.GetLinkedDiscordUserId("player-3"));
        Assert.Equal(new[]
        {
            "https://discord.com/api/v10/channels/channel/messages?after=100&limit=100",
            "https://discord.com/api/v10/channels/channel/messages?after=200&limit=100",
            "https://discord.com/api/v10/channels/channel/messages?after=300&limit=100"
        }, webhookClient.RequestedUrls);
        Assert.Equal("300", lastMessageStore.SavedLastMessageId);
    }

    [Fact]
    public async Task PollOnceAsync_WhenMessageContainsEmbeddedCode_IgnoresMessageAndDoesNotReply()
    {
        var webhookClient = new FakeDiscordWebhookClient(
            """
            [
              { "id": "101", "content": "Please link ABC123", "author": { "id": "discord-1" } }
            ]
            """);
        var lastMessageStore = new FakeDiscordLinkLastMessageStore("100");
        var linkedAccountStore = new FakeLinkedAccountStore();
        var pendingCodeStore = new FakePendingDiscordLinkCodeStore();
        pendingCodeStore.SaveCode("ABC123", new PendingDiscordLinkCodeRecord("player-1", DateTime.UtcNow.AddMinutes(10).Ticks));
        var poller = CreatePoller(webhookClient, lastMessageStore, linkedAccountStore, pendingCodeStore);

        await InvokePollOnceAsync(poller);

        Assert.Null(linkedAccountStore.GetLinkedDiscordUserId("player-1"));
        Assert.Equal(0, webhookClient.PostBotJsonCallCount);
        Assert.Equal("101", lastMessageStore.SavedLastMessageId);
    }

    [Fact]
    public async Task PollOnceAsync_WhenFailureRepeats_SuppressesDuplicateWarningUntilSuccessClearsStreak()
    {
        var statusTracker = new DiscordLinkPollerStatusTracker();
        var (api, logger) = CreateApiWithLogger();
        var webhookClient = new FakeDiscordWebhookClient(
            new DiscordHttpResponse { StatusCode = 401, Body = "{}" },
            new DiscordHttpResponse { StatusCode = 401, Body = "{}" },
            new DiscordHttpResponse { StatusCode = 200, Body = "[]" },
            new DiscordHttpResponse { StatusCode = 401, Body = "{}" });
        var poller = CreatePoller(
            webhookClient,
            new FakeDiscordLinkLastMessageStore("100"),
            api: api,
            statusTracker: statusTracker);

        await InvokePollOnceAsync(poller);
        await InvokePollOnceAsync(poller);

        Assert.Single(logger.WarningMessages);
        Assert.Equal("Discord link poll returned 401 Unauthorized. Check the configured bot token.", statusTracker.Capture().LastFailureSummary);

        await InvokePollOnceAsync(poller);

        Assert.Equal(string.Empty, statusTracker.Capture().LastFailureSummary);
        Assert.True(statusTracker.Capture().LastSuccessfulPollUtc.HasValue);

        await InvokePollOnceAsync(poller);

        Assert.Equal(2, logger.WarningMessages.Count);
    }

    [Fact]
    public async Task PollOnceAsync_WhenBacklogHitsPerPollCap_ResumesOnNextTickWithoutDroppingMessages()
    {
        var statusTracker = new DiscordLinkPollerStatusTracker();
        var (api, logger) = CreateApiWithLogger();
        var webhookClient = new FakeDiscordWebhookClient(
            new DiscordHttpResponse
            {
                StatusCode = 200,
                Body = CreateMessagePageJson(Enumerable.Range(101, 100).Reverse().Select(index => (index.ToString(), index == 105 ? "ABC123" : "ignore", "discord-1", false)))
            },
            new DiscordHttpResponse
            {
                StatusCode = 200,
                Body = CreateMessagePageJson(Enumerable.Range(201, 100).Reverse().Select(index => (index.ToString(), index == 205 ? "DEF456" : "ignore", "discord-1", false)))
            },
            new DiscordHttpResponse
            {
                StatusCode = 200,
                Body = CreateMessagePageJson(new[]
                {
                    ("301", "GHI789", "discord-1", false),
                    ("300", "ignore", "discord-1", false)
                })
            },
            new DiscordHttpResponse
            {
                StatusCode = 200,
                Body = "[]"
            });
        var lastMessageStore = new FakeDiscordLinkLastMessageStore("100");
        var linkedAccountStore = new FakeLinkedAccountStore();
        var pendingCodeStore = new FakePendingDiscordLinkCodeStore();
        pendingCodeStore.SaveCode("ABC123", new PendingDiscordLinkCodeRecord("player-1", DateTime.UtcNow.AddMinutes(10).Ticks));
        pendingCodeStore.SaveCode("DEF456", new PendingDiscordLinkCodeRecord("player-2", DateTime.UtcNow.AddMinutes(10).Ticks));
        pendingCodeStore.SaveCode("GHI789", new PendingDiscordLinkCodeRecord("player-3", DateTime.UtcNow.AddMinutes(10).Ticks));
        var poller = CreatePoller(
            webhookClient,
            lastMessageStore,
            linkedAccountStore,
            pendingCodeStore,
            api: api,
            statusTracker: statusTracker,
            maxPagesPerPoll: 2);

        await InvokePollOnceAsync(poller);

        Assert.Equal("300", lastMessageStore.SavedLastMessageId);
        Assert.Equal("discord-1", linkedAccountStore.GetLinkedDiscordUserId("player-1"));
        Assert.Equal("discord-1", linkedAccountStore.GetLinkedDiscordUserId("player-2"));
        Assert.Null(linkedAccountStore.GetLinkedDiscordUserId("player-3"));
        Assert.True(statusTracker.Capture().LastPollReachedProcessingCap);
        Assert.Equal(2, statusTracker.Capture().LastProcessedPageCount);
        Assert.Single(logger.WarningMessages);
        Assert.Contains("processing cap of 2 pages", logger.WarningMessages[0]);

        await InvokePollOnceAsync(poller);

        Assert.Equal("discord-1", linkedAccountStore.GetLinkedDiscordUserId("player-3"));
        Assert.Equal("301", lastMessageStore.SavedLastMessageId);
        Assert.False(statusTracker.Capture().LastPollReachedProcessingCap);
        Assert.Equal(1, logger.WarningMessages.Count);
    }

    [Fact]
    public async Task PollOnceAsync_WhenDiscordRequestTimesOut_LogsTimeoutFailure()
    {
        var statusTracker = new DiscordLinkPollerStatusTracker();
        var (api, logger) = CreateApiWithLogger();
        var webhookClient = new FakeDiscordWebhookClient();
        webhookClient.ExceptionToThrowOnGet = new TaskCanceledException();
        var poller = CreatePoller(
            webhookClient,
            new FakeDiscordLinkLastMessageStore("100"),
            api: api,
            statusTracker: statusTracker);

        await InvokePollOnceAsync(poller);

        Assert.Single(logger.WarningMessages);
        Assert.Contains("timed out after 10 seconds", logger.WarningMessages[0]);
        Assert.Equal(
            "Discord link poll request timed out after 10 seconds.",
            statusTracker.Capture().LastFailureSummary);
    }

    private static DiscordLinkPoller CreatePoller(
        FakeDiscordWebhookClient webhookClient,
        FakeDiscordLinkLastMessageStore lastMessageStore,
        FakeLinkedAccountStore? linkedAccountStore = null,
        FakePendingDiscordLinkCodeStore? pendingCodeStore = null,
        FakeDiscordLinkRewardStateStore? rewardStateStore = null,
        FakeDiscordLinkRewardItemGiver? rewardItemGiver = null,
        FakePlayerLookup? playerLookup = null,
        FakePlayerMessenger? messenger = null,
        FakeDiscordMemberRoleClient? memberRoleClient = null,
        ICoreServerAPI? api = null,
        DiscordLinkPollerStatusTracker? statusTracker = null,
        int maxPagesPerPoll = 10)
    {
        linkedAccountStore ??= new FakeLinkedAccountStore();
        pendingCodeStore ??= new FakePendingDiscordLinkCodeStore();
        rewardStateStore ??= new FakeDiscordLinkRewardStateStore();
        rewardItemGiver ??= new FakeDiscordLinkRewardItemGiver();
        playerLookup ??= new FakePlayerLookup(null);
        messenger ??= new FakePlayerMessenger();
        memberRoleClient ??= new FakeDiscordMemberRoleClient();
        statusTracker ??= new DiscordLinkPollerStatusTracker();

        return new DiscordLinkPoller(
            api!,
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
                api!,
                new DiscordBridgeConfig
                {
                    EnableRoleSync = true,
                    BotToken = "token",
                    GuildId = "guild"
                },
                linkedAccountStore,
                memberRoleClient,
                new DiscordRoleNameResolver(),
                new DiscordDonatorRolePlanner(new DonatorTierCatalog()),
                new DonatorRoleTransitionApplier(
                    api!,
                    new PlayerRoleCodeReader(),
                    new FakePlayerRoleAssigner(),
                    new FakePlayerDefaultRoleResetter()),
                new LegacyDonatorPrivilegeCleaner(
                    new FakePlayerPrivilegeReader(),
                    new FakePlayerPrivilegeMutator(),
                    new DonatorTierCatalog()),
                new AdminModePriorRoleUpdater(api!, new FakeAdminModeStore()),
                messenger),
            messenger,
            statusTracker,
            maxPagesPerPoll);
    }

    private static Task InvokePollOnceAsync(DiscordLinkPoller poller)
    {
        MethodInfo method = typeof(DiscordLinkPoller).GetMethod("PollOnceAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)method.Invoke(poller, null)!;
    }

    private sealed class FakeDiscordWebhookClient : IDiscordWebhookClient
    {
        private readonly Queue<DiscordHttpResponse> responses;

        public FakeDiscordWebhookClient(string responseBody)
            : this(new DiscordHttpResponse
            {
                StatusCode = 200,
                Body = responseBody
            })
        {
        }

        public FakeDiscordWebhookClient(params DiscordHttpResponse[] responses)
        {
            this.responses = new Queue<DiscordHttpResponse>(responses);
        }

        public int GetCallCount { get; private set; }

        public int PostBotJsonCallCount { get; private set; }

        public List<string> RequestedUrls { get; } = new();

        public Exception? ExceptionToThrowOnGet { get; set; }

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
            RequestedUrls.Add(url);

            if (ExceptionToThrowOnGet != null)
            {
                throw ExceptionToThrowOnGet;
            }

            if (responses.Count == 0)
            {
                return Task.FromResult(new DiscordHttpResponse
                {
                    StatusCode = 200,
                    Body = "[]"
                });
            }

            return Task.FromResult(responses.Dequeue());
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
        private readonly DiscordMemberRoles memberRoles;

        public FakeDiscordMemberRoleClient()
            : this(new DiscordMemberRoles(Array.Empty<string>(), Array.Empty<DiscordGuildRole>()))
        {
        }

        public FakeDiscordMemberRoleClient(DiscordMemberRoles memberRoles)
        {
            this.memberRoles = memberRoles;
        }

        public Task<DiscordMemberRoles> GetMemberRolesAsync(DiscordBridgeConfig config, string discordUserId)
        {
            return Task.FromResult(memberRoles);
        }
    }

    private sealed class FakePlayerPrivilegeReader : IPlayerPrivilegeReader
    {
        public bool HasPrivilege(IServerPlayer player, string privilege)
        {
            return false;
        }
    }

    private sealed class FakePlayerPrivilegeMutator : IPlayerPrivilegeMutator
    {
        public void Grant(IServerPlayer player, string privilege)
        {
        }

        public void Revoke(IServerPlayer player, string privilege)
        {
        }
    }

    private sealed class FakePlayerRoleAssigner : IPlayerRoleAssigner
    {
        public void Assign(IServerPlayer player, string roleCode)
        {
            ((TestServerPlayerProxy)(object)player).RoleCode = roleCode;
        }
    }

    private sealed class FakePlayerDefaultRoleResetter : IPlayerDefaultRoleResetter
    {
        public void Reset(IServerPlayer player)
        {
            ((TestServerPlayerProxy)(object)player).RoleCode = GetDefaultRoleCode();
        }

        public string GetDefaultRoleCode()
        {
            return "suplayer";
        }
    }

    private sealed class FakeAdminModeStore : IAdminModeStore
    {
        public bool IsActive(IServerPlayer player)
        {
            return false;
        }

        public bool TryLoad(IServerPlayer player, out AdminModeState state, out string errorMessage)
        {
            state = null!;
            errorMessage = string.Empty;
            return false;
        }

        public void Save(IServerPlayer player, AdminModeState state)
        {
        }

        public void Clear(IServerPlayer player)
        {
        }
    }

    private sealed class FakePlayerMessenger : IPlayerMessenger
    {
        public string? LastDualMessage { get; private set; }

        public string? LastInfoMessage { get; private set; }

        public void SendInfo(IServerPlayer player, string message, int groupId, int chatType)
        {
            LastInfoMessage = message;
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
        ((TestServerPlayerProxy)(object)proxy).Values["get_RoleCode"] = "suplayer";
        return proxy;
    }

    private static string CreateMessagePageJson(IEnumerable<(string Id, string Content, string AuthorId, bool IsBot)> messages)
    {
        return JsonSerializer.Serialize(messages.Select(message => new
        {
            id = message.Id,
            content = message.Content,
            author = new
            {
                id = message.AuthorId,
                bot = message.IsBot
            }
        }));
    }

    private static (ICoreServerAPI Api, TestLoggerProxy Logger) CreateApiWithLogger()
    {
        ILogger logger = DispatchProxy.Create<ILogger, TestLoggerProxy>();
        var loggerProxy = (TestLoggerProxy)(object)logger;
        ICoreServerAPI api = DispatchProxy.Create<ICoreServerAPI, TestCoreServerApiProxy>();
        ((TestCoreServerApiProxy)(object)api).Logger = logger;
        return (api, loggerProxy);
    }

    private class TestServerPlayerProxy : DispatchProxy
    {
        public Dictionary<string, object> Values { get; } = new(StringComparer.Ordinal);

        public string RoleCode
        {
            get => Values.TryGetValue("get_RoleCode", out object? value) ? value?.ToString() ?? string.Empty : string.Empty;
            set => Values["get_RoleCode"] = value;
        }

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

    private class TestCoreServerApiProxy : DispatchProxy
    {
        public ILogger? Logger { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_Logger" => Logger,
                _ => targetMethod?.ReturnType.IsValueType == true
                    ? Activator.CreateInstance(targetMethod.ReturnType)
                    : null
            };
        }
    }

    private class TestLoggerProxy : DispatchProxy
    {
        public List<string> WarningMessages { get; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Warning")
            {
                WarningMessages.Add(args?.Length > 0 && args[0] != null ? args[0]!.ToString()! : string.Empty);
                return null;
            }

            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
