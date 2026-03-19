using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Teleport
{
    public interface IBackLocationStore
    {
        void RecordCurrentLocation(IServerPlayer player);
        bool TryGet(string playerUid, out Vec3d location);
        void Set(string playerUid, Vec3d location);
    }
}
