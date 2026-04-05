using Vintagestory.API.MathTools;

namespace FirstStepsTweaks.Infrastructure.Teleport
{
    public sealed class RtpColumnSafetyScanResult
    {
        public Vec3d Destination { get; set; }

        public string FailureDetail { get; set; } = string.Empty;

        public RtpColumnSafetyFailureKind FailureKind { get; set; }

        public bool Success => Destination != null;
    }
}
