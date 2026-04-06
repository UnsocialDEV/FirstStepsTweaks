using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class StaffTargetResolver
    {
        private readonly IPlayerLookup playerLookup;

        public StaffTargetResolver(IPlayerLookup playerLookup)
        {
            this.playerLookup = playerLookup;
        }

        public StaffCommandTarget ResolvePersistentTarget(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            IServerPlayer onlinePlayer = ResolveOnlinePlayer(token);
            if (onlinePlayer != null)
            {
                return new StaffCommandTarget
                {
                    PlayerUid = onlinePlayer.PlayerUID,
                    DisplayName = onlinePlayer.PlayerName,
                    OnlinePlayer = onlinePlayer
                };
            }

            return new StaffCommandTarget
            {
                PlayerUid = token.Trim(),
                DisplayName = token.Trim()
            };
        }

        public StaffCommandTarget ResolveOnlineTarget(string token)
        {
            IServerPlayer onlinePlayer = ResolveOnlinePlayer(token);
            if (onlinePlayer == null)
            {
                return null;
            }

            return new StaffCommandTarget
            {
                PlayerUid = onlinePlayer.PlayerUID,
                DisplayName = onlinePlayer.PlayerName,
                OnlinePlayer = onlinePlayer
            };
        }

        private IServerPlayer ResolveOnlinePlayer(string token)
        {
            return playerLookup.FindOnlinePlayerByName(token) ?? playerLookup.FindOnlinePlayerByUid(token);
        }
    }
}
