using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System;
using System.Threading.Tasks;

namespace FirstStepsTweaks.Discord.Transport
{
    public sealed class DiscordWebhookClient : IDiscordWebhookClient
    {
        internal const int RequestTimeoutSeconds = 10;
        private static readonly HttpClient http = new HttpClient();

        static DiscordWebhookClient()
        {
            http.Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds);
        }

        public async Task PostJsonAsync(string url, string json)
        {
            using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            await http.PostAsync(url, httpContent);
        }

        public async Task<DiscordHttpResponse> PostBotJsonAsync(string url, string botToken, string json)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await http.SendAsync(request);
            return new DiscordHttpResponse
            {
                StatusCode = (int)response.StatusCode,
                Body = await response.Content.ReadAsStringAsync()
            };
        }

        public async Task<DiscordHttpResponse> GetAsync(string url, string botToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);

            using var response = await http.SendAsync(request);
            return new DiscordHttpResponse
            {
                StatusCode = (int)response.StatusCode,
                Body = await response.Content.ReadAsStringAsync()
            };
        }
    }
}
