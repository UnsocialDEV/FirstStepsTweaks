namespace FirstStepsTweaks.Discord
{
    public sealed class PendingDiscordLinkCodeRecord
    {
        public PendingDiscordLinkCodeRecord()
        {
        }

        public PendingDiscordLinkCodeRecord(string playerUid, long expiresAtUtcTicks)
        {
            PlayerUid = playerUid;
            ExpiresAtUtcTicks = expiresAtUtcTicks;
        }

        public string PlayerUid { get; set; } = string.Empty;

        public long ExpiresAtUtcTicks { get; set; }
    }
}
