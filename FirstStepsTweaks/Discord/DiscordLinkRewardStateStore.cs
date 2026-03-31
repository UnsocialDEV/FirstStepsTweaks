using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordLinkRewardStateStore : IDiscordLinkRewardStateStore
    {
        private const string ClaimedRewardsKey = "fst_discordlinkreward_claimed";
        private const string PendingRewardsKey = "fst_discordlinkreward_pending";
        private readonly ICoreServerAPI api;

        public DiscordLinkRewardStateStore(ICoreServerAPI api)
        {
            this.api = api;
        }

        public bool HasClaimed(string playerUid)
        {
            return !string.IsNullOrWhiteSpace(playerUid) && LoadSet(ClaimedRewardsKey).Contains(playerUid);
        }

        public void MarkClaimed(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return;
            }

            HashSet<string> claimed = LoadSet(ClaimedRewardsKey);
            if (!claimed.Add(playerUid))
            {
                return;
            }

            SaveSet(ClaimedRewardsKey, claimed);
        }

        public void ClearClaimed(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return;
            }

            HashSet<string> claimed = LoadSet(ClaimedRewardsKey);
            if (!claimed.Remove(playerUid))
            {
                return;
            }

            SaveSet(ClaimedRewardsKey, claimed);
        }

        public bool HasPendingReward(string playerUid)
        {
            return !string.IsNullOrWhiteSpace(playerUid) && LoadSet(PendingRewardsKey).Contains(playerUid);
        }

        public void MarkPendingReward(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return;
            }

            HashSet<string> pending = LoadSet(PendingRewardsKey);
            if (!pending.Add(playerUid))
            {
                return;
            }

            SaveSet(PendingRewardsKey, pending);
        }

        public void ClearPendingReward(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return;
            }

            HashSet<string> pending = LoadSet(PendingRewardsKey);
            if (!pending.Remove(playerUid))
            {
                return;
            }

            SaveSet(PendingRewardsKey, pending);
        }

        public IReadOnlyCollection<string> GetClaimedPlayerUids()
        {
            return LoadSet(ClaimedRewardsKey).ToArray();
        }

        public IReadOnlyCollection<string> GetPendingRewardPlayerUids()
        {
            return LoadSet(PendingRewardsKey).ToArray();
        }

        private HashSet<string> LoadSet(string key)
        {
            string[] values = api.WorldManager.SaveGame.GetData<string[]>(key) ?? Array.Empty<string>();
            return new HashSet<string>(
                values.Where(value => !string.IsNullOrWhiteSpace(value)),
                StringComparer.OrdinalIgnoreCase);
        }

        private void SaveSet(string key, HashSet<string> values)
        {
            api.WorldManager.SaveGame.StoreData(key, values.ToArray());
        }
    }
}
