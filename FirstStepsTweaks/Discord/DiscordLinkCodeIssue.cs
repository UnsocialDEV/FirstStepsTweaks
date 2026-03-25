using System;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordLinkCodeIssue
    {
        public DiscordLinkCodeIssue(string code, DateTime expiresAtUtc)
        {
            Code = code;
            ExpiresAtUtc = expiresAtUtc;
        }

        public string Code { get; }

        public DateTime ExpiresAtUtc { get; }
    }
}
