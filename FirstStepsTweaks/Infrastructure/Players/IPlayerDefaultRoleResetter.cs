using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Players
{
    public interface IPlayerDefaultRoleResetter
    {
        void Reset(IServerPlayer player);
        string GetDefaultRoleCode();
    }
}
