using System.Reflection;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class PlayerDonatorRoleSyncServiceTests
{
    [Fact]
    public async Task SyncAsync_WhenDiscordHasMultipleMatchingRoles_GrantsHighestPrivilege()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");

        var memberRoleClient = new FakeDiscordMemberRoleClient(new DiscordMemberRoles(
            new[] { "1", "2" },
            new[]
            {
                new DiscordGuildRole("1", "supporter"),
                new DiscordGuildRole("2", "founder")
            }));
        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var messenger = new FakePlayerMessenger();
        var service = CreateService(linkedStore, memberRoleClient, privilegeMutator, messenger);

        await service.SyncAsync(CreatePlayer("player-1", "Ava"));

        Assert.Contains("firststepstweaks.founder", privilegeMutator.GrantedPrivileges);
        Assert.Contains("firststepstweaks.back", privilegeMutator.GrantedPrivileges);
        Assert.Equal(1, messenger.InfoCount);
        Assert.Equal("Discord donator role synced.", messenger.LastInfoMessage);
    }

    [Fact]
    public async Task SyncAsync_TreatsDiscordRoleNamesAsCaseInsensitive()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");

        var memberRoleClient = new FakeDiscordMemberRoleClient(new DiscordMemberRoles(
            new[] { "1" },
            new[] { new DiscordGuildRole("1", "FoUnDeR") }));
        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var service = CreateService(linkedStore, memberRoleClient, privilegeMutator, new FakePlayerMessenger());

        await service.SyncAsync(CreatePlayer("player-1", "Ava"));

        Assert.Contains("firststepstweaks.founder", privilegeMutator.GrantedPrivileges);
        Assert.Contains("firststepstweaks.back", privilegeMutator.GrantedPrivileges);
    }

    [Fact]
    public async Task SyncAsync_WhenPlayerIsNotLinked_DoesNothing()
    {
        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var messenger = new FakePlayerMessenger();
        var service = CreateService(new FakeLinkedAccountStore(), new FakeDiscordMemberRoleClient(EmptyRoles), privilegeMutator, messenger);

        await service.SyncAsync(CreatePlayer("player-1", "Ava"));

        Assert.Empty(privilegeMutator.GrantedPrivileges);
        Assert.Empty(privilegeMutator.RevokedPrivileges);
        Assert.Equal(0, messenger.InfoCount);
    }

    [Fact]
    public async Task SyncAsync_WhenDiscordHasNoMatchingRole_RevokesExistingDonatorPrivileges()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");

        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var messenger = new FakePlayerMessenger();
        var service = CreateService(linkedStore, new FakeDiscordMemberRoleClient(EmptyRoles), privilegeMutator, messenger);

        await service.SyncAsync(CreatePlayer("player-1", "Ava", "firststepstweaks.supporter"));

        Assert.Contains("firststepstweaks.supporter", privilegeMutator.RevokedPrivileges);
        Assert.Equal(1, messenger.InfoCount);
    }

    [Fact]
    public async Task SyncAsync_WhenTierIsPatron_GrantsBackPrivilege()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");

        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var service = CreateService(
            linkedStore,
            new FakeDiscordMemberRoleClient(new DiscordMemberRoles(
                new[] { "1" },
                new[] { new DiscordGuildRole("1", "patron") })),
            privilegeMutator,
            new FakePlayerMessenger());

        await service.SyncAsync(CreatePlayer("player-1", "Ava"));

        Assert.Contains("firststepstweaks.patron", privilegeMutator.GrantedPrivileges);
        Assert.Contains("firststepstweaks.back", privilegeMutator.GrantedPrivileges);
    }

    [Fact]
    public void ClearDonatorRole_RevokesAllDonatorPrivilegesWithoutTouchingBaseRole()
    {
        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var service = CreateService(new FakeLinkedAccountStore(), new FakeDiscordMemberRoleClient(EmptyRoles), privilegeMutator, new FakePlayerMessenger());

        service.ClearDonatorRole(CreatePlayer("player-1", "Ava", "firststepstweaks.founder", "firststepstweaks.supporter", "firststepstweaks.back"));

        Assert.Contains("firststepstweaks.founder", privilegeMutator.RevokedPrivileges);
        Assert.Contains("firststepstweaks.supporter", privilegeMutator.RevokedPrivileges);
        Assert.Contains("firststepstweaks.back", privilegeMutator.RevokedPrivileges);
    }

    [Fact]
    public async Task SyncAsync_WhenPrivilegeDoesNotChange_DoesNotSendNotification()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");

        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var messenger = new FakePlayerMessenger();
        var service = CreateService(
            linkedStore,
            new FakeDiscordMemberRoleClient(new DiscordMemberRoles(
                new[] { "1" },
                new[] { new DiscordGuildRole("1", "supporter") })),
            privilegeMutator,
            messenger);

        await service.SyncAsync(CreatePlayer("player-1", "Ava", "firststepstweaks.supporter"));

        Assert.Empty(privilegeMutator.GrantedPrivileges);
        Assert.Empty(privilegeMutator.RevokedPrivileges);
        Assert.Equal(0, messenger.InfoCount);
    }

    [Fact]
    public async Task SyncAsync_WhenHigherTierMatches_RevokesLowerTierAndGrantsHigherTier()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");

        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var messenger = new FakePlayerMessenger();
        var service = CreateService(
            linkedStore,
            new FakeDiscordMemberRoleClient(new DiscordMemberRoles(
                new[] { "1" },
                new[] { new DiscordGuildRole("1", "founder") })),
            privilegeMutator,
            messenger);

        await service.SyncAsync(CreatePlayer("player-1", "Ava", "firststepstweaks.supporter"));

        Assert.Contains("firststepstweaks.supporter", privilegeMutator.RevokedPrivileges);
        Assert.Contains("firststepstweaks.founder", privilegeMutator.GrantedPrivileges);
        Assert.Contains("firststepstweaks.back", privilegeMutator.GrantedPrivileges);
        Assert.Equal(1, messenger.InfoCount);
    }

    [Fact]
    public async Task SyncAsync_WhenTierDropsBelowPatron_RevokesBackPrivilege()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");

        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var service = CreateService(
            linkedStore,
            new FakeDiscordMemberRoleClient(new DiscordMemberRoles(
                new[] { "1" },
                new[] { new DiscordGuildRole("1", "supporter") })),
            privilegeMutator,
            new FakePlayerMessenger());

        await service.SyncAsync(CreatePlayer("player-1", "Ava", "firststepstweaks.patron", "firststepstweaks.back"));

        Assert.Contains("firststepstweaks.patron", privilegeMutator.RevokedPrivileges);
        Assert.Contains("firststepstweaks.back", privilegeMutator.RevokedPrivileges);
        Assert.Contains("firststepstweaks.supporter", privilegeMutator.GrantedPrivileges);
    }

    private static readonly DiscordMemberRoles EmptyRoles = new(Array.Empty<string>(), Array.Empty<DiscordGuildRole>());

    private static PlayerDonatorRoleSyncService CreateService(
        IDiscordLinkedAccountStore linkedStore,
        IDiscordMemberRoleClient memberRoleClient,
        IPlayerPrivilegeMutator privilegeMutator,
        IPlayerMessenger messenger)
    {
        return new PlayerDonatorRoleSyncService(
            null!,
            new DiscordBridgeConfig
            {
                EnableRoleSync = true,
                BotToken = "token",
                GuildId = "guild"
            },
            linkedStore,
            memberRoleClient,
            new DiscordRoleNameResolver(),
            new DiscordDonatorRolePlanner(new DonatorPrivilegeCatalog()),
            new DonatorPrivilegeCatalog(),
            new DonatorFeaturePrivilegeResolver(),
            privilegeMutator,
            messenger);
    }

    private static IServerPlayer CreatePlayer(string playerUid, string playerName, params string[] privileges)
    {
        var proxy = DispatchProxy.Create<IServerPlayer, TestServerPlayerProxy>();
        ((TestServerPlayerProxy)(object)proxy).Values["get_PlayerUID"] = playerUid;
        ((TestServerPlayerProxy)(object)proxy).Values["get_PlayerName"] = playerName;
        ((TestServerPlayerProxy)(object)proxy).Privileges.UnionWith(privileges);
        return proxy;
    }

    private sealed class FakeLinkedAccountStore : IDiscordLinkedAccountStore
    {
        private readonly Dictionary<string, string> links = new(StringComparer.OrdinalIgnoreCase);

        public string GetLinkedDiscordUserId(string playerUid)
        {
            return links.TryGetValue(playerUid, out string discordUserId) ? discordUserId : null;
        }

        public void SetLinkedDiscordUserId(string playerUid, string discordUserId)
        {
            links[playerUid] = discordUserId;
        }

        public void ClearLinkedDiscordUserId(string playerUid)
        {
            links.Remove(playerUid);
        }
    }

    private sealed class FakeDiscordMemberRoleClient : IDiscordMemberRoleClient
    {
        private readonly DiscordMemberRoles memberRoles;

        public FakeDiscordMemberRoleClient(DiscordMemberRoles memberRoles)
        {
            this.memberRoles = memberRoles;
        }

        public Task<DiscordMemberRoles> GetMemberRolesAsync(DiscordBridgeConfig config, string discordUserId)
        {
            return Task.FromResult(memberRoles);
        }
    }

    private sealed class FakePlayerPrivilegeMutator : IPlayerPrivilegeMutator
    {
        public List<string> GrantedPrivileges { get; } = new();

        public List<string> RevokedPrivileges { get; } = new();

        public string? LastGrantedPrivilege => GrantedPrivileges.LastOrDefault();

        public void Grant(IServerPlayer player, string privilege)
        {
            GrantedPrivileges.Add(privilege);
        }

        public void Revoke(IServerPlayer player, string privilege)
        {
            RevokedPrivileges.Add(privilege);
        }
    }

    private sealed class FakePlayerMessenger : IPlayerMessenger
    {
        public int InfoCount { get; private set; }

        public string? LastInfoMessage { get; private set; }

        public void SendInfo(IServerPlayer player, string message, int groupId, int chatType)
        {
            InfoCount++;
            LastInfoMessage = message;
        }

        public void SendGeneral(IServerPlayer player, string message, int groupId, int chatType)
        {
        }

        public void SendDual(IServerPlayer player, string message, int infoChatType, int generalChatType)
        {
        }

        public void SendDual(IServerPlayer player, string message, int infoGroupId, int infoChatType, int generalGroupId, int generalChatType)
        {
        }

        public void SendIngameError(IServerPlayer player, string code, string message)
        {
        }
    }

    private class TestServerPlayerProxy : DispatchProxy
    {
        public Dictionary<string, object> Values { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Privileges { get; } = new(StringComparer.OrdinalIgnoreCase);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null)
            {
                return null;
            }

            if (string.Equals(targetMethod.Name, nameof(IServerPlayer.HasPrivilege), StringComparison.Ordinal)
                && args?.Length == 1
                && args[0] is string privilege)
            {
                return Privileges.Contains(privilege);
            }

            if (Values.TryGetValue(targetMethod.Name, out object value))
            {
                return value;
            }

            Type returnType = targetMethod.ReturnType;
            if (returnType == typeof(void))
            {
                return null;
            }

            if (returnType.IsValueType)
            {
                return Activator.CreateInstance(returnType);
            }

            return null;
        }
    }
}
