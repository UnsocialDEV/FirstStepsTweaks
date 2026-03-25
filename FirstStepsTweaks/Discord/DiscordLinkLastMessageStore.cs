using Vintagestory.API.Server;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordLinkLastMessageStore
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
    }
}
