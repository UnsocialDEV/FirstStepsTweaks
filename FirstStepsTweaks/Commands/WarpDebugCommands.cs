using System.Text;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class WarpDebugCommands
    {
        private readonly WarpStore warpStore;
        private readonly IPlayerMessenger messenger;

        public WarpDebugCommands(WarpStore warpStore, IPlayerMessenger messenger)
        {
            this.warpStore = warpStore;
            this.messenger = messenger;
        }

        public TextCommandResult Inspect(TextCommandCallingArgs args)
        {
            var warps = warpStore.LoadWarps();
            if (warps.Count == 0)
            {
                SendDual(args.Caller.Player, "No stored warps found.");
                return TextCommandResult.Success();
            }

            var builder = new StringBuilder();
            builder.AppendLine($"Stored warps ({warps.Count}):");
            foreach (var pair in warps)
            {
                if (pair.Value == null || pair.Value.Length != 3)
                {
                    builder.AppendLine($"- {pair.Key}: invalid payload");
                    continue;
                }

                builder.AppendLine($"- {pair.Key}: {DebugCommandSupport.FormatVec3(pair.Value[0], pair.Value[1], pair.Value[2])}");
            }

            SendInfo(args.Caller.Player, builder.ToString().TrimEnd());
            return TextCommandResult.Success();
        }

        public TextCommandResult Clear(TextCommandCallingArgs args)
        {
            warpStore.ClearWarps();
            SendDual(args.Caller.Player, "Cleared all stored warps.");
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
