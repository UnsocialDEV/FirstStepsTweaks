using System.Collections.Generic;

namespace FirstStepsTweaks.Discord
{
    public interface IDiscordLinkedAccountStore
    {
        string GetLinkedDiscordUserId(string playerUid);
        IReadOnlyDictionary<string, string> GetAllLinkedDiscordUserIds();
        void SetLinkedDiscordUserId(string playerUid, string discordUserId);
        void ClearLinkedDiscordUserId(string playerUid);
    }
}
