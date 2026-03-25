using System;
using System.Collections.Generic;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordLinkedAccountStore : IDiscordLinkedAccountStore
    {
        private const string LinkedAccountDataKey = "fst_discord_links";
        private readonly ICoreServerAPI api;

        public DiscordLinkedAccountStore(ICoreServerAPI api)
        {
            this.api = api;
        }

        public string GetLinkedDiscordUserId(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return null;
            }

            Dictionary<string, string> links = LoadLinks();
            return links.TryGetValue(playerUid, out string discordUserId) ? discordUserId : null;
        }

        public void SetLinkedDiscordUserId(string playerUid, string discordUserId)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || string.IsNullOrWhiteSpace(discordUserId))
            {
                return;
            }

            Dictionary<string, string> links = LoadLinks();
            links[playerUid] = discordUserId.Trim();
            SaveLinks(links);
        }

        public void ClearLinkedDiscordUserId(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return;
            }

            Dictionary<string, string> links = LoadLinks();
            if (!links.Remove(playerUid))
            {
                return;
            }

            SaveLinks(links);
        }

        private Dictionary<string, string> LoadLinks()
        {
            return api.WorldManager.SaveGame.GetData<Dictionary<string, string>>(LinkedAccountDataKey)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private void SaveLinks(Dictionary<string, string> links)
        {
            api.WorldManager.SaveGame.StoreData(LinkedAccountDataKey, links);
        }
    }
}
