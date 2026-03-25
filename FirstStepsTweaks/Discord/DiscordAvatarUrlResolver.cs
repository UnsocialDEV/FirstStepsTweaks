namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordAvatarUrlResolver
    {
        public string ResolveGlobalAvatarUrl(DiscordUserProfile profile)
        {
            if (profile == null
                || string.IsNullOrWhiteSpace(profile.Id)
                || string.IsNullOrWhiteSpace(profile.AvatarHash))
            {
                return null;
            }

            string extension = profile.AvatarHash.StartsWith("a_")
                ? "gif"
                : "png";

            return $"https://cdn.discordapp.com/avatars/{profile.Id}/{profile.AvatarHash}.{extension}?size=128";
        }
    }
}
