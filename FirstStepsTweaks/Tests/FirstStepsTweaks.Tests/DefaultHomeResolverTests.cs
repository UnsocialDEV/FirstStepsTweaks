using System.Collections.Generic;
using FirstStepsTweaks.Services;
using FirstStepsTweaks.Teleport;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class DefaultHomeResolverTests
{
    private readonly DefaultHomeResolver resolver = new();

    [Fact]
    public void Resolve_PrefersLiteralHome_WhenPresent()
    {
        var homes = new Dictionary<string, HomeLocation>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["mine"] = new HomeLocation(4, 5, 6, 1),
            ["home"] = new HomeLocation(1, 2, 3, 2)
        };

        var result = resolver.Resolve(homes);

        Assert.NotNull(result);
        Assert.Equal("home", result.Value.Key);
    }

    [Fact]
    public void Resolve_UsesOldestCreated_WhenHomeIsMissing()
    {
        var homes = new Dictionary<string, HomeLocation>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["mine"] = new HomeLocation(4, 5, 6, 2),
            ["farm"] = new HomeLocation(1, 2, 3, 1)
        };

        var result = resolver.Resolve(homes);

        Assert.NotNull(result);
        Assert.Equal("farm", result.Value.Key);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNoHomesExist()
    {
        var homes = new Dictionary<string, HomeLocation>(System.StringComparer.OrdinalIgnoreCase);

        var result = resolver.Resolve(homes);

        Assert.Null(result);
    }
}
