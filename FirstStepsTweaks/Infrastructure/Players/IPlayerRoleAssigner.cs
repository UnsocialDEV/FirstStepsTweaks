using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Players
{
    public interface IPlayerRoleAssigner
    {
        void Assign(IServerPlayer player, string roleCode);
    }
}
