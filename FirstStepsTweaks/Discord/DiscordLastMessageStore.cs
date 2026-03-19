using Vintagestory.API.Server;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordLastMessageStore
    {
        private const string DiscordLastIdKey = "fst_discord_lastmsgid";
        private readonly ICoreServerAPI api;

        public DiscordLastMessageStore(ICoreServerAPI api)
        {
            this.api = api;
        }

        public string Load()
        {
            return api.WorldManager.SaveGame.GetData<string>(DiscordLastIdKey);
        }

        public void Save(string lastMessageId)
        {
            api.WorldManager.SaveGame.StoreData(DiscordLastIdKey, lastMessageId);
        }
    }
}
