using System.Collections.Generic;
using FirstStepsTweaks.Teleport;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class HomeDataSerializerTests
{
    private readonly HomeDataSerializer serializer = new();

    [Fact]
    public void SerializeAndDeserialize_RoundTripsMultipleHomes()
    {
        var homes = new Dictionary<string, HomeLocation>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = new HomeLocation(1, 2, 3, 1),
            ["mine"] = new HomeLocation(4, 5, 6, 2)
        };

        byte[] data = serializer.Serialize(homes);
        Dictionary<string, HomeLocation> result = serializer.Deserialize(data);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result["home"].X);
        Assert.Equal(6, result["mine"].Z);
        Assert.Equal(1, result["home"].CreatedOrder);
        Assert.Equal(2, result["mine"].CreatedOrder);
    }

    [Fact]
    public void TryDeserializeLegacy_MigratesLegacySingleHome()
    {
        byte[] data = new byte[24];
        System.BitConverter.GetBytes(11d).CopyTo(data, 0);
        System.BitConverter.GetBytes(22d).CopyTo(data, 8);
        System.BitConverter.GetBytes(33d).CopyTo(data, 16);

        bool migrated = serializer.TryDeserializeLegacy(data, out HomeLocation? home);

        Assert.True(migrated);
        Assert.NotNull(home);
        Assert.Equal(11d, home.X);
        Assert.Equal(22d, home.Y);
        Assert.Equal(33d, home.Z);
        Assert.Equal(0, home.CreatedOrder);
    }

    [Fact]
    public void Deserialize_DoesNotUseLegacyShape_WhenNewPayloadExists()
    {
        var homes = new Dictionary<string, HomeLocation>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["base"] = new HomeLocation(7, 8, 9)
        };

        byte[] data = serializer.Serialize(homes);
        Dictionary<string, HomeLocation> result = serializer.Deserialize(data);

        Assert.Single(result);
        Assert.DoesNotContain(HomeStore.MigratedLegacyHomeName, result.Keys);
    }

    [Fact]
    public void Deserialize_AllowsExistingNamedPayloadWithoutCreationMetadata()
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes("{\"mine\":{\"x\":4,\"y\":5,\"z\":6}}");

        Dictionary<string, HomeLocation> result = serializer.Deserialize(data);

        Assert.Single(result);
        Assert.Equal(0, result["mine"].CreatedOrder);
    }
}
