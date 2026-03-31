using System.Collections.Generic;
using FirstStepsTweaks.Teleport;

namespace FirstStepsTweaks.Services
{
    public sealed class PlayerDebugDataSnapshot
    {
        public string PlayerName { get; set; } = string.Empty;

        public string PlayerUid { get; set; } = string.Empty;

        public bool FirstJoinRecorded { get; set; }

        public double? LastSeenTotalDays { get; set; }

        public bool StarterKitClaimed { get; set; }

        public bool WinterKitClaimed { get; set; }

        public bool SupporterKitClaimed { get; set; }

        public long TotalPlayedSeconds { get; set; }

        public bool TpaDisabled { get; set; }

        public IReadOnlyDictionary<string, HomeLocation> Homes { get; set; } = new Dictionary<string, HomeLocation>();
    }
}
