using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using FirstStepsTweaks.AgentBridge;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Features;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests.AgentBridge;

public sealed class AgentBridgeFeatureTests
{
    [Fact]
    public void Register_ParksBridgeBeforeAnyRuntimeStartupWork()
    {
        var (api, eventApi, logger) = CreateApi();
        var factory = new FakeServerFactory(() => new RecordingServer());
        var feature = new AgentBridgeFeature(
            api,
            new FirstStepsTweaksConfig(),
            new ParkedAgentBridgeAvailabilityPolicy(),
            new AgentBridgeStartupValidator(),
            new AgentBridgeEndpointResolver(),
            factory);

        feature.Register();

        Assert.Contains(logger.NotificationMessages, message => message.Contains("intentionally disabled"));
        Assert.Equal(0, eventApi.TotalAddedHandlers);
        Assert.Equal(0, eventApi.TimerCallCount);
        Assert.Empty(factory.Endpoints);
    }

    [Fact]
    public void Register_DoesNotThrow_WhenSharedTokenIsMissing()
    {
        var (api, eventApi, logger) = CreateApi();
        var config = CreateEnabledConfig();
        config.AgentBridge.SharedToken = string.Empty;
        var feature = new AgentBridgeFeature(
            api,
            config,
            new EnabledAgentBridgeAvailabilityPolicy(),
            new AgentBridgeStartupValidator(),
            new AgentBridgeEndpointResolver(),
            new FakeServerFactory(() => new RecordingServer()));

        feature.Register();

        Assert.Contains(logger.WarningMessages, message => message.Contains("SharedToken"));
        Assert.Equal(0, eventApi.TotalAddedHandlers);
    }

    [Fact]
    public void Register_DoesNotThrow_WhenHostIsInvalid()
    {
        var (api, eventApi, logger) = CreateApi();
        var config = CreateEnabledConfig();
        config.AgentBridge.Host = "invalid host name ###";
        var feature = new AgentBridgeFeature(
            api,
            config,
            new EnabledAgentBridgeAvailabilityPolicy(),
            new AgentBridgeStartupValidator(),
            new AgentBridgeEndpointResolver(),
            new FakeServerFactory(() => new RecordingServer()));

        feature.Register();

        Assert.Contains(logger.WarningMessages, message => message.Contains("could not be resolved"));
        Assert.Equal(0, eventApi.TotalAddedHandlers);
    }

    [Fact]
    public void Register_DoesNotThrow_WhenServerStartFails()
    {
        var (api, eventApi, logger) = CreateApi();
        var config = CreateEnabledConfig();
        var server = new ThrowingServer();
        var feature = new AgentBridgeFeature(
            api,
            config,
            new EnabledAgentBridgeAvailabilityPolicy(),
            new AgentBridgeStartupValidator(),
            new AgentBridgeEndpointResolver(),
            new FakeServerFactory(() => server));

        feature.Register();

        Assert.True(server.Disposed);
        Assert.Contains(logger.WarningMessages, message => message.Contains("port already in use"));
        Assert.Equal(0, eventApi.TotalAddedHandlers);
    }

    [Fact]
    public void Register_AttachesLifecycleHooks_OnlyAfterSuccessfulStartup()
    {
        var (api, eventApi, logger) = CreateApi();
        var config = CreateEnabledConfig();
        var server = new RecordingServer();
        var factory = new FakeServerFactory(() => server);
        var feature = new AgentBridgeFeature(
            api,
            config,
            new EnabledAgentBridgeAvailabilityPolicy(),
            new AgentBridgeStartupValidator(),
            new AgentBridgeEndpointResolver(),
            factory);

        feature.Register();

        Assert.True(server.Started);
        Assert.Equal(4, eventApi.TotalAddedHandlers);
        Assert.Equal(0, eventApi.TotalRemovedHandlers);
        Assert.Equal(2, eventApi.TimerCallCount);
        Assert.Single(factory.Endpoints);
        Assert.Equal(IPAddress.Loopback, factory.Endpoints[0].Address);
        Assert.Empty(logger.WarningMessages);

        feature.Dispose();

        Assert.True(server.Disposed);
        Assert.Equal(4, eventApi.TotalRemovedHandlers);
    }

