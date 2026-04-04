using System;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Features
{
    internal sealed class DiscordStartupCoordinator
    {
        private readonly ICoreServerAPI api;

        public DiscordStartupCoordinator(ICoreServerAPI api)
        {
            this.api = api;
        }

        public void RunWhenWorldReady(Action action)
        {
            IServerAPI server = api.Server;
            if (server != null && server.CurrentRunPhase >= EnumServerRunPhase.WorldReady)
            {
                action();
                return;
            }

            api.Event.ServerRunPhase(EnumServerRunPhase.WorldReady, action);
        }
    }
}
