namespace FirstStepsTweaks.Discord
{
    public interface IDiscordLastMessageStore
    {
        string Load();
        void Save(string lastMessageId);
    }
}
