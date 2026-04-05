using System.Collections.Generic;

namespace FirstStepsTweaks.Services
{
    public interface IRtpColumnPlanner
    {
        IReadOnlyList<RtpChunkCandidate> PlanColumns(double centerX, double centerZ, int dimension);
    }
}
