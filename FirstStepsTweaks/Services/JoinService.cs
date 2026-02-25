using FirstStepsTweaks.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public class JoinService
    {
        private readonly ICoreServerAPI api;
        private readonly JoinConfig joinConfig;

        private const string FirstJoinKey = "fst_firstjoin";

        public JoinService(ICoreServerAPI api, FirstStepsTweaksConfig config)
        {
            this.api = api;
            joinConfig = config?.Join ?? new JoinConfig();
        }

        public void OnPlayerJoin(IServerPlayer player)
        {
            byte[] data = player.GetModdata(FirstJoinKey);

            if (data == null)
            {
                api.BroadcastMessageToAllGroups(
                    joinConfig.FirstJoinMessage.Replace("{player}", player.PlayerName),
                    EnumChatType.AllGroups
                );

                player.SetModdata(FirstJoinKey, new byte[] { 1 });
            }
            else
            {
                api.BroadcastMessageToAllGroups(
                    joinConfig.ReturningJoinMessage.Replace("{player}", player.PlayerName),
                    EnumChatType.AllGroups
                );
            }
        }
    }
}
