using System;
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
        private const string LastSeenDayKey = "fst_lastseenday";

        public JoinService(ICoreServerAPI api, FirstStepsTweaksConfig config)
        {
            this.api = api;
            joinConfig = config?.Join ?? new JoinConfig();
        }

        public void OnPlayerJoin(IServerPlayer player)
        {
            byte[] data = player.GetModdata(FirstJoinKey);
            double currentTotalDays = api.World.Calendar.TotalDays;

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
                int daysSinceLastSeen = GetDaysSinceLastSeen(player, currentTotalDays);

                api.BroadcastMessageToAllGroups(
                    joinConfig.ReturningJoinMessage
                        .Replace("{player}", player.PlayerName)
                        .Replace("{days}", daysSinceLastSeen.ToString()),
                    EnumChatType.AllGroups
                );
            }

            player.SetModdata(LastSeenDayKey, BitConverter.GetBytes(currentTotalDays));
        }

        private int GetDaysSinceLastSeen(IServerPlayer player, double currentTotalDays)
        {
            byte[] lastSeenData = player.GetModdata(LastSeenDayKey);

            if (lastSeenData == null || lastSeenData.Length != sizeof(double))
            {
                return 0;
            }

            double lastSeenTotalDays = BitConverter.ToDouble(lastSeenData, 0);
            double elapsedDays = currentTotalDays - lastSeenTotalDays;

            return Math.Max(0, (int)Math.Floor(elapsedDays));
        }
    }
}
