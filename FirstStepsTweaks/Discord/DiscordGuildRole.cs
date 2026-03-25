namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordGuildRole
    {
        public DiscordGuildRole(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; }

        public string Name { get; }
    }
}
