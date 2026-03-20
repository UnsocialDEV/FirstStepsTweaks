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
    }
}
