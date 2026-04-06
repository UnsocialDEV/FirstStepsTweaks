using FirstStepsTweaks.Config;
using FirstStepsTweaks.Services;
using System.Reflection;
using Vintagestory.API.Common;
using Xunit;

namespace FirstStepsTweaks.Tests;

public class DonatorChatPrefixApplicatorTests
{
    private readonly DonatorChatPrefixApplicator applicator = new();

    [Fact]
    public void Apply_LeavesMessageUnchanged_WhenNoPrivilegesMatch()
    {
        var config = new ChatConfig();

        string result = applicator.Apply("Ava: hello", CreatePlayer("villager"), config);

        Assert.Equal("Ava: hello", result);
    }

    [Fact]
    public void Apply_LeavesCommandUnchanged_WhenMessageStartsWithSlash()
    {
        var config = new ChatConfig();

        string result = applicator.Apply("/home", CreatePlayer("founder"), config);

        Assert.Equal("/home", result);
    }

    [Fact]
    public void Apply_PrependsTierPrefix_WhenRoleMatches()
    {
        var config = new ChatConfig();

        string result = applicator.Apply("Ava: hello", CreatePlayer("patron"), config);

        Assert.Equal("•P Ava: hello", result);
    }

    private static IPlayer CreatePlayer(string roleCode)
    {
        IPlayer player = DispatchProxy.Create<IPlayer, TestPlayerProxy>();
        ((TestPlayerProxy)(object)player).RoleCode = roleCode;
        return player;
    }

    private class TestPlayerProxy : DispatchProxy
    {
        public string RoleCode { get; set; } = string.Empty;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_RoleCode")
            {
                return RoleCode;
            }

            if (targetMethod?.ReturnType == typeof(void))
            {
                return null;
            }

            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
