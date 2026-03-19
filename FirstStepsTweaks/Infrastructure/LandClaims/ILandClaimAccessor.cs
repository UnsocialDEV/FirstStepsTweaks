using Vintagestory.API.MathTools;

namespace FirstStepsTweaks.Infrastructure.LandClaims
{
    public interface ILandClaimAccessor
    {
        LandClaimInfo GetClaimAt(BlockPos pos);
    }
}
