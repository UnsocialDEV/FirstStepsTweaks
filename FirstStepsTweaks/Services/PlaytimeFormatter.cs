using System;
using System.Globalization;

namespace FirstStepsTweaks.Services
{
    public sealed class PlaytimeFormatter
    {
        public string FormatHours(long totalPlayedSeconds)
        {
            double totalHours = Math.Max(0L, totalPlayedSeconds) / 3600d;
            return totalHours.ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
