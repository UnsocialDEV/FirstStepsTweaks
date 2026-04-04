using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.LandClaims;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Services;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Features
{
    public sealed class FeatureRuntime
    {
        public FeatureRuntime(ICoreServerAPI api, FirstStepsTweaksConfig config)
        {
            CoordinateReader = new WorldCoordinateReader();
            CoordinateDisplayFormatter = new WorldCoordinateDisplayFormatter(api);
            Messenger = new PlayerMessenger();
            PlayerLookup = new PlayerLookup(api);
            BackLocationStore = new BackLocationStore(CoordinateReader);
            TeleportWarmupService = new TeleportWarmupService(api, Messenger, CoordinateReader);
            LandClaimAccessor = new ReflectionLandClaimAccessor(api);
            PlayerLoadoutManager = new PlayerLoadoutManager(api);
            AdminModeStore = new AdminModeStore();
            AdminModeService = new AdminModeService(
                api,
                AdminModeStore,
                new AdminModePlayerStateController(
                    new PlayerRoleCodeReader(),
                    new PlayerRoleAssigner(api),
                    new PlayerPrivilegeReader(),
                    new PlayerPrivilegeMutator(api)),
                new AdminModeLoadoutService(api, PlayerLoadoutManager, CoordinateReader),
                new AdminModeVitalsService(),
                Messenger);
            GravestoneService = new GravestoneService(api, config, Messenger, LandClaimAccessor, AdminModeStore, CoordinateReader, CoordinateDisplayFormatter);
            DiscordLinkRewardService = new DiscordLinkRewardService(
                new DiscordLinkRewardStateStore(api),
                new DiscordLinkRewardItemGiver(api));
            DiscordLinkPollerStatusTracker = new DiscordLinkPollerStatusTracker();
        }

        public IWorldCoordinateReader CoordinateReader { get; }

        public IWorldCoordinateDisplayFormatter CoordinateDisplayFormatter { get; }

        public IPlayerMessenger Messenger { get; }

        public IPlayerLookup PlayerLookup { get; }

        public IBackLocationStore BackLocationStore { get; }

        public ITeleportWarmupService TeleportWarmupService { get; }

        public ILandClaimAccessor LandClaimAccessor { get; }

        public IPlayerLoadoutManager PlayerLoadoutManager { get; }

        public IAdminModeStore AdminModeStore { get; }

        public AdminModeService AdminModeService { get; }

        public GravestoneService GravestoneService { get; }

        public DiscordLinkRewardService DiscordLinkRewardService { get; }

        public DiscordLinkPollerStatusTracker DiscordLinkPollerStatusTracker { get; }
    }
}
