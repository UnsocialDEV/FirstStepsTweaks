using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Players
{
    public interface IPlayerRoleCodeReader
    {
        string Read(IServerPlayer player);
    }
}
