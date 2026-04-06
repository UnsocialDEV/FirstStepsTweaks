using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Players
{
    public interface IPlayerRoleCodeReader
    {
        string Read(IPlayer player);

        string Read(IServerPlayer player);
    }
}
