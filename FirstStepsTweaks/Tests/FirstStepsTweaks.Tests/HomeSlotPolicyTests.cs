using System.Collections.Generic;
using FirstStepsTweaks.Services;
using FirstStepsTweaks.Teleport;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class HomeSlotPolicyTests
{
    private readonly HomeSlotPolicy policy = new();

    [Fact]
    public void CanCreate_ReturnsTrue_WhenAddingNewHomeUnderLimit()
    {
        var homes = new Dictionary<string, HomeLocation>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = new HomeLocation(1, 2, 3)
        };

        bool result = policy.CanCreate(homes, "mine", 2);

        Assert.True(result);
    }

    [Fact]
    public void CanCreate_ReturnsFalse_WhenAddingNewHomeAtLimit()
    {
        var homes = new Dictionary<string, HomeLocation>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = new HomeLocation(1, 2, 3),
            ["mine"] = new HomeLocation(4, 5, 6)
        };

        bool result = policy.CanCreate(homes, "cave", 2);

        Assert.False(result);
    }

    [Fact]
    public void CanCreate_ReturnsTrue_WhenOverwritingExistingHome()
    {
        var homes = new Dictionary<string, HomeLocation>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = new HomeLocation(1, 2, 3),
            ["mine"] = new HomeLocation(4, 5, 6)
        };

        bool result = policy.CanCreate(homes, "mine", 2);

        Assert.True(result);
    }

    [Fact]
    public void CanCreate_ReturnsTrue_WhenOverwritingExistingHome_WhileAboveCurrentLimit()
    {
        var homes = new Dictionary<string, HomeLocation>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = new HomeLocation(1, 2, 3),
            ["mine"] = new HomeLocation(4, 5, 6),
            ["farm"] = new HomeLocation(7, 8, 9)
        };

        bool result = policy.CanCreate(homes, "mine", 1);

        Assert.True(result);
    }

    [Fact]
    public void DictionaryBehavior_IsCaseInsensitive_ForLookupAndDelete()
    {
        var homes = new Dictionary<string, HomeLocation>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["Mine"] = new HomeLocation(4, 5, 6),
            ["Farm"] = new HomeLocation(7, 8, 9)
        };

        bool contains = homes.ContainsKey("mine");
        bool removed = homes.Remove("FARM");

        Assert.True(contains);
        Assert.True(removed);
        Assert.Single(homes);
    }
}
