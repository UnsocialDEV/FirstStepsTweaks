using System;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Players
{
    public sealed class DelayedPlayerActionScheduler : IDelayedPlayerActionScheduler
    {
        private readonly ICoreServerAPI api;

        public DelayedPlayerActionScheduler(ICoreServerAPI api)
        {
            this.api = api;
        }

        public void Schedule(string playerUid, int delayMs, Action<IServerPlayer> action)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || action == null)
            {
                return;
            }

            api.Event.RegisterCallback(_ =>
            {
                IServerPlayer player = FindOnlinePlayer(playerUid);
                if (player != null)
                {
                    action(player);
                }
            }, Math.Max(0, delayMs));
        }

        private IServerPlayer FindOnlinePlayer(string playerUid)
        {
            foreach (IServerPlayer player in api.World.AllOnlinePlayers)
            {
                if (string.Equals(player.PlayerUID, playerUid, StringComparison.OrdinalIgnoreCase))
                {
                    return player;
                }
            }

            return null;
        }
    }
}
