using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Teleport
{
    public interface IPlayerTeleporter
    {
        void Teleport(IServerPlayer player, Vec3d destination);
    }
}
