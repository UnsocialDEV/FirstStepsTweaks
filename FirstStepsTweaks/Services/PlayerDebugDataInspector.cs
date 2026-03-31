using System.Collections.Generic;
using System.Linq;
using System.Text;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class PlayerDebugDataInspector
    {
        private readonly JoinHistoryStore joinHistoryStore;
        private readonly KitClaimStore kitClaimStore;
        private readonly PlayerPlaytimeStore playtimeStore;
        private readonly HomeStore homeStore;
        private readonly TpaPreferenceStore tpaPreferenceStore;

        public PlayerDebugDataInspector(
            JoinHistoryStore joinHistoryStore,
            KitClaimStore kitClaimStore,
            PlayerPlaytimeStore playtimeStore,
            HomeStore homeStore,
            TpaPreferenceStore tpaPreferenceStore)
        {
            this.joinHistoryStore = joinHistoryStore;
            this.kitClaimStore = kitClaimStore;
            this.playtimeStore = playtimeStore;
            this.homeStore = homeStore;
            this.tpaPreferenceStore = tpaPreferenceStore;
        }

        public PlayerDebugDataSnapshot Capture(IServerPlayer player)
        {
            return new PlayerDebugDataSnapshot
            {
                PlayerName = player?.PlayerName ?? string.Empty,
                PlayerUid = player?.PlayerUID ?? string.Empty,
                FirstJoinRecorded = joinHistoryStore.HasJoinedBefore(player),
                LastSeenTotalDays = joinHistoryStore.GetLastSeenTotalDays(player),
                StarterKitClaimed = kitClaimStore.HasStarterClaim(player),
                WinterKitClaimed = kitClaimStore.HasWinterClaim(player),
                TotalPlayedSeconds = playtimeStore.GetTotalPlayedSeconds(player),
                TpaDisabled = tpaPreferenceStore.IsDisabled(player),
                Homes = new Dictionary<string, HomeLocation>(homeStore.GetAll(player))
            };
        }

        public string Format(PlayerDebugDataSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Player data for {snapshot.PlayerName} ({snapshot.PlayerUid})");
            builder.AppendLine($"- firstJoinRecorded: {snapshot.FirstJoinRecorded}");
            builder.AppendLine($"- lastSeenTotalDays: {FormatNullableDouble(snapshot.LastSeenTotalDays)}");
            builder.AppendLine($"- starterKitClaimed: {snapshot.StarterKitClaimed}");
            builder.AppendLine($"- winterKitClaimed: {snapshot.WinterKitClaimed}");
            builder.AppendLine($"- totalPlayedSeconds: {snapshot.TotalPlayedSeconds}");
            builder.AppendLine($"- tpaDisabled: {snapshot.TpaDisabled}");
            builder.AppendLine($"- homes: {snapshot.Homes.Count}");

            foreach (KeyValuePair<string, HomeLocation> pair in snapshot.Homes.OrderBy(pair => pair.Key))
            {
                HomeLocation location = pair.Value;
                builder.AppendLine($"  - {pair.Key}: {location.X:0.##}, {location.Y:0.##}, {location.Z:0.##} (order={location.CreatedOrder})");
            }

            return builder.ToString().TrimEnd();
        }

        private static string FormatNullableDouble(double? value)
        {
            return value == null ? "unset" : value.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
