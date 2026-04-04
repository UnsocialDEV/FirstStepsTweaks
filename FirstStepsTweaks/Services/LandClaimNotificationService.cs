using System;
using System.Collections.Generic;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.LandClaims;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class LandClaimNotificationService
    {
        private readonly ICoreServerAPI api;
        private readonly LandClaimNotificationConfig config;
        private readonly ILandClaimAccessor landClaimAccessor;
        private readonly LandClaimMessageFormatter formatter;
        private readonly IWorldCoordinateReader coordinateReader;
        private readonly Dictionary<string, LandClaimInfo> playerClaimByUid = new Dictionary<string, LandClaimInfo>();

        public LandClaimNotificationService(ICoreServerAPI api, FirstStepsTweaksConfig rootConfig)
            : this(api, rootConfig, new ReflectionLandClaimAccessor(api), new LandClaimMessageFormatter(), new WorldCoordinateReader())
        {
        }

        public LandClaimNotificationService(
            ICoreServerAPI api,
            FirstStepsTweaksConfig rootConfig,
            ILandClaimAccessor landClaimAccessor,
            LandClaimMessageFormatter formatter)
            : this(api, rootConfig, landClaimAccessor, formatter, new WorldCoordinateReader())
        {
        }

        public LandClaimNotificationService(
            ICoreServerAPI api,
            FirstStepsTweaksConfig rootConfig,
            ILandClaimAccessor landClaimAccessor,
            LandClaimMessageFormatter formatter,
            IWorldCoordinateReader coordinateReader)
        {
            this.api = api;
            config = rootConfig.LandClaims ?? new LandClaimNotificationConfig();
            this.landClaimAccessor = landClaimAccessor;
            this.formatter = formatter;
            this.coordinateReader = coordinateReader ?? new WorldCoordinateReader();

            api.Event.RegisterGameTickListener(OnTick, Math.Max(200, config.TickIntervalMs));
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
        }

        private void OnPlayerDisconnect(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            playerClaimByUid.Remove(player.PlayerUID);
        }

        private void OnTick(float dt)
        {
            foreach (IPlayer onlinePlayer in api.World.AllOnlinePlayers)
            {
                if (!(onlinePlayer is IServerPlayer player) || player.Entity == null)
                {
                    continue;
                }

                LandClaimInfo previousClaim = playerClaimByUid.TryGetValue(player.PlayerUID, out LandClaimInfo snapshot)
                    ? snapshot
                    : LandClaimInfo.None;

                var currentPosition = coordinateReader.GetBlockPosition(player);
                if (currentPosition == null)
                {
                    continue;
                }

                LandClaimInfo currentClaim = landClaimAccessor.GetClaimAt(currentPosition);
                if (previousClaim.Key == currentClaim.Key)
                {
                    continue;
                }

                if (IsTransitionBetweenPlayersOwnClaims(previousClaim, currentClaim, player))
                {
                    playerClaimByUid[player.PlayerUID] = currentClaim;
                    continue;
                }

                if (previousClaim.Exists)
                {
                    Send(player, formatter.BuildMessage(config.ExitMessage, previousClaim.ClaimName, previousClaim.OwnerUid, previousClaim.OwnerName, player.PlayerUID, player.PlayerName));
                }

                if (currentClaim.Exists)
                {
                    Send(player, formatter.BuildMessage(config.EnterMessage, currentClaim.ClaimName, currentClaim.OwnerUid, currentClaim.OwnerName, player.PlayerUID, player.PlayerName));
                    playerClaimByUid[player.PlayerUID] = currentClaim;
                }
                else
                {
                    playerClaimByUid.Remove(player.PlayerUID);
                }
            }
        }

        private static bool IsTransitionBetweenPlayersOwnClaims(LandClaimInfo previousClaim, LandClaimInfo currentClaim, IServerPlayer player)
        {
            if (!previousClaim.Exists || !currentClaim.Exists || player == null)
            {
                return false;
            }

            return IsOwnedByPlayer(previousClaim, player) && IsOwnedByPlayer(currentClaim, player);
        }

        private static bool IsOwnedByPlayer(LandClaimInfo claim, IServerPlayer player)
        {
            return claim.Exists
                && player != null
                && !string.IsNullOrWhiteSpace(claim.OwnerUid)
                && string.Equals(claim.OwnerUid, player.PlayerUID, StringComparison.OrdinalIgnoreCase);
        }

        private static void Send(IServerPlayer player, string message)
        {
            if (player == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            player.SendIngameError("no_permission", message);
        }
    }
}
