using System;
using System.Linq;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class WhereIsMyGraveCommand
    {
        private const double MaxTeleportDistanceBlocks = 100d;

        private readonly ICoreServerAPI api;
        private readonly GravestoneService gravestoneService;
        private readonly IPlayerMessenger messenger;
        private readonly IBackLocationStore backLocationStore;

        public WhereIsMyGraveCommand(
            ICoreServerAPI api,
            GravestoneService gravestoneService,
            IPlayerMessenger messenger,
            IBackLocationStore backLocationStore)
        {
            this.api = api;
            this.gravestoneService = gravestoneService;
            this.messenger = messenger;
            this.backLocationStore = backLocationStore;
        }

        public void Register()
        {
            api.ChatCommands
                .Create("whereismygrave")
                .WithDescription("Teleport to your latest grave if you are close enough to it")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(Execute);
        }

        private TextCommandResult Execute(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity?.Pos == null)
            {
                return TextCommandResult.Success();
            }

            GraveData grave = gravestoneService
                .GetActiveGraves()
                .Where(candidate => candidate != null
                    && string.Equals(candidate.OwnerUid, player.PlayerUID, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(candidate => candidate.CreatedUnixMs)
                .FirstOrDefault();

            if (grave == null)
            {
                SendBoth(player, "You do not have an active grave to return to.");
                return TextCommandResult.Success();
            }

            if (!gravestoneService.TryGetTeleportTarget(grave.GraveId, out GraveData resolvedGrave, out Vec3d target, out string message))
            {
                if (gravestoneService.TryAdminRestoreGraveToPlayer(grave.GraveId, player, out string restoreMessage))
                {
                    SendBoth(player, $"Your grave location can't be found. {restoreMessage}");
                    return TextCommandResult.Success();
                }

                SendBoth(player, $"Your grave location can't be found. {restoreMessage}");
                return TextCommandResult.Success();
            }

            if (!(player.Entity is EntityPlayer entityPlayer))
            {
                SendBoth(player, "Teleport is only available to in-game players.");
                return TextCommandResult.Success();
            }

            if (entityPlayer.Pos.Dimension != resolvedGrave.Dimension)
            {
                SendBoth(player, $"Your grave is in dimension {resolvedGrave.Dimension}. Return to where you died (within 25 blocks) then use /whereismygrave again.");
                return TextCommandResult.Success();
            }

            double distance = entityPlayer.Pos.DistanceTo(target);
            if (distance > MaxTeleportDistanceBlocks)
            {
                SendBoth(player, $"You are {Math.Ceiling(distance)} blocks from your grave. Return to where you died (within 25 blocks) and use /whereismygrave again.");
                return TextCommandResult.Success();
            }

            backLocationStore.RecordCurrentLocation(player);
            entityPlayer.TeleportToDouble(
                target.X,
                target.Y,
                target.Z,
                () => SendBoth(player, $"Teleported you to your grave at {resolvedGrave.Dimension}:{resolvedGrave.X},{resolvedGrave.Y},{resolvedGrave.Z}."));

            return TextCommandResult.Success();
        }

        private void SendBoth(IServerPlayer player, string message)
        {
            messenger.SendDual(player, message, (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
        }
    }
}
