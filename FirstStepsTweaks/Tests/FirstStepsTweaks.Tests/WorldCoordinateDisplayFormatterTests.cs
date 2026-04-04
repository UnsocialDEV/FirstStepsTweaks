using System.Reflection;
using FirstStepsTweaks.Infrastructure.Coordinates;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class WorldCoordinateDisplayFormatterTests
{
    [Fact]
    public void ToDisplayPosition_SubtractsWorldCenterOffset_ForBlockCoordinates()
    {
        var formatter = new WorldCoordinateDisplayFormatter(TestCoreServerApiFactory.Create(1024000, 1024000));

        var displayPosition = formatter.ToDisplayPosition(new Vintagestory.API.MathTools.BlockPos(512009, 3, 512146, 0));

        Assert.NotNull(displayPosition);
        Assert.Equal(9, displayPosition.X);
        Assert.Equal(3, displayPosition.Y);
        Assert.Equal(146, displayPosition.Z);
        Assert.Equal(0, displayPosition.dimension);
    }

    [Fact]
    public void ToDisplayPosition_SubtractsWorldCenterOffset_ForExactCoordinates()
    {
        var formatter = new WorldCoordinateDisplayFormatter(TestCoreServerApiFactory.Create(1024000, 1024000));

        var displayPosition = formatter.ToDisplayPosition(new Vintagestory.API.MathTools.Vec3d(512009.75, 4, 511998.25));

        Assert.NotNull(displayPosition);
        Assert.Equal(9.75, displayPosition.X);
        Assert.Equal(4, displayPosition.Y);
        Assert.Equal(-1.75, displayPosition.Z);
    }

    [Fact]
    public void FormatBlockPosition_FormatsCenteredPlayerFacingCoordinates()
    {
        var formatter = new WorldCoordinateDisplayFormatter(TestCoreServerApiFactory.Create(1024000, 1024000));

        string formatted = formatter.FormatBlockPosition(2, 512002, 3, 512139);

        Assert.Equal("2:2,3,139", formatted);
    }

    private static class TestCoreServerApiFactory
    {
        public static ICoreServerAPI Create(int mapSizeX, int mapSizeZ)
        {
            var worldManager = DispatchProxy.Create<IWorldManagerAPI, WorldManagerProxy>();
            var worldManagerProxy = (WorldManagerProxy)(object)worldManager;
            var api = DispatchProxy.Create<ICoreServerAPI, CoreServerApiProxy>();
            var apiProxy = (CoreServerApiProxy)(object)api;

            worldManagerProxy.MapSizeX = mapSizeX;
            worldManagerProxy.MapSizeZ = mapSizeZ;
            apiProxy.WorldManager = worldManager;

            return api;
        }
    }

    private class CoreServerApiProxy : DispatchProxy
    {
        public IWorldManagerAPI? WorldManager { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_WorldManager")
            {
                return WorldManager;
            }

            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }

    private class WorldManagerProxy : DispatchProxy
    {
        public int MapSizeX { get; set; }

        public int MapSizeZ { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_MapSizeX" => MapSizeX,
                "get_MapSizeZ" => MapSizeZ,
                _ => targetMethod?.ReturnType.IsValueType == true
                    ? Activator.CreateInstance(targetMethod.ReturnType)
                    : null
            };
        }
    }
}
