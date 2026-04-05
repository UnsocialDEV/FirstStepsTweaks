using FirstStepsTweaks.Infrastructure.Teleport;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class RtpColumnSafetyScannerTests
{
    [Fact]
    public void FindSafeDestination_FindsGroundWellBelowScanStart()
    {
        RtpColumnSafetyScanResult result = RtpColumnSafetyScanner.FindSafeDestination(
            x: 25,
            z: -40,
            dimension: 0,
            scanStartY: 80,
            scanEndY: 16,
            isPassableTeleportSpace: pos => pos.Y >= 41,
            isSafeTeleportGround: pos => pos.Y == 40);

        Assert.True(result.Success);
        Assert.NotNull(result.Destination);
        Assert.Equal(25.5, result.Destination.X);
        Assert.Equal(41, result.Destination.Y);
        Assert.Equal(-39.5, result.Destination.Z);
    }

    [Fact]
    public void FindSafeDestination_ReturnsUnsafeFailure_WhenNoSafeGroundExists()
    {
        RtpColumnSafetyScanResult result = RtpColumnSafetyScanner.FindSafeDestination(
            x: 25,
            z: -40,
            dimension: 0,
            scanStartY: 20,
            scanEndY: 2,
            isPassableTeleportSpace: _ => true,
            isSafeTeleportGround: _ => false);

        Assert.False(result.Success);
        Assert.Equal(RtpColumnSafetyFailureKind.UnsafeTerrain, result.FailureKind);
        Assert.Contains("scanRange=", result.FailureDetail);
    }
}
