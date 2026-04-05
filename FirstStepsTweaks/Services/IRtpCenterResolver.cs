using Vintagestory.API.MathTools;

namespace FirstStepsTweaks.Services
{
    public interface IRtpCenterResolver
    {
        Vec2d Resolve(Vec3d currentPosition);
    }
}