    private static FirstStepsTweaksConfig CreateEnabledConfig()
    {
        var config = new FirstStepsTweaksConfig();
        config.Features.EnableAgentBridge = true;
        config.AgentBridge.SharedToken = "token";
        config.AgentBridge.Host = "127.0.0.1";
        config.AgentBridge.Port = AgentBridgeConfig.DefaultPort;
        return config;
    }

    private static (ICoreServerAPI Api, TestServerEventApiProxy EventApi, TestLoggerProxy Logger) CreateApi()
    {
        var logger = DispatchProxy.Create<ILogger, TestLoggerProxy>();
        var loggerProxy = (TestLoggerProxy)(object)logger;
        var eventApi = DispatchProxy.Create<IServerEventAPI, TestServerEventApiProxy>();
        var eventProxy = (TestServerEventApiProxy)(object)eventApi;
        var api = DispatchProxy.Create<ICoreServerAPI, TestCoreServerApiProxy>();
        var apiProxy = (TestCoreServerApiProxy)(object)api;
        apiProxy.Logger = logger;
        apiProxy.Event = eventApi;
        return (api, eventProxy, loggerProxy);
    }

    private sealed class EnabledAgentBridgeAvailabilityPolicy : IAgentBridgeAvailabilityPolicy
    {
        public bool IsAvailable(out string unavailableReason)
        {
            unavailableReason = string.Empty;
            return true;
        }
    }

    private sealed class FakeServerFactory : IAgentBridgeServerFactory
    {
        private readonly Func<IAgentBridgeServer> createServer;

        public FakeServerFactory(Func<IAgentBridgeServer> createServer)
        {
            this.createServer = createServer;
        }

        public List<IPEndPoint> Endpoints { get; } = new();

        public IAgentBridgeServer Create(IPEndPoint endpoint, ILogger logger, AgentBridgeConnectionHandler connectionHandler)
        {
            Endpoints.Add(endpoint);
            return createServer();
        }
    }

    private sealed class RecordingServer : IAgentBridgeServer
    {
        public bool Started { get; private set; }

        public bool Disposed { get; private set; }

        public void Start()
        {
            Started = true;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class ThrowingServer : IAgentBridgeServer
    {
        public bool Disposed { get; private set; }

        public void Start()
        {
            throw new InvalidOperationException("port already in use");
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private class TestCoreServerApiProxy : DispatchProxy
    {
        public ILogger? Logger { get; set; }

        public IServerEventAPI? Event { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_Logger" => Logger,
                "get_Event" => Event,
                _ => targetMethod?.ReturnType.IsValueType == true
                    ? Activator.CreateInstance(targetMethod.ReturnType)
                    : null
            };
        }
    }

    private class TestServerEventApiProxy : DispatchProxy
    {
        public int TimerCallCount { get; private set; }

        public int TotalAddedHandlers { get; private set; }

        public int TotalRemovedHandlers { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null)
            {
                return null;
            }

            switch (targetMethod.Name)
            {
                case "Timer":
                    TimerCallCount++;
                    return null;
                case "add_PlayerJoin":
                case "add_PlayerLeave":
                case "add_PlayerDeath":
                case "add_GameWorldSave":
                    TotalAddedHandlers++;
                    return null;
                case "remove_PlayerJoin":
                case "remove_PlayerLeave":
                case "remove_PlayerDeath":
                case "remove_GameWorldSave":
                    TotalRemovedHandlers++;
                    return null;
                default:
                    return targetMethod.ReturnType.IsValueType
                        ? Activator.CreateInstance(targetMethod.ReturnType)
                        : null;
            }
        }
    }

    private class TestLoggerProxy : DispatchProxy
    {
        public List<string> WarningMessages { get; } = new();

        public List<string> NotificationMessages { get; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null)
            {
                return null;
            }

            string message = args?.Length > 0 && args[0] != null
                ? args[0]!.ToString()!
                : string.Empty;

            switch (targetMethod.Name)
            {
                case "Warning":
                    WarningMessages.Add(message);
                    return null;
                case "Notification":
                    NotificationMessages.Add(message);
                    return null;
                default:
                    return targetMethod.ReturnType.IsValueType
                        ? Activator.CreateInstance(targetMethod.ReturnType)
                        : null;
            }
        }
    }
}
