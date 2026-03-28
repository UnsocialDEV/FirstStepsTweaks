using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FirstStepsTweaks.Discord.Transport;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordMemberRoleClient : IDiscordMemberRoleClient
    {
        private const int MaxAttempts = 2;
        private readonly IDiscordWebhookClient webhookClient;

        public DiscordMemberRoleClient(IDiscordWebhookClient webhookClient)
        {
            this.webhookClient = webhookClient;
        }

        public async Task<DiscordMemberRoles> GetMemberRolesAsync(DiscordBridgeConfig config, string discordUserId)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (string.IsNullOrWhiteSpace(config.BotToken)
                || string.IsNullOrWhiteSpace(config.GuildId)
                || string.IsNullOrWhiteSpace(discordUserId))
            {
                return new DiscordMemberRoles(Array.Empty<string>(), Array.Empty<DiscordGuildRole>());
            }

            DiscordHttpResponse memberResponse = await GetWithRetryAsync(
                $"https://discord.com/api/v10/guilds/{config.GuildId}/members/{discordUserId}",
                config.BotToken);

            if (memberResponse.StatusCode == 404)
            {
                return new DiscordMemberRoles(Array.Empty<string>(), Array.Empty<DiscordGuildRole>());
            }

            EnsureSuccess(memberResponse, "guild member");

            DiscordHttpResponse roleResponse = await GetWithRetryAsync(
                $"https://discord.com/api/v10/guilds/{config.GuildId}/roles",
                config.BotToken);

            EnsureSuccess(roleResponse, "guild roles");

            return new DiscordMemberRoles(
                ParseMemberRoleIds(memberResponse.Body),
                ParseGuildRoles(roleResponse.Body));
        }

        private static void EnsureSuccess(DiscordHttpResponse response, string operationName)
        {
            if (response.StatusCode >= 200 && response.StatusCode < 300)
            {
                return;
            }

            throw new InvalidOperationException($"Discord {operationName} request returned status code {response.StatusCode}.");
        }

        private async Task<DiscordHttpResponse> GetWithRetryAsync(string url, string botToken)
        {
            DiscordHttpResponse lastResponse = null;

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                lastResponse = await webhookClient.GetAsync(url, botToken);
                if (!IsTransientFailure(lastResponse.StatusCode) || attempt == MaxAttempts - 1)
                {
                    return lastResponse;
                }

                await Task.Delay(500);
            }

            return lastResponse;
        }

        private static bool IsTransientFailure(int statusCode)
        {
            return statusCode == 429 || statusCode == 502 || statusCode == 503 || statusCode == 504;
        }

        private static IReadOnlyCollection<string> ParseMemberRoleIds(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("roles", out JsonElement rolesElement) || rolesElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var roleIds = new List<string>();
            foreach (JsonElement roleElement in rolesElement.EnumerateArray())
            {
                string roleId = roleElement.GetString();
                if (!string.IsNullOrWhiteSpace(roleId))
                {
                    roleIds.Add(roleId);
                }
            }

            return roleIds;
        }

        private static IReadOnlyCollection<DiscordGuildRole> ParseGuildRoles(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<DiscordGuildRole>();
            }

            var roles = new List<DiscordGuildRole>();
            foreach (JsonElement roleElement in document.RootElement.EnumerateArray())
            {
                string roleId = roleElement.TryGetProperty("id", out JsonElement idElement) ? idElement.GetString() : null;
                string roleName = roleElement.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() : null;

                if (string.IsNullOrWhiteSpace(roleId) || string.IsNullOrWhiteSpace(roleName))
                {
                    continue;
                }

                roles.Add(new DiscordGuildRole(roleId, roleName));
            }

            return roles;
        }
    }
}
