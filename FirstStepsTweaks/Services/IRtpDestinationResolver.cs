using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public interface IRtpDestinationResolver
    {
        bool TryResolveDestination(IServerPlayer player, out Vec3d destination);
    }
}
