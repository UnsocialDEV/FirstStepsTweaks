namespace FirstStepsTweaks.Discord
{
    public interface IDiscordLinkLastMessageStore
    {
        string Load();
        void Save(string lastMessageId);
        void Clear();
    }
}
