using Vintagestory.API.MathTools;

namespace FirstStepsTweaks.Services
{
    public sealed class RtpDestinationResolutionResult
    {
        public Vec3d Destination { get; set; }

        public RtpSearchSession SearchSession { get; set; }

        public int PendingChunkCount { get; set; }

        public int UnsafeTerrainCount { get; set; }

        public int ClaimRejectedCount { get; set; }

        public string FailureReason { get; set; } = string.Empty;

        public bool Success => Destination != null;
    }
}
