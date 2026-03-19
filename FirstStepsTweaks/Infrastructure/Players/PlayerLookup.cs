using System;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Players
{
    public sealed class PlayerLookup : IPlayerLookup
    {
        private readonly ICoreServerAPI api;

        public PlayerLookup(ICoreServerAPI api)
        {
            this.api = api;
        }

        public IServerPlayer FindOnlinePlayerByUid(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
            {
                return null;
            }

            foreach (IServerPlayer player in api.World.AllOnlinePlayers)
            {
                if (string.Equals(player.PlayerUID, uid, StringComparison.OrdinalIgnoreCase))
                {
                    return player;
                }
            }

            return null;
        }

        public IServerPlayer FindOnlinePlayerByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            foreach (IServerPlayer player in api.World.AllOnlinePlayers)
            {
                if (string.Equals(player.PlayerName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return player;
                }
            }

            return null;
        }
    }
}
