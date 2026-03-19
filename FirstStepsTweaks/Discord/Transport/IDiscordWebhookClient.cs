using System.Threading.Tasks;

namespace FirstStepsTweaks.Discord.Transport
{
    public interface IDiscordWebhookClient
    {
        Task PostJsonAsync(string url, string json);
        Task<DiscordHttpResponse> GetAsync(string url, string botToken);
    }
}
