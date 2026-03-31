using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class SpawnDebugCommands
    {
        private readonly SpawnStore spawnStore;
        private readonly IPlayerMessenger messenger;

        public SpawnDebugCommands(SpawnStore spawnStore, IPlayerMessenger messenger)
        {
            this.spawnStore = spawnStore;
            this.messenger = messenger;
        }

        public TextCommandResult Inspect(TextCommandCallingArgs args)
        {
            if (!spawnStore.TryGetSpawn(out var spawn))
            {
                SendDual(args.Caller.Player, "Spawn is not set.");
                return TextCommandResult.Success();
            }

            SendInfo(args.Caller.Player, $"Stored spawn: {DebugCommandSupport.FormatVec3(spawn.X, spawn.Y, spawn.Z)}");
            return TextCommandResult.Success();
        }

        public TextCommandResult Clear(TextCommandCallingArgs args)
        {
            spawnStore.ClearSpawn();
            SendDual(args.Caller.Player, "Cleared stored spawn.");
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
