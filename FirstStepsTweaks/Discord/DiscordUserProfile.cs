namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordUserProfile
    {
        public DiscordUserProfile(string id, string avatarHash)
        {
            Id = id;
            AvatarHash = avatarHash;
        }

        public string Id { get; }

        public string AvatarHash { get; }
    }
}
