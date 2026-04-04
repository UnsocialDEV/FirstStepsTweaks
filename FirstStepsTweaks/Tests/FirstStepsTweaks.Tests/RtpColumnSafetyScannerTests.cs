using FirstStepsTweaks.Infrastructure.Teleport;
using Vintagestory.API.MathTools;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class RtpColumnSafetyScannerTests
{
    [Fact]
    public void FindSafeDestination_AllowsFlatWorldTerrainHeightOfOne()
    {
        Vec3d destination = RtpColumnSafetyScanner.FindSafeDestination(
            x: 25,
            z: -40,
            dimension: 0,
            terrainHeight: 1,
            isPassableTeleportSpace: pos => pos.Y >= 2,
            isSafeTeleportGround: pos => pos.Y == 1);

        Assert.NotNull(destination);
        Assert.Equal(25.5, destination.X);
        Assert.Equal(2, destination.Y);
        Assert.Equal(-39.5, destination.Z);
    }

    [Fact]
    public void FindSafeDestination_ReturnsNull_WhenNoSafeGroundExists()
    {
        Vec3d destination = RtpColumnSafetyScanner.FindSafeDestination(
            x: 25,
            z: -40,
            dimension: 0,
            terrainHeight: 1,
            isPassableTeleportSpace: _ => true,
            isSafeTeleportGround: _ => false);

        Assert.Null(destination);
    }
}
