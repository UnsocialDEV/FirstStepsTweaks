using FirstStepsTweaks.Config;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Services;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Features
{
    public sealed class JoinFeature : IFeatureModule
    {
        private readonly ICoreServerAPI api;
        private readonly JoinService joinService;
        private readonly JoinInvulnerabilityService joinInvulnerabilityService;
        private readonly DiscordLinkRewardJoinHandler discordLinkRewardJoinHandler;
        private readonly LandClaimNotificationService landClaimNotificationService;

        public JoinFeature(ICoreServerAPI api, FirstStepsTweaksConfig config, FeatureRuntime runtime)
        {
            this.api = api;
            joinService = new JoinService(api, config);
            joinInvulnerabilityService = new JoinInvulnerabilityService(api);
            discordLinkRewardJoinHandler = new DiscordLinkRewardJoinHandler(runtime.DiscordLinkRewardService, runtime.Messenger);

            if (config.Features.EnableLandClaimNotifications)
            {
                landClaimNotificationService = new LandClaimNotificationService(api, config, runtime.LandClaimAccessor, new LandClaimMessageFormatter());
            }
        }

        public void Register()
        {
            api.Event.PlayerJoin += joinInvulnerabilityService.OnPlayerJoin;
            api.Event.PlayerNowPlaying += joinInvulnerabilityService.OnPlayerNowPlaying;
            api.Event.PlayerNowPlaying += joinService.OnPlayerNowPlaying;
            api.Event.PlayerNowPlaying += discordLinkRewardJoinHandler.OnPlayerNowPlaying;
            api.Event.PlayerLeave += joinInvulnerabilityService.OnPlayerLeave;
            api.Event.PlayerLeave += joinService.OnPlayerLeave;
        }
    }
}
