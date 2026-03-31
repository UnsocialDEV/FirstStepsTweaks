using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Players
{
    public sealed class PlayerPrivilegeReader : IPlayerPrivilegeReader
    {
        public bool HasPrivilege(IServerPlayer player, string privilege)
        {
            return player?.HasPrivilege(privilege) == true;
        }
    }
}
