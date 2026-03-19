namespace FirstStepsTweaks.Discord.Transport
{
    public sealed class DiscordHttpResponse
    {
        public int StatusCode { get; set; }
        public string Body { get; set; } = string.Empty;
    }
}
