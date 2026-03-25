namespace FirstStepsTweaks.Discord
{
    public interface IDiscordLinkedAccountStore
    {
        string GetLinkedDiscordUserId(string playerUid);
        void SetLinkedDiscordUserId(string playerUid, string discordUserId);
        void ClearLinkedDiscordUserId(string playerUid);
    }
}
