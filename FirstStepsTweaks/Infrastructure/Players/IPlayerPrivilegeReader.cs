using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Players
{
    public interface IPlayerPrivilegeReader
    {
        bool HasPrivilege(IServerPlayer player, string privilege);
    }
}
