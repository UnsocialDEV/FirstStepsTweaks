using System.Reflection;
using System.Runtime.CompilerServices;
using FirstStepsTweaks.Infrastructure.Coordinates;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class WorldCoordinateReaderTests
{
    [Fact]
    public void GetExactPosition_ReturnsOriginalWorldCoordinates()
    {
        var reader = new WorldCoordinateReader();
        Entity entity = CreateEntity(12.25, 34.5, -6.75, 3);

        Vec3d position = reader.GetExactPosition(entity);

        Assert.NotNull(position);
        Assert.Equal(12.25, position.X);
        Assert.Equal(34.5, position.Y);
        Assert.Equal(-6.75, position.Z);
    }

    [Fact]
    public void GetBlockPosition_PreservesSimpleWorldCoordinates_ForGravePlacement()
    {
        var reader = new WorldCoordinateReader();
        Entity entity = CreateEntity(10, 10, 10, 7);

        BlockPos position = reader.GetBlockPosition(entity);

        Assert.NotNull(position);
        Assert.Equal(10, position.X);
        Assert.Equal(10, position.Y);
        Assert.Equal(10, position.Z);
        Assert.Equal(7, position.dimension);
    }

    [Fact]
    public void GetBlockPosition_FloorsFractionalAndNegativeCoordinates()
    {
        var reader = new WorldCoordinateReader();
        Entity entity = CreateEntity(-0.2, 9.99, 3.1, 2);

        BlockPos position = reader.GetBlockPosition(entity);

        Assert.NotNull(position);
        Assert.Equal(-1, position.X);
        Assert.Equal(9, position.Y);
        Assert.Equal(3, position.Z);
        Assert.Equal(2, position.dimension);
    }

    private static Entity CreateEntity(double x, double y, double z, int dimension)
    {
        var entity = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(EntityPlayer));
        var position = new EntityPos(x, y, z)
        {
            Dimension = dimension
        };

        typeof(Entity).GetField("Pos", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(entity, position);

        return entity;
    }
}
