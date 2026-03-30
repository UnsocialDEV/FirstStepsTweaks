using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Players
{
    public interface IPlayerPrivilegeMutator
    {
        void Grant(IServerPlayer player, string privilege);

        void Revoke(IServerPlayer player, string privilege);
    }
}
