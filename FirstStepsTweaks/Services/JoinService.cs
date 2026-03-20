using System;
using FirstStepsTweaks.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class JoinService
    {
        private readonly ICoreServerAPI api;
        private readonly JoinConfig joinConfig;
        private readonly bool enableJoinBroadcasts;
        private readonly JoinMessageFormatter formatter;
        private readonly JoinHistoryStore joinHistoryStore;
        private readonly PlayerPlaytimeStore playtimeStore;
        private readonly ActivePlaySessionTracker activePlaySessionTracker;
        private readonly PlaytimeFormatter playtimeFormatter;

        public JoinService(ICoreServerAPI api, FirstStepsTweaksConfig config)
            : this(
                api,
                config,
                new JoinMessageFormatter(),
                new JoinHistoryStore(),
                new PlayerPlaytimeStore(),
                new ActivePlaySessionTracker(),
                new PlaytimeFormatter())
        {
        }

        public JoinService(
            ICoreServerAPI api,
            FirstStepsTweaksConfig config,
            JoinMessageFormatter formatter,
            JoinHistoryStore joinHistoryStore,
            PlayerPlaytimeStore playtimeStore,
            ActivePlaySessionTracker activePlaySessionTracker,
            PlaytimeFormatter playtimeFormatter)
        {
            this.api = api;
            joinConfig = config?.Join ?? new JoinConfig();
            enableJoinBroadcasts = config?.Features?.EnableJoinBroadcasts ?? true;
            this.formatter = formatter;
            this.joinHistoryStore = joinHistoryStore;
            this.playtimeStore = playtimeStore;
            this.activePlaySessionTracker = activePlaySessionTracker;
            this.playtimeFormatter = playtimeFormatter;
        }

        public void OnPlayerNowPlaying(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            activePlaySessionTracker.StartSession(player.PlayerUID, DateTime.UtcNow);
            double currentTotalDays = api.World.Calendar.TotalDays;

            if (!joinHistoryStore.HasJoinedBefore(player))
            {
                joinHistoryStore.MarkFirstJoinRecorded(player);

                if (enableJoinBroadcasts)
                {
                    api.BroadcastMessageToAllGroups(
                        formatter.FormatFirstJoin(joinConfig.FirstJoinMessage, player.PlayerName),
                        EnumChatType.AllGroups
                    );
                }

                return;
            }

            if (!enableJoinBroadcasts)
            {
                return;
            }

            int daysSinceLastSeen = joinHistoryStore.GetDaysSinceLastSeen(player, currentTotalDays);
            long totalPlayedSeconds = playtimeStore.GetTotalPlayedSeconds(player);
            string formattedPlaytime = playtimeFormatter.FormatHours(totalPlayedSeconds);

            api.BroadcastMessageToAllGroups(
                formatter.FormatReturningJoin(joinConfig.ReturningJoinMessage, player.PlayerName, daysSinceLastSeen, formattedPlaytime),
                EnumChatType.AllGroups
            );
        }

        public void OnPlayerLeave(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            if (activePlaySessionTracker.TryStopSession(player.PlayerUID, DateTime.UtcNow, out long playedSeconds))
            {
                playtimeStore.AddPlayedSeconds(player, playedSeconds);
            }

            joinHistoryStore.RecordLastSeenDay(player, api.World.Calendar.TotalDays);
        }
    }
}
