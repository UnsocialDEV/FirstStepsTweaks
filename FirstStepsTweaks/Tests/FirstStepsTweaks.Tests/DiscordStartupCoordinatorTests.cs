using System;
using System.Reflection;
using FirstStepsTweaks.Features;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class DiscordStartupCoordinatorTests
{
    [Fact]
    public void RunWhenWorldReady_DefersUntilWorldReady_WhenServerIsStillLoading()
    {
        var (api, eventApi, _) = CreateApi(EnumServerRunPhase.GameReady);
        var coordinator = new DiscordStartupCoordinator(api);
        int callCount = 0;

        coordinator.RunWhenWorldReady(() => callCount++);

        Assert.Equal(0, callCount);
        Assert.Equal(1, eventApi.ServerRunPhaseCallCount);
        Assert.Equal(EnumServerRunPhase.WorldReady, eventApi.RegisteredPhase);

        eventApi.RegisteredAction!.Invoke();

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void RunWhenWorldReady_RunsImmediately_WhenServerIsAlreadyWorldReady()
    {
        var (api, eventApi, _) = CreateApi(EnumServerRunPhase.WorldReady);
        var coordinator = new DiscordStartupCoordinator(api);
        int callCount = 0;

        coordinator.RunWhenWorldReady(() => callCount++);

        Assert.Equal(1, callCount);
        Assert.Equal(0, eventApi.ServerRunPhaseCallCount);
        Assert.Null(eventApi.RegisteredAction);
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

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "ServerRunPhase")
            {
                ServerRunPhaseCallCount++;
                RegisteredPhase = (EnumServerRunPhase)args![0]!;
                RegisteredAction = (Action)args[1]!;
                return null;
            }

            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
