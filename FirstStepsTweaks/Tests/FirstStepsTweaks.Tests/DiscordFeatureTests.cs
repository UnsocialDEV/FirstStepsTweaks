using System;
using System.Reflection;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Features;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class DiscordFeatureTests
{
    [Fact]
    public void Register_DefersDiscordRuntimeHooksUntilWorldReady()
    {
        var (api, eventApi, _) = CreateApi(EnumServerRunPhase.GameReady);
        int discordBridgeRegisterCount = 0;
        int linkPollerRegisterCount = 0;
        int discordCommandRegisterCount = 0;
        int linkCommandRegisterCount = 0;
        var feature = new DiscordFeature(
            api,
            () => discordBridgeRegisterCount++,
            () => linkPollerRegisterCount++,
            () => linkCommandRegisterCount++,
            () => discordCommandRegisterCount++,
            () => api.Event.PlayerNowPlaying += _ => { },
            new DiscordStartupCoordinator(api));

        feature.Register();

        Assert.Equal(1, discordCommandRegisterCount);
        Assert.Equal(1, linkCommandRegisterCount);
        Assert.Equal(0, discordBridgeRegisterCount);
        Assert.Equal(0, linkPollerRegisterCount);
        Assert.Equal(0, eventApi.PlayerNowPlayingAddedCount);
        Assert.Equal(1, eventApi.ServerRunPhaseCallCount);
        Assert.Equal(EnumServerRunPhase.WorldReady, eventApi.RegisteredPhase);

        eventApi.RegisteredAction!.Invoke();
        eventApi.RegisteredAction.Invoke();

        Assert.Equal(1, discordBridgeRegisterCount);
        Assert.Equal(1, linkPollerRegisterCount);
        Assert.Equal(1, eventApi.PlayerNowPlayingAddedCount);
    }

    private static (ICoreServerAPI Api, TestServerEventApiProxy EventApi, TestServerApiProxy ServerApi) CreateApi(EnumServerRunPhase phase)
    {
        var eventApi = DispatchProxy.Create<IServerEventAPI, TestServerEventApiProxy>();
        var eventProxy = (TestServerEventApiProxy)(object)eventApi;
        var serverApi = DispatchProxy.Create<IServerAPI, TestServerApiProxy>();
        var serverProxy = (TestServerApiProxy)(object)serverApi;
        serverProxy.CurrentRunPhase = phase;
        var api = DispatchProxy.Create<ICoreServerAPI, TestCoreServerApiProxy>();
        var apiProxy = (TestCoreServerApiProxy)(object)api;
        apiProxy.Event = eventApi;
        apiProxy.Server = serverApi;
        return (api, eventProxy, serverProxy);
    }

    private class TestCoreServerApiProxy : DispatchProxy
    {
        public IServerEventAPI? Event { get; set; }

        public IServerAPI? Server { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_Event" => Event,
                "get_Server" => Server,
                _ => targetMethod?.ReturnType.IsValueType == true
                    ? Activator.CreateInstance(targetMethod.ReturnType)
                    : null
            };
        }
    }

    private class TestServerApiProxy : DispatchProxy
    {
        public EnumServerRunPhase CurrentRunPhase { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_CurrentRunPhase" => CurrentRunPhase,
                _ => targetMethod?.ReturnType.IsValueType == true
                    ? Activator.CreateInstance(targetMethod.ReturnType)
                    : null
            };
        }
    }

    private class TestServerEventApiProxy : DispatchProxy
    {
        public int ServerRunPhaseCallCount { get; private set; }

        public EnumServerRunPhase RegisteredPhase { get; private set; }

        public Action? RegisteredAction { get; private set; }

        public int PlayerNowPlayingAddedCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "ServerRunPhase":
                    ServerRunPhaseCallCount++;
                    RegisteredPhase = (EnumServerRunPhase)args![0]!;
                    RegisteredAction = (Action)args[1]!;
                    return null;
                case "add_PlayerNowPlaying":
                    PlayerNowPlayingAddedCount++;
                    return null;
                default:
                    return targetMethod?.ReturnType.IsValueType == true
                        ? Activator.CreateInstance(targetMethod.ReturnType)
                        : null;
            }
        }
    }
}
