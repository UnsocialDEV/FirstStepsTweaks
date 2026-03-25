using System;
using System.Security.Cryptography;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordLinkService
    {
        private const string CodeCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private readonly IDiscordLinkedAccountStore linkedAccountStore;
        private readonly IPendingDiscordLinkCodeStore pendingCodeStore;
        private readonly DiscordLinkCodeMessageParser parser;
        private readonly int linkCodeExpiryMinutes;

        public DiscordLinkService(
            IDiscordLinkedAccountStore linkedAccountStore,
            IPendingDiscordLinkCodeStore pendingCodeStore,
            DiscordLinkCodeMessageParser parser,
            int linkCodeExpiryMinutes)
        {
            this.linkedAccountStore = linkedAccountStore;
            this.pendingCodeStore = pendingCodeStore;
            this.parser = parser;
            this.linkCodeExpiryMinutes = Math.Max(1, linkCodeExpiryMinutes);
        }

        public string GetLinkedDiscordUserId(string playerUid)
        {
            return linkedAccountStore.GetLinkedDiscordUserId(playerUid);
        }

        public DiscordLinkCodeIssue CreateLinkCode(string playerUid, DateTime nowUtc)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                throw new ArgumentException("A player UID is required to create a Discord link code.", nameof(playerUid));
            }

            pendingCodeStore.RemoveCodesForPlayer(playerUid);

            string code = GenerateUniqueCode(nowUtc);
            DateTime expiresAtUtc = nowUtc.AddMinutes(linkCodeExpiryMinutes);

            pendingCodeStore.SaveCode(
                code,
                new PendingDiscordLinkCodeRecord(playerUid, expiresAtUtc.Ticks));

            return new DiscordLinkCodeIssue(code, expiresAtUtc);
        }

        public bool TryCompleteLink(string discordUserId, string content, DateTime nowUtc, out string playerUid)
        {
            playerUid = null;
            if (string.IsNullOrWhiteSpace(discordUserId))
            {
                return false;
            }

            if (!parser.TryParsePendingCode(content, pendingCodeStore.GetPendingCodes(nowUtc), out string code))
            {
                return false;
            }

            if (!pendingCodeStore.TryGetCode(code, nowUtc, out PendingDiscordLinkCodeRecord pendingCode))
            {
                return false;
            }

            linkedAccountStore.SetLinkedDiscordUserId(pendingCode.PlayerUid, discordUserId.Trim());
            pendingCodeStore.RemoveCodesForPlayer(pendingCode.PlayerUid);
            playerUid = pendingCode.PlayerUid;
            return true;
        }

        public void UnlinkPlayer(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return;
            }

            linkedAccountStore.ClearLinkedDiscordUserId(playerUid);
            pendingCodeStore.RemoveCodesForPlayer(playerUid);
        }

        private string GenerateUniqueCode(DateTime nowUtc)
        {
            for (int attempt = 0; attempt < 32; attempt++)
            {
                string code = GenerateCode();
                if (!pendingCodeStore.TryGetCode(code, nowUtc, out _))
                {
                    return code;
                }
            }

            throw new InvalidOperationException("Unable to allocate a unique Discord link code.");
        }

        private static string GenerateCode()
        {
            Span<char> code = stackalloc char[6];
            for (int index = 0; index < code.Length; index++)
            {
                code[index] = CodeCharacters[RandomNumberGenerator.GetInt32(CodeCharacters.Length)];
            }

            return new string(code);
        }
    }
}
