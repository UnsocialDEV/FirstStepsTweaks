using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Teleport
{
    public sealed class PlayerTeleporter : IPlayerTeleporter
    {
        public void Teleport(IServerPlayer player, Vec3d destination)
        {
            player?.Entity?.TeleportToDouble(destination.X, destination.Y, destination.Z);
        }
    }
}
