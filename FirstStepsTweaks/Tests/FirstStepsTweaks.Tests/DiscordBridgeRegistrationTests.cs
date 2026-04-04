using System;
using System.Reflection;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Discord.Messaging;
using FirstStepsTweaks.Discord.Transport;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class DiscordBridgeRegistrationTests
{
    [Fact]
    public void Register_IsIdempotent_AndAddsDiscordHooksOnce()
    {
        var eventApi = DispatchProxy.Create<IServerEventAPI, TestServerEventApiProxy>();
        var eventProxy = (TestServerEventApiProxy)(object)eventApi;
        var api = DispatchProxy.Create<ICoreServerAPI, TestCoreServerApiProxy>();
        var apiProxy = (TestCoreServerApiProxy)(object)api;
        apiProxy.Event = eventApi;

        var bridge = new DiscordBridge(
            api,
            new DiscordBridgeConfig
            {
                BotToken = "token",
                ChannelId = "channel",
                WebhookUrl = "https://example.invalid/webhook",
                RelayDiscordToGame = true,
                RelayGameToDiscord = true,
                RelayWorldUpdates = true,
                PollMs = 1000,
                WorldUpdatePollMs = 2000
            },
            new FakeDiscordLastMessageStore(),
            new DiscordMessageTranslator(),
            new FakeDiscordWebhookClient(),
            null!,
            new DiscordRelayMessageNormalizer(),
            new DiscordRelayConfigurationValidator());

        bridge.Register();
        bridge.Register();

        Assert.Equal(1, eventProxy.PlayerChatAddedCount);
        Assert.Equal(1, eventProxy.PlayerJoinAddedCount);
        Assert.Equal(1, eventProxy.PlayerDisconnectAddedCount);
        Assert.Equal(1, eventProxy.PlayerDeathAddedCount);
        Assert.Equal(2, eventProxy.GameTickListenerCallCount);
    }

    private sealed class FakeDiscordLastMessageStore : IDiscordLastMessageStore
    {
        public string Load()
        {
            return string.Empty;
        }

        public void Save(string lastMessageId)
        {
        }

        public void Clear()
        {
        }
    }

    private sealed class FakeDiscordWebhookClient : IDiscordWebhookClient
    {
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
            return Task.FromResult(new DiscordHttpResponse
            {
                StatusCode = 200,
                Body = "[]"
            });
        }
    }

    private class TestCoreServerApiProxy : DispatchProxy
    {
        public IServerEventAPI? Event { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_Event" => Event,
                _ => targetMethod?.ReturnType.IsValueType == true
                    ? Activator.CreateInstance(targetMethod.ReturnType)
                    : null
            };
        }
    }

    private class TestServerEventApiProxy : DispatchProxy
    {
        public int PlayerChatAddedCount { get; private set; }

        public int PlayerJoinAddedCount { get; private set; }

        public int PlayerDisconnectAddedCount { get; private set; }

        public int PlayerDeathAddedCount { get; private set; }

        public int GameTickListenerCallCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "add_PlayerChat":
                    PlayerChatAddedCount++;
                    return null;
                case "add_PlayerJoin":
                    PlayerJoinAddedCount++;
                    return null;
                case "add_PlayerDisconnect":
                    PlayerDisconnectAddedCount++;
                    return null;
                case "add_PlayerDeath":
                    PlayerDeathAddedCount++;
                    return null;
                case "RegisterGameTickListener":
                    GameTickListenerCallCount++;
                    return 0L;
                default:
                    return targetMethod?.ReturnType.IsValueType == true
                        ? Activator.CreateInstance(targetMethod.ReturnType)
                        : null;
            }
        }
    }
}
