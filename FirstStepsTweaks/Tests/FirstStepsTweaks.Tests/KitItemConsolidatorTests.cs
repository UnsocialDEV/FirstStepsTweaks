using System.Collections.Generic;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class KitItemConsolidatorTests
{
    [Fact]
    public void Consolidate_KeepsFirstEntryAndDropsDuplicates()
    {
        var consolidator = new KitItemConsolidator();
        var items = new List<KitItemConfig>
        {
            new KitItemConfig("game:stick", 3),
            new KitItemConfig("game:stick", 99),
            new KitItemConfig("game:flint", 2)
        };

        Dictionary<string, int> result = consolidator.Consolidate(items);

        Assert.Equal(2, result.Count);
        Assert.Equal(3, result["game:stick"]);
        Assert.Equal(2, result["game:flint"]);
    }
}
