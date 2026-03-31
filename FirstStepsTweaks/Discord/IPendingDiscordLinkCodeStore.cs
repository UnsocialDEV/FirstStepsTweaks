using System;
using System.Collections.Generic;

namespace FirstStepsTweaks.Discord
{
    public interface IPendingDiscordLinkCodeStore
    {
        IReadOnlyCollection<string> GetPendingCodes(DateTime nowUtc);
        IReadOnlyDictionary<string, PendingDiscordLinkCodeRecord> GetPendingCodeRecords(DateTime nowUtc);
        bool TryGetCode(string code, DateTime nowUtc, out PendingDiscordLinkCodeRecord record);
        void SaveCode(string code, PendingDiscordLinkCodeRecord record);
        void RemoveCode(string code);
        void RemoveCodesForPlayer(string playerUid);
    }
}
