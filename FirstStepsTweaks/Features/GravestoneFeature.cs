using FirstStepsTweaks.Commands;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Services;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Features
{
    public sealed class GravestoneFeature : IFeatureModule
    {
        private readonly FirstStepsTweaksConfig config;
        private readonly WhereIsMyGraveCommand whereIsMyGraveCommand;
        private readonly GravestoneCommands gravestoneCommands;

        public GravestoneFeature(ICoreServerAPI api, FirstStepsTweaksConfig config, FeatureRuntime runtime)
        {
            this.config = config;
            var gravestoneService = runtime.GravestoneService;
            var entryFormatter = new GraveAdminEntryFormatter(runtime.CoordinateDisplayFormatter);
            var nearbyQuery = new GraveAdminNearbyQuery(runtime.CoordinateReader);
            var snapshotStore = new GraveAdminListSnapshotStore();
            var selectorResolver = new GraveAdminSelectorResolver(snapshotStore, gravestoneService);
            var pageFormatter = new GraveAdminPageFormatter(entryFormatter);
            var restoreTargetResolver = new GraveAdminRestoreTargetResolver(runtime.PlayerLookup);
            var infoPresenter = new GraveAdminInfoPresenter(gravestoneService, gravestoneService.IsPubliclyClaimable, runtime.Messenger, runtime.CoordinateReader, entryFormatter);
            whereIsMyGraveCommand = new WhereIsMyGraveCommand(api, gravestoneService, runtime.Messenger, runtime.BackLocationStore, runtime.CoordinateReader, runtime.CoordinateDisplayFormatter);
            gravestoneCommands = new GravestoneCommands(
                api,
                gravestoneService,
                runtime.Messenger,
                runtime.PlayerLookup,
                runtime.BackLocationStore,
                runtime.CoordinateReader,
                runtime.CoordinateDisplayFormatter,
                nearbyQuery,
                snapshotStore,
                selectorResolver,
                pageFormatter,
                restoreTargetResolver,
                infoPresenter);
        }

        public void Register()
        {
            whereIsMyGraveCommand.Register();

            if (config.Features.EnableCorpseAdminCommands)
            {
                gravestoneCommands.Register();
            }
        }
    }
}
