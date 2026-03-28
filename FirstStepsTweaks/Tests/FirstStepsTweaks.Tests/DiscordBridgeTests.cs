using System.Reflection;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Discord.Messaging;
using FirstStepsTweaks.Discord.Transport;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class DiscordBridgeTests
{
    [Fact]
    public async Task CheckDiscordMessagesOnce_WhenBacklogExceedsOnePage_RelaysEveryMessageAcrossPages()
    {
        var api = DispatchProxy.Create<ICoreServerAPI, TestCoreServerApiProxy>();
        var apiProxy = (TestCoreServerApiProxy)(object)api;
        var webhookClient = new FakeDiscordWebhookClient(
            CreateMessagePageJson(250, 151),
            CreateMessagePageJson(150, 101));
        var lastMessageStore = new FakeDiscordLastMessageStore("100");
        var bridge = new DiscordBridge(
            api,
            new DiscordBridgeConfig
            {
                BotToken = "token",
                ChannelId = "channel",
                RelayDiscordToGame = true
            },
            lastMessageStore,
            new DiscordMessageTranslator(),
            webhookClient,
            null,
            new DiscordRelayMessageNormalizer(),
            new DiscordRelayConfigurationValidator());

        await InvokeCheckDiscordMessagesOnceAsync(bridge);

        Assert.Equal(150, apiProxy.SentMessages.Count);
        Assert.Equal("[Discord] User101: msg101", apiProxy.SentMessages[0]);
        Assert.Equal("[Discord] User250: msg250", apiProxy.SentMessages[^1]);
        Assert.Equal("250", lastMessageStore.SavedLastMessageId);
        Assert.Equal(2, webhookClient.GetCallCount);
        Assert.Contains("messages?limit=100", webhookClient.RequestUrls[0]);
        Assert.Contains("messages?before=151&limit=100", webhookClient.RequestUrls[1]);
    }

    private static Task InvokeCheckDiscordMessagesOnceAsync(DiscordBridge bridge)
    {
        MethodInfo method = typeof(DiscordBridge).GetMethod("CheckDiscordMessagesOnce", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)method.Invoke(bridge, null)!;
    }

    private static string CreateMessagePageJson(int newestId, int oldestId)
    {
        return "[" + string.Join(
            ",",
            Enumerable.Range(oldestId, newestId - oldestId + 1)
                .Reverse()
                .Select(id => $"{{\"id\":\"{id}\",\"content\":\"msg{id}\",\"author\":{{\"username\":\"User{id}\"}}}}")) + "]";
    }

    private sealed class FakeDiscordWebhookClient : IDiscordWebhookClient
    {
        private readonly Queue<string> responseBodies;

        public FakeDiscordWebhookClient(params string[] responseBodies)
        {
            this.responseBodies = new Queue<string>(responseBodies);
        }

        public List<string> RequestUrls { get; } = new();

        public int GetCallCount { get; private set; }

        public Task PostJsonAsync(string url, string json)
        {
            return Task.CompletedTask;
        }

        public Task<DiscordHttpResponse> PostBotJsonAsync(string url, string botToken, string json)
        {
            return Task.FromResult(new DiscordHttpResponse
            {
                StatusCode = 200,
                Body = "{}"
            });
        }

        public Task<DiscordHttpResponse> GetAsync(string url, string botToken)
        {
            GetCallCount++;
            RequestUrls.Add(url);

            return Task.FromResult(new DiscordHttpResponse
            {
                StatusCode = 200,
                Body = responseBodies.Dequeue()
            });
        }
    }

    private sealed class FakeDiscordLastMessageStore : IDiscordLastMessageStore
    {
        private readonly string initialLastMessageId;

        public FakeDiscordLastMessageStore(string initialLastMessageId)
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
    }

    private class TestCoreServerApiProxy : DispatchProxy
    {
        public List<string> SentMessages { get; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "SendMessageToGroup")
            {
                SentMessages.Add((string)args![1]!);
                return null;
            }

            Type? returnType = targetMethod?.ReturnType;
            if (returnType == null || returnType == typeof(void))
            {
                return null;
            }

            if (returnType.IsValueType)
            {
                return Activator.CreateInstance(returnType);
            }

            return null;
        }
    }
}
