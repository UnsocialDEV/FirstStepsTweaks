using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Infrastructure.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class AdminTeleportService
    {
        private readonly IPlayerLookup playerLookup;
        private readonly IBackLocationStore backLocationStore;
        private readonly IPlayerTeleporter playerTeleporter;
        private readonly IPlayerMessenger messenger;
        private readonly IWorldCoordinateReader coordinateReader;

        public AdminTeleportService(
            IPlayerLookup playerLookup,
            IBackLocationStore backLocationStore,
            IPlayerTeleporter playerTeleporter,
            IPlayerMessenger messenger)
            : this(playerLookup, backLocationStore, playerTeleporter, messenger, new WorldCoordinateReader())
        {
        }

        public AdminTeleportService(
            IPlayerLookup playerLookup,
            IBackLocationStore backLocationStore,
            IPlayerTeleporter playerTeleporter,
            IPlayerMessenger messenger,
            IWorldCoordinateReader coordinateReader)
        {
            this.playerLookup = playerLookup;
            this.backLocationStore = backLocationStore;
            this.playerTeleporter = playerTeleporter;
            this.messenger = messenger;
            this.coordinateReader = coordinateReader ?? new WorldCoordinateReader();
        }

        public void TeleportCallerToTarget(IServerPlayer caller, string targetName)
        {
            IServerPlayer target = ResolveTarget(caller, targetName);
            Vec3d targetPosition = coordinateReader.GetExactPosition(target);
            if (targetPosition == null)
            {
                return;
            }

            backLocationStore.RecordCurrentLocation(caller);
            playerTeleporter.Teleport(caller, targetPosition);
            messenger.SendDual(caller, $"Teleported to {target.PlayerName}.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
            messenger.SendDual(target, $"{caller.PlayerName} teleported to you.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
        }

        public void TeleportTargetToCaller(IServerPlayer caller, string targetName)
        {
            IServerPlayer target = ResolveTarget(caller, targetName);
            Vec3d callerPosition = coordinateReader.GetExactPosition(caller);
            if (target?.Entity?.Pos == null || callerPosition == null)
            {
                return;
            }

            backLocationStore.RecordCurrentLocation(target);
            playerTeleporter.Teleport(target, callerPosition);
            messenger.SendDual(caller, $"Teleported {target.PlayerName} to you.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
            messenger.SendDual(target, $"{caller.PlayerName} teleported you to them.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
        }

        private IServerPlayer ResolveTarget(IServerPlayer caller, string targetName)
        {
            IServerPlayer target = playerLookup.FindOnlinePlayerByName(targetName);
            if (target == null)
            {
                messenger.SendDual(caller, "Player not found.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return null;
            }

            if (target.PlayerUID == caller.PlayerUID)
            {
                messenger.SendDual(caller, "You cannot teleport to yourself.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return null;
            }

            return target;
        }
    }
}
