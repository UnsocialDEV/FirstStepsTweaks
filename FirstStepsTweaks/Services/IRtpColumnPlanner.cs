using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace FirstStepsTweaks.Services
{
    public interface IRtpColumnPlanner
    {
        IReadOnlyList<BlockPos> PlanColumns(double centerX, double centerZ, int dimension);
    }
}
