using System.Collections.Generic;

namespace FirstStepsTweaks.Teleport
{
    public sealed class RtpCooldownStore
    {
        private readonly Dictionary<string, long> lastRtpByPlayerUid = new Dictionary<string, long>();

        public bool TryGetLastUse(string playerUid, out long lastUseMs)
        {
            return lastRtpByPlayerUid.TryGetValue(playerUid, out lastUseMs);
        }

        public void SetLastUse(string playerUid, long useMs)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return;
            }

            lastRtpByPlayerUid[playerUid] = useMs;
        }
    }
}
