using Vintagestory.API.MathTools;

namespace FirstStepsTweaks.Infrastructure.Teleport
{
    public interface IRtpColumnSafetyScanner
    {
        Vec3d FindSafeDestination(int x, int z, int dimension);
    }
}
