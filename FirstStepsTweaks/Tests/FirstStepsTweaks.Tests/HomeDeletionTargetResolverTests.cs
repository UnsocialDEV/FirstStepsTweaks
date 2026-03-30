using System.Collections.Generic;
using FirstStepsTweaks.Services;
using FirstStepsTweaks.Teleport;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class HomeDeletionTargetResolverTests
{
    private readonly HomeDeletionTargetResolver resolver = new();

    [Fact]
    public void Resolve_ReturnsRequestedName_WhenExplicitNameProvided()
    {
        var homes = new Dictionary<string, HomeLocation>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = new HomeLocation(1, 2, 3),
            ["mine"] = new HomeLocation(4, 5, 6)
        };

        var result = resolver.Resolve(homes, "mine");

        Assert.True(result.Success);
        Assert.Equal("mine", result.HomeName);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Resolve_ReturnsOnlyStoredHome_WhenNameMissingAndSingleHomeExists()
    {
        var homes = new Dictionary<string, HomeLocation>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["cabin"] = new HomeLocation(1, 2, 3)
        };

        var result = resolver.Resolve(homes, null);

        Assert.True(result.Success);
        Assert.Equal("cabin", result.HomeName);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Resolve_ReturnsError_WhenNameMissingAndNoHomesExist()
    {
        var homes = new Dictionary<string, HomeLocation>(System.StringComparer.OrdinalIgnoreCase);

        var result = resolver.Resolve(homes, null);

        Assert.False(result.Success);
        Assert.Null(result.HomeName);
        Assert.Equal("You do not have any homes set.", result.ErrorMessage);
    }

    [Fact]
    public void Resolve_ReturnsError_WhenNameMissingAndMultipleHomesExist()
    {
        var homes = new Dictionary<string, HomeLocation>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = new HomeLocation(1, 2, 3),
            ["mine"] = new HomeLocation(4, 5, 6)
        };

        var result = resolver.Resolve(homes, null);

        Assert.False(result.Success);
        Assert.Null(result.HomeName);
        Assert.Equal("You have multiple homes. Use /delhome <name>.", result.ErrorMessage);
    }

    [Fact]
    public void Resolve_NormalizesExplicitName_CaseInsensitively()
    {
        var homes = new Dictionary<string, HomeLocation>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["Mine"] = new HomeLocation(4, 5, 6),
            ["Farm"] = new HomeLocation(7, 8, 9)
        };

        var result = resolver.Resolve(homes, "MINE");

        Assert.True(result.Success);
        Assert.Equal("mine", result.HomeName);
        Assert.Null(result.ErrorMessage);
    }
}
