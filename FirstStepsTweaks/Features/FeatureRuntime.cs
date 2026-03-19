using FirstStepsTweaks.Infrastructure.LandClaims;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Infrastructure.Teleport;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Features
{
    public sealed class FeatureRuntime
    {
        public FeatureRuntime(ICoreServerAPI api)
        {
            Messenger = new PlayerMessenger();
            PlayerLookup = new PlayerLookup(api);
            BackLocationStore = new BackLocationStore();
            TeleportWarmupService = new TeleportWarmupService(api, Messenger);
            LandClaimAccessor = new ReflectionLandClaimAccessor(api);
        }

        public IPlayerMessenger Messenger { get; }

        public IPlayerLookup PlayerLookup { get; }

        public IBackLocationStore BackLocationStore { get; }

        public ITeleportWarmupService TeleportWarmupService { get; }

        public ILandClaimAccessor LandClaimAccessor { get; }
    }
}
