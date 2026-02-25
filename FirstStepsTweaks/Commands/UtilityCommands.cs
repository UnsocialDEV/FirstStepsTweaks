using System.Linq;
using FirstStepsTweaks.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public static class UtilityCommands
    {
        private static UtilityConfig utilityConfig = new UtilityConfig();
        public static void Register(ICoreServerAPI api, FirstStepsTweaksConfig config)
        {
            utilityConfig = config?.Utility ?? new UtilityConfig();

            api.ChatCommands
                .Create("whosonline")
                .WithDescription("Shows the current player list")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => WhosOnline(api, args));

            api.ChatCommands
                .Create("wind")
                .WithDescription("Shows the current wind speed at your location")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => Wind(api, args));

            api.ChatCommands
                .Create("nextstorm")
                .WithDescription("Shows the next temporal storm information")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => NextStorm(api, args));
        }

        private static string GetWindCategory(float wind)
        {
            if (wind >= utilityConfig.HurricaneThreshold) return "Hurricane";
            if (wind >= utilityConfig.StormThreshold) return "Storm";
            if (wind >= utilityConfig.StrongWindThreshold) return "Strong Wind";
            if (wind >= utilityConfig.BreezyThreshold) return "Breezy";
            return "Calm";
        }

        private static TextCommandResult Wind(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;

            Vec3d windVec = api.World.BlockAccessor.GetWindSpeedAt(player.Entity.Pos.XYZ);
            float windStrength = (float)windVec.Length();

            string category = GetWindCategory(windStrength);

            player.SendMessage(
                GlobalConstants.GeneralChatGroup,
                $"Wind: {windStrength:0.00} ({category})",
                EnumChatType.Notification
            );

            return TextCommandResult.Success();
        }

        private static TextCommandResult WhosOnline(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            var players = api.World.AllOnlinePlayers;
            IServerPlayer caller = (IServerPlayer)args.Caller.Player;

            if (players == null || players.Length == 0)
            {
                caller.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    "No players online.",
                    EnumChatType.Notification
                );
                return TextCommandResult.Success();
            }

            string playerList = string.Join(", ",
                players
                    .Cast<IServerPlayer>()
                    .Select(p => $"{p.PlayerName} ({p.Ping}ms)")
            );

            caller.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                $"Online players ({players.Length}): {playerList}",
                EnumChatType.CommandSuccess
            );

            return TextCommandResult.Success();
        }

        private static TextCommandResult NextStorm(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;

            var tree = api.World.Config;

            double nextStart = tree.GetDouble("temporalStormNextStartTotalHours", -1);
            double duration = tree.GetDouble("temporalStormDurationHours", 0);

            if (nextStart < 0)
            {
                player.SendMessage(GlobalConstants.InfoLogChatGroup,
                    "Temporal storms are disabled.",
                    EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            double currentHours = api.World.Calendar.TotalHours;
            double hoursUntil = nextStart - currentHours;

            if (hoursUntil <= 0)
            {
                player.SendMessage(GlobalConstants.InfoLogChatGroup,
                    "A temporal storm is currently active.",
                    EnumChatType.Notification);
            }
            else
            {
                player.SendMessage(GlobalConstants.InfoLogChatGroup,
                    $"Next temporal storm in {hoursUntil:F1} hours.",
                    EnumChatType.Notification);
            }

            return TextCommandResult.Success();
        }
    }
}