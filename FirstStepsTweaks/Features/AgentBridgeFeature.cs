using System;
using System.Net;
using FirstStepsTweaks.AgentBridge;
using FirstStepsTweaks.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Features
{
    #nullable enable
    public sealed class AgentBridgeFeature : IFeatureModule, IDisposable
    {
        private readonly ICoreServerAPI api;
        private readonly FirstStepsTweaksConfig config;
        private readonly IAgentBridgeAvailabilityPolicy availabilityPolicy;
        private IAgentBridgeServer? server;
        private AgentBridgeTickTimeTracker? tickTimeTracker;
        private AgentBridgeMetricsPublisher? metricsPublisher;
        private AgentBridgeEventPublisher? eventPublisher;
        private readonly AgentBridgeStartupValidator startupValidator;
        private readonly AgentBridgeEndpointResolver endpointResolver;
        private readonly IAgentBridgeServerFactory serverFactory;

        public AgentBridgeFeature(ICoreServerAPI api, FirstStepsTweaksConfig config)
            : this(
                api,
                config,
                new ParkedAgentBridgeAvailabilityPolicy(),
                new AgentBridgeStartupValidator(),
                new AgentBridgeEndpointResolver(),
                new AgentBridgeTcpServerFactory())
        {
        }

        internal AgentBridgeFeature(
            ICoreServerAPI api,
            FirstStepsTweaksConfig config,
            IAgentBridgeAvailabilityPolicy availabilityPolicy,
            AgentBridgeStartupValidator startupValidator,
            AgentBridgeEndpointResolver endpointResolver,
            IAgentBridgeServerFactory serverFactory)
        {
            this.api = api;
            this.config = config;
            this.availabilityPolicy = availabilityPolicy;
            this.startupValidator = startupValidator;
            this.endpointResolver = endpointResolver;
            this.serverFactory = serverFactory;
        }

        public void Register()
        {
            if (!availabilityPolicy.IsAvailable(out var unavailableReason))
            {
                api.Logger.Notification($"[FirstStepsTweaks] {unavailableReason}");
                return;
            }

            if (!config.Features.EnableAgentBridge)
            {
                return;
            }

            // When the bridge is unparked, startup follows this sequence: validate config,
            // resolve the bind endpoint, construct the request/telemetry pipeline, start the
            // TCP listener, then attach the event and metrics publishers.
            if (!startupValidator.TryValidate(config.AgentBridge, out var message))
            {
                api.Logger.Warning($"[FirstStepsTweaks] Agent bridge listener was not started: {message}");
                return;
            }

            if (!endpointResolver.TryResolve(config.AgentBridge.Host, config.AgentBridge.Port, out IPEndPoint? endpoint, out message))
            {
                api.Logger.Warning($"[FirstStepsTweaks] Agent bridge listener was not started: {message}");
                return;
            }

            var serializer = new AgentBridgeJsonSerializer();
            var subscriptions = new AgentBridgeSubscriptionRegistry();
            var tokenValidator = new AgentBridgeTokenValidator(config.AgentBridge.SharedToken);
            var requestValidator = new AgentBridgeCommandRequestValidator();
            var executor = new AgentBridgeMainThreadCommandExecutor(api);
            var processor = new AgentBridgeRequestProcessor(tokenValidator, requestValidator, executor);
            var connectionHandler = new AgentBridgeConnectionHandler(serializer, processor, tokenValidator, subscriptions);
            var uptimeTracker = new AgentBridgeUptimeTracker();
            var localTickTimeTracker = new AgentBridgeTickTimeTracker(api);
            var localMetricsPublisher = new AgentBridgeMetricsPublisher(api, subscriptions, serializer, uptimeTracker, localTickTimeTracker);
            var localEventPublisher = new AgentBridgeEventPublisher(subscriptions, serializer);
            IAgentBridgeServer? localServer = null;
            bool eventsRegistered = false;

            try
            {
                localServer = serverFactory.Create(endpoint, api.Logger, connectionHandler);
                localServer.Start();
                localTickTimeTracker.Start();
                localMetricsPublisher.Start();

                api.Event.PlayerJoin += localEventPublisher.OnPlayerJoin;
                api.Event.PlayerLeave += localEventPublisher.OnPlayerLeave;
                api.Event.PlayerDeath += localEventPublisher.OnPlayerDeath;
                api.Event.GameWorldSave += localEventPublisher.OnGameWorldSave;
                eventsRegistered = true;
            }
            catch (Exception exception)
            {
                if (eventsRegistered)
                {
                    api.Event.PlayerJoin -= localEventPublisher.OnPlayerJoin;
                    api.Event.PlayerLeave -= localEventPublisher.OnPlayerLeave;
                    api.Event.PlayerDeath -= localEventPublisher.OnPlayerDeath;
                    api.Event.GameWorldSave -= localEventPublisher.OnGameWorldSave;
                }

                localMetricsPublisher.Stop();
                localTickTimeTracker.Stop();
                localServer?.Dispose();
                api.Logger.Warning($"[FirstStepsTweaks] Agent bridge listener was not started: {exception.Message}");
                return;
            }

            server = localServer;
            tickTimeTracker = localTickTimeTracker;
            metricsPublisher = localMetricsPublisher;
            eventPublisher = localEventPublisher;
        }

        public void Dispose()
        {
            if (eventPublisher is not null)
            {
                api.Event.PlayerJoin -= eventPublisher.OnPlayerJoin;
                api.Event.PlayerLeave -= eventPublisher.OnPlayerLeave;
                api.Event.PlayerDeath -= eventPublisher.OnPlayerDeath;
                api.Event.GameWorldSave -= eventPublisher.OnGameWorldSave;
                eventPublisher = null;
            }

            metricsPublisher?.Stop();
            metricsPublisher = null;
            tickTimeTracker?.Stop();
            tickTimeTracker = null;
            server?.Dispose();
            server = null;
        }
    }
}
