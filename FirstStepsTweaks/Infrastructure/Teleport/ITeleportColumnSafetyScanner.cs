using Vintagestory.API.MathTools;

namespace FirstStepsTweaks.Infrastructure.Teleport
{
    public interface ITeleportColumnSafetyScanner
    {
        Vec3d FindSafeDestination(int x, int z, int referenceY, int dimension);
    }
}
