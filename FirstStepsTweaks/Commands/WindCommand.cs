using FirstStepsTweaks.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class WindCommand
    {
        private readonly ICoreServerAPI api;
        private readonly UtilityConfig utilityConfig;

        public WindCommand(ICoreServerAPI api, FirstStepsTweaksConfig config)
        {
            this.api = api;
            utilityConfig = config?.Utility ?? new UtilityConfig();
        }

        public void Register()
        {
            api.ChatCommands
                .Create("wind")
                .WithDescription("Shows the current wind speed at your location")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(Wind);
        }

        private string GetWindCategory(float wind)
        {
            if (wind >= utilityConfig.HurricaneThreshold) return "Hurricane";
            if (wind >= utilityConfig.StormThreshold) return "Storm";
            if (wind >= utilityConfig.StrongWindThreshold) return "Strong Wind";
            if (wind >= utilityConfig.BreezyThreshold) return "Breezy";
            return "Calm";
        }

        private TextCommandResult Wind(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            Vec3d windVec = api.World.BlockAccessor.GetWindSpeedAt(player.Entity.Pos.XYZ);
            float windStrength = (float)windVec.Length();
            string category = GetWindCategory(windStrength);

            player.SendMessage(GlobalConstants.InfoLogChatGroup, $"Wind: {windStrength:0.00} ({category})", EnumChatType.CommandSuccess);
            player.SendMessage(GlobalConstants.GeneralChatGroup, $"Wind: {windStrength:0.00} ({category})", EnumChatType.Notification);
            return TextCommandResult.Success();
        }
    }
}
