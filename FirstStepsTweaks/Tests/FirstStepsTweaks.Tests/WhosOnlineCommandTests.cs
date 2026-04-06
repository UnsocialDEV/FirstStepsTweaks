using System.Reflection;
using FirstStepsTweaks.Commands;
using FirstStepsTweaks.Services;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class WhosOnlineCommandTests
{
    [Fact]
    public void PrivateStaffTagLogic_ReturnsAdminAndModTags()
    {
        MethodInfo method = typeof(WhosOnlineCommand).GetMethod("GetStaffTag", BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal(" [ADMIN]", method.Invoke(null, [StaffLevel.Admin]));
        Assert.Equal(" [MOD]", method.Invoke(null, [StaffLevel.Moderator]));
        Assert.Equal(string.Empty, method.Invoke(null, [StaffLevel.None]));
    }

    [Fact]
    public void PrivateSortOrderLogic_SortsAdminThenModeratorThenNormal()
    {
        MethodInfo method = typeof(WhosOnlineCommand).GetMethod("GetSortOrder", BindingFlags.NonPublic | BindingFlags.Static)!;
        IServerPlayer admin = StaffTestServerPlayerFactory.Create("uid-1", "Charlie");
        IServerPlayer moderator = StaffTestServerPlayerFactory.Create("uid-2", "Bravo");
        IServerPlayer normal = StaffTestServerPlayerFactory.Create("uid-3", "Alpha");
        var levels = new Dictionary<string, StaffLevel>
        {
            [admin.PlayerUID] = StaffLevel.Admin,
            [moderator.PlayerUID] = StaffLevel.Moderator,
            [normal.PlayerUID] = StaffLevel.None
        };

        IServerPlayer[] players = [normal, moderator, admin];
        string[] sorted = players
            .OrderBy(player => (int)method.Invoke(null, [levels[player.PlayerUID]])!)
            .ThenBy(player => player.PlayerName, StringComparer.OrdinalIgnoreCase)
            .Select(player => player.PlayerName)
            .ToArray();

        Assert.Equal(["Charlie", "Bravo", "Alpha"], sorted);
    }
}
