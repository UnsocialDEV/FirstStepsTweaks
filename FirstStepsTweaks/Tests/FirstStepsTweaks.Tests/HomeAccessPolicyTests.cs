using System.Collections.Generic;
using FirstStepsTweaks.Services;
using FirstStepsTweaks.Teleport;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class HomeAccessPolicyTests
{
    private readonly HomeAccessPolicy policy = new();

    [Fact]
    public void GetAccessibleHomes_ReturnsOnlyOldestHomesUpToCurrentLimit()
    {
        var homes = new Dictionary<string, HomeLocation>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["mine"] = new HomeLocation(1, 2, 3, 2),
            ["home"] = new HomeLocation(4, 5, 6, 1),
            ["farm"] = new HomeLocation(7, 8, 9, 3)
        };

        IReadOnlyDictionary<string, HomeLocation> result = policy.GetAccessibleHomes(homes, 2);

        Assert.Equal(2, result.Count);
        Assert.Contains("home", result.Keys);
        Assert.Contains("mine", result.Keys);
        Assert.DoesNotContain("farm", result.Keys);
    }

    [Fact]
    public void CanUseHome_ReturnsFalse_WhenHomeIsStoredButBeyondCurrentLimit()
    {
        var homes = new Dictionary<string, HomeLocation>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = new HomeLocation(1, 2, 3, 1),
            ["mine"] = new HomeLocation(4, 5, 6, 2),
            ["farm"] = new HomeLocation(7, 8, 9, 3)
        };

        bool result = policy.CanUseHome(homes, "farm", 1);

        Assert.False(result);
    }
}
