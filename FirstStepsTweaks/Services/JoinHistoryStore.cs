using System;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class JoinHistoryStore
    {
        private const string FirstJoinKey = "fst_firstjoin";
        private const string LastSeenDayKey = "fst_lastseenday";

        public bool HasJoinedBefore(IServerPlayer player)
        {
            return player?.GetModdata(FirstJoinKey) != null;
        }

        public void MarkFirstJoinRecorded(IServerPlayer player)
        {
            player?.SetModdata(FirstJoinKey, new byte[] { 1 });
        }

        public void SetFirstJoinRecorded(IServerPlayer player, bool value)
        {
            if (value)
            {
                MarkFirstJoinRecorded(player);
                return;
            }

            player?.SetModdata(FirstJoinKey, null);
        }

        public int GetDaysSinceLastSeen(IServerPlayer player, double currentTotalDays)
        {
            byte[] lastSeenData = player?.GetModdata(LastSeenDayKey);
            if (lastSeenData == null || lastSeenData.Length != sizeof(double))
            {
                return 0;
            }

            double lastSeenTotalDays = BitConverter.ToDouble(lastSeenData, 0);
            return Math.Max(0, (int)Math.Floor(currentTotalDays - lastSeenTotalDays));
        }

        public void RecordLastSeenDay(IServerPlayer player, double currentTotalDays)
        {
            player?.SetModdata(LastSeenDayKey, BitConverter.GetBytes(currentTotalDays));
        }

        public double? GetLastSeenTotalDays(IServerPlayer player)
        {
            byte[] lastSeenData = player?.GetModdata(LastSeenDayKey);
            if (lastSeenData == null || lastSeenData.Length != sizeof(double))
            {
                return null;
            }

            return BitConverter.ToDouble(lastSeenData, 0);
        }

        public void SetLastSeenDay(IServerPlayer player, double? totalDays)
        {
            if (totalDays == null)
            {
                player?.SetModdata(LastSeenDayKey, null);
                return;
            }

            player?.SetModdata(LastSeenDayKey, BitConverter.GetBytes(totalDays.Value));
        }
    }
}
