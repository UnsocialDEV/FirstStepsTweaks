using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public static class BackCommands
    {
        private static readonly Dictionary<string, Vec3d> LastPositionsByPlayerUid =
            new Dictionary<string, Vec3d>();

        public static void Register(ICoreServerAPI api)
        {
            api.ChatCommands
                .Create("back")
                .WithDescription("Teleport back to your last location")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => Back(args));
        }

        public static void RecordCurrentLocation(IServerPlayer player)
        {
            if (player?.Entity?.Pos == null)
            {
                return;
            }

            LastPositionsByPlayerUid[player.PlayerUID] = player.Entity.Pos.XYZ.Copy();
        }

        private static TextCommandResult Back(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;

            if (!LastPositionsByPlayerUid.TryGetValue(player.PlayerUID, out Vec3d lastLocation))
            {
                player.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    "No previous location recorded.",
                    EnumChatType.CommandError
                );
                return TextCommandResult.Success();
            }

            Vec3d currentLocation = player.Entity.Pos.XYZ.Copy();

            player.Entity.TeleportToDouble(lastLocation.X, lastLocation.Y, lastLocation.Z);

            LastPositionsByPlayerUid[player.PlayerUID] = currentLocation;

            player.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "Teleported to your last location.",
                EnumChatType.CommandSuccess
            );

            return TextCommandResult.Success();
        }
    }
}
