using System;
using System.Linq;
using System.Text;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class GraveDebugCommands
    {
        private readonly GravestoneService gravestoneService;
        private readonly IPlayerMessenger messenger;

        public GraveDebugCommands(GravestoneService gravestoneService, IPlayerMessenger messenger)
        {
            this.gravestoneService = gravestoneService;
            this.messenger = messenger;
        }

        public TextCommandResult Inspect(TextCommandCallingArgs args)
        {
            var graves = gravestoneService.GetActiveGraves();
            if (graves.Count == 0)
            {
                SendDual(args.Caller.Player, "No active gravestones found.");
                return TextCommandResult.Success();
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var builder = new StringBuilder();
            builder.AppendLine($"Active gravestones ({graves.Count}):");
            foreach (GraveData grave in graves.OrderBy(grave => grave.CreatedUnixMs))
            {
                long ageMinutes = Math.Max(0, (now - grave.CreatedUnixMs) / 60000L);
                string claimState = gravestoneService.IsPubliclyClaimable(grave) ? "public" : "owner-only";
                builder.AppendLine($"- {grave.GraveId} | owner={grave.OwnerName} | pos={grave.Dimension}:{grave.X},{grave.Y},{grave.Z} | age={ageMinutes}m | {claimState}");
            }

            SendInfo(args.Caller.Player, builder.ToString().TrimEnd());
            return TextCommandResult.Success();
        }

        public TextCommandResult Clear(TextCommandCallingArgs args)
        {
            int cleared = gravestoneService.ClearAllGraves();
            SendDual(args.Caller.Player, $"Cleared {cleared} gravestone(s).");
            return TextCommandResult.Success();
        }

        private void SendDual(IPlayer caller, string message)
        {
            if (caller is IServerPlayer serverPlayer)
            {
                messenger.SendDual(serverPlayer, message, (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
            }
        }

        private void SendInfo(IPlayer caller, string message)
        {
            if (caller is IServerPlayer serverPlayer)
            {
                messenger.SendInfo(serverPlayer, message, GlobalConstants.InfoLogChatGroup, (int)EnumChatType.Notification);
            }
        }
    }
}
