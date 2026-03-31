using Vintagestory.API.Server;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordLinkLastMessageStore : IDiscordLinkLastMessageStore
    {
        private const string DiscordLinkLastIdKey = "fst_discord_lastlinkmsgid";
        private readonly ICoreServerAPI api;

        public DiscordLinkLastMessageStore(ICoreServerAPI api)
        {
            this.api = api;
        }

        public string Load()
        {
            return api.WorldManager.SaveGame.GetData<string>(DiscordLinkLastIdKey);
        }

        public void Save(string lastMessageId)
        {
            api.WorldManager.SaveGame.StoreData(DiscordLinkLastIdKey, lastMessageId);
        }

        public void Clear()
        {
            api.WorldManager.SaveGame.StoreData(DiscordLinkLastIdKey, (string)null);
        }
    }
}
