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
            whereIsMyGraveCommand = new WhereIsMyGraveCommand(api, gravestoneService, runtime.Messenger, runtime.BackLocationStore, runtime.CoordinateReader, runtime.CoordinateDisplayFormatter);
            gravestoneCommands = new GravestoneCommands(api, gravestoneService, runtime.Messenger, runtime.PlayerLookup, runtime.BackLocationStore, runtime.CoordinateReader, runtime.CoordinateDisplayFormatter);
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
