using System.Threading.Tasks;

namespace FirstStepsTweaks.Discord
{
    public interface IDiscordMemberRoleClient
    {
        Task<DiscordMemberRoles> GetMemberRolesAsync(DiscordBridgeConfig config, string discordUserId);
    }
}
