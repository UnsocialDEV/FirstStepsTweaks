using FirstStepsTweaks.Services;

namespace FirstStepsTweaks.Infrastructure.Teleport
{
    public interface IRtpColumnSafetyScanner
    {
        RtpColumnSafetyScanResult ScanCandidate(RtpChunkCandidate candidate);
    }
}
