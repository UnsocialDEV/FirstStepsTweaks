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

    private static DiscordLinkPoller CreatePoller(
        FakeDiscordWebhookClient webhookClient,
        FakeDiscordLinkLastMessageStore lastMessageStore)
    {
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
                new FakeLinkedAccountStore(),
                new FakePendingDiscordLinkCodeStore(),
                new DiscordLinkCodeMessageParser(),
                15),
            new DiscordLinkCodeMessageParser(),
            new FakePlayerLookup(),
            new PlayerDonatorRoleSyncService(
                null!,
                new DiscordBridgeConfig
                {
                    EnableRoleSync = true,
                    BotToken = "token",
                    GuildId = "guild"
                },
                new FakeLinkedAccountStore(),
                new FakeDiscordMemberRoleClient(),
                new DiscordRoleNameResolver(),
                new DiscordDonatorRolePlanner(new DonatorPrivilegeCatalog()),
                new FakePlayerRoleCodeReader(),
                new FakePlayerRoleAssigner(),
                new FakePlayerDefaultRoleResetter(),
                new FakePlayerMessenger()),
            new FakePlayerMessenger());
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
        public string? SavedLastMessageId { get; private set; }

        public string Load()
        {
            return null;
        }

        public void Save(string lastMessageId)
        {
            SavedLastMessageId = lastMessageId;
        }
    }

    private sealed class FakeLinkedAccountStore : IDiscordLinkedAccountStore
    {
        public string GetLinkedDiscordUserId(string playerUid)
        {
            return null;
        }

        public void SetLinkedDiscordUserId(string playerUid, string discordUserId)
        {
        }

        public void ClearLinkedDiscordUserId(string playerUid)
        {
        }
    }

    private sealed class FakePendingDiscordLinkCodeStore : IPendingDiscordLinkCodeStore
    {
        public IReadOnlyCollection<string> GetPendingCodes(DateTime nowUtc)
        {
            return Array.Empty<string>();
        }

        public bool TryGetCode(string code, DateTime nowUtc, out PendingDiscordLinkCodeRecord record)
        {
            record = null;
            return false;
        }

        public void SaveCode(string code, PendingDiscordLinkCodeRecord record)
        {
        }

        public void RemoveCode(string code)
        {
        }

        public void RemoveCodesForPlayer(string playerUid)
        {
        }
    }

    private sealed class FakePlayerLookup : IPlayerLookup
    {
        public IServerPlayer FindOnlinePlayerByUid(string uid)
        {
            return null;
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
        public void SendInfo(IServerPlayer player, string message, int groupId, int chatType)
        {
        }

        public void SendGeneral(IServerPlayer player, string message, int groupId, int chatType)
        {
        }

        public void SendDual(IServerPlayer player, string message, int infoChatType, int generalChatType)
        {
        }

        public void SendDual(IServerPlayer player, string message, int infoGroupId, int infoChatType, int generalGroupId, int generalChatType)
        {
        }

        public void SendIngameError(IServerPlayer player, string code, string message)
        {
        }
    }
}
