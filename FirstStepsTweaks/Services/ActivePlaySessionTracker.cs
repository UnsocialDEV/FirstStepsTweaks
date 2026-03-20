using System;
using System.Collections.Generic;

namespace FirstStepsTweaks.Services
{
    public sealed class ActivePlaySessionTracker
    {
        private readonly Dictionary<string, DateTime> activeSessions = new Dictionary<string, DateTime>();

        public void StartSession(string playerUid, DateTime startedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return;
            }

            activeSessions[playerUid] = startedAtUtc;
        }

        public bool TryStopSession(string playerUid, DateTime endedAtUtc, out long elapsedSeconds)
        {
            elapsedSeconds = 0;

            if (string.IsNullOrWhiteSpace(playerUid) || !activeSessions.Remove(playerUid, out DateTime startedAtUtc))
            {
                return false;
            }

            TimeSpan elapsed = endedAtUtc - startedAtUtc;
            if (elapsed <= TimeSpan.Zero)
            {
                return true;
            }

            elapsedSeconds = (long)Math.Floor(elapsed.TotalSeconds);
            return true;
        }
    }
}
