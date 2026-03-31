using System;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class PlayerPlaytimeStore
    {
        private const string TotalPlayedSecondsKey = "fst_playtimeseconds";

        public long GetTotalPlayedSeconds(IServerPlayer player)
        {
            byte[] data = player?.GetModdata(TotalPlayedSecondsKey);
            if (data == null || data.Length != sizeof(long))
            {
                return 0;
            }

            long totalPlayedSeconds = BitConverter.ToInt64(data, 0);
            return Math.Max(0, totalPlayedSeconds);
        }

        public void AddPlayedSeconds(IServerPlayer player, long playedSeconds)
        {
            if (player == null || playedSeconds <= 0)
            {
                return;
            }

            long totalPlayedSeconds = GetTotalPlayedSeconds(player);
            long updatedTotal = long.MaxValue - totalPlayedSeconds < playedSeconds
                ? long.MaxValue
                : totalPlayedSeconds + playedSeconds;

            player.SetModdata(TotalPlayedSecondsKey, BitConverter.GetBytes(updatedTotal));
        }

        public void SetTotalPlayedSeconds(IServerPlayer player, long totalPlayedSeconds)
        {
            if (player == null)
            {
                return;
            }

            long clampedTotal = Math.Max(0, totalPlayedSeconds);
            player.SetModdata(TotalPlayedSecondsKey, BitConverter.GetBytes(clampedTotal));
        }

        public void ResetTotalPlayedSeconds(IServerPlayer player)
        {
            player?.SetModdata(TotalPlayedSecondsKey, null);
        }
    }
}
