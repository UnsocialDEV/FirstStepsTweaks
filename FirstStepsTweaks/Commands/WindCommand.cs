using FirstStepsTweaks.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public class WindCommand
    {
        private static UtilityConfig utilityConfig = new UtilityConfig();

        public static void Register(ICoreServerAPI api, FirstStepsTweaksConfig config)
        {
            utilityConfig = config?.Utility ?? new UtilityConfig();

            api.ChatCommands
                .Create("wind")
                .WithDescription("Shows the current wind speed at your location")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => Wind(api, args));
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
                GlobalConstants.InfoLogChatGroup,
                $"Wind: {windStrength:0.00} ({category})",
                EnumChatType.CommandSuccess
            );

            player.SendMessage(
                GlobalConstants.GeneralChatGroup,
                $"Wind: {windStrength:0.00} ({category})",
                EnumChatType.Notification
            );

            return TextCommandResult.Success();
        }
    }
}