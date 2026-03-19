using System;
using FirstStepsTweaks.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class JoinService
    {
        private const string FirstJoinKey = "fst_firstjoin";
        private const string LastSeenDayKey = "fst_lastseenday";

        private readonly ICoreServerAPI api;
        private readonly JoinConfig joinConfig;
        private readonly bool enableJoinBroadcasts;
        private readonly JoinMessageFormatter formatter;

        public JoinService(ICoreServerAPI api, FirstStepsTweaksConfig config)
            : this(api, config, new JoinMessageFormatter())
        {
        }

        public JoinService(ICoreServerAPI api, FirstStepsTweaksConfig config, JoinMessageFormatter formatter)
        {
            this.api = api;
            joinConfig = config?.Join ?? new JoinConfig();
            enableJoinBroadcasts = config?.Features?.EnableJoinBroadcasts ?? true;
            this.formatter = formatter;
        }

        public void OnPlayerNowPlaying(IServerPlayer player)
        {
            if (player == null || !enableJoinBroadcasts)
            {
                return;
            }

            byte[] data = player.GetModdata(FirstJoinKey);
            double currentTotalDays = api.World.Calendar.TotalDays;

            if (data == null)
            {
                api.BroadcastMessageToAllGroups(
                    formatter.FormatFirstJoin(joinConfig.FirstJoinMessage, player.PlayerName),
                    EnumChatType.AllGroups
                );

                player.SetModdata(FirstJoinKey, new byte[] { 1 });
                return;
            }

            int daysSinceLastSeen = GetDaysSinceLastSeen(player, currentTotalDays);
            api.BroadcastMessageToAllGroups(
                formatter.FormatReturningJoin(joinConfig.ReturningJoinMessage, player.PlayerName, daysSinceLastSeen),
                EnumChatType.AllGroups
            );
        }

        public void OnPlayerLeave(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            player.SetModdata(LastSeenDayKey, BitConverter.GetBytes(api.World.Calendar.TotalDays));
        }

        private int GetDaysSinceLastSeen(IServerPlayer player, double currentTotalDays)
        {
            byte[] lastSeenData = player.GetModdata(LastSeenDayKey);
            if (lastSeenData == null || lastSeenData.Length != sizeof(double))
            {
                return 0;
            }

            double lastSeenTotalDays = BitConverter.ToDouble(lastSeenData, 0);
            return Math.Max(0, (int)Math.Floor(currentTotalDays - lastSeenTotalDays));
        }
    }
}
