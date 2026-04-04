using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
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
    public async Task SyncAsync_WhenDiscordHasMultipleMatchingRoles_GrantsCumulativePrivilegesForHighestTier()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");
        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var messenger = new FakePlayerMessenger();
        var service = CreateService(
            linkedStore,
            new FakeDiscordMemberRoleClient(new DiscordMemberRoles(
                new[] { "1", "2" },
                new[]
                {
                    new DiscordGuildRole("1", "supporter"),
                    new DiscordGuildRole("2", "founder")
                })),
            new FakePlayerPrivilegeReader(),
            privilegeMutator,
            messenger);

        await service.SyncAsync(CreatePlayer("player-1", "Ava"));

        Assert.Equal(
            new[]
            {
                "firststepstweaks.founder",
                "firststepstweaks.patron",
                "firststepstweaks.sponsor",
                "firststepstweaks.contributor",
                "firststepstweaks.supporter"
            },
            privilegeMutator.GrantedPrivileges);
        Assert.Empty(privilegeMutator.RevokedPrivileges);
        Assert.Equal("Discord donator role synced.", messenger.LastInfoMessage);
    }

    [Fact]
    public async Task SyncAsync_TreatsDiscordRoleNamesAsCaseInsensitive()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");
        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var service = CreateService(
            linkedStore,
            new FakeDiscordMemberRoleClient(new DiscordMemberRoles(
                new[] { "1" },
                new[] { new DiscordGuildRole("1", "FoUnDeR") })),
            new FakePlayerPrivilegeReader(),
            privilegeMutator,
            new FakePlayerMessenger());

        await service.SyncAsync(CreatePlayer("player-1", "Ava"));

        Assert.Contains("firststepstweaks.founder", privilegeMutator.GrantedPrivileges);
    }

    [Fact]
    public async Task SyncAsync_WhenPlayerIsNotLinked_DoesNothing()
    {
        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var messenger = new FakePlayerMessenger();
        var service = CreateService(
            new FakeLinkedAccountStore(),
            new FakeDiscordMemberRoleClient(EmptyRoles),
            new FakePlayerPrivilegeReader(),
            privilegeMutator,
            messenger);

        await service.SyncAsync(CreatePlayer("player-1", "Ava"));

        Assert.Empty(privilegeMutator.GrantedPrivileges);
        Assert.Empty(privilegeMutator.RevokedPrivileges);
        Assert.Equal(0, messenger.InfoCount);
    }

    [Fact]
    public async Task SyncAsync_WhenDiscordHasNoMatchingRole_RemovesManagedPrivilegesOnly()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");
        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var messenger = new FakePlayerMessenger();
        var service = CreateService(
            linkedStore,
            new FakeDiscordMemberRoleClient(EmptyRoles),
            new FakePlayerPrivilegeReader(
                "firststepstweaks.supporter",
                "firststepstweaks.contributor"),
            privilegeMutator,
            messenger);

        await service.SyncAsync(CreatePlayer("player-1", "Ava"));

        Assert.Empty(privilegeMutator.GrantedPrivileges);
        Assert.Equal(
            new[]
            {
                "firststepstweaks.contributor",
                "firststepstweaks.supporter"
            },
            privilegeMutator.RevokedPrivileges);
        Assert.Equal("Discord donator role synced.", messenger.LastInfoMessage);
    }

    [Fact]
    public async Task SyncAsync_WhenTierDowngrades_RevokesMissingPrivilegesOnly()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");
        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var service = CreateService(
            linkedStore,
            new FakeDiscordMemberRoleClient(new DiscordMemberRoles(
                new[] { "1" },
                new[] { new DiscordGuildRole("1", "contributor") })),
            new FakePlayerPrivilegeReader(
                "firststepstweaks.supporter",
                "firststepstweaks.contributor",
                "firststepstweaks.sponsor"),
            privilegeMutator,
            new FakePlayerMessenger());

        await service.SyncAsync(CreatePlayer("player-1", "Ava"));

        Assert.Empty(privilegeMutator.GrantedPrivileges);
        Assert.Equal(new[] { "firststepstweaks.sponsor" }, privilegeMutator.RevokedPrivileges);
    }

    [Fact]
    public async Task SyncAsync_WhenPlayerHasPartialTargetSet_GrantsOnlyMissingPrivileges()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");
        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var service = CreateService(
            linkedStore,
            new FakeDiscordMemberRoleClient(new DiscordMemberRoles(
                new[] { "1" },
                new[] { new DiscordGuildRole("1", "sponsor") })),
            new FakePlayerPrivilegeReader("firststepstweaks.supporter"),
            privilegeMutator,
            new FakePlayerMessenger());

        await service.SyncAsync(CreatePlayer("player-1", "Ava"));

        Assert.Equal(
            new[]
            {
                "firststepstweaks.sponsor",
                "firststepstweaks.contributor"
            },
            privilegeMutator.GrantedPrivileges);
        Assert.Empty(privilegeMutator.RevokedPrivileges);
    }

    [Fact]
    public void ClearDonatorRole_RemovesManagedPrivilegesOnly()
    {
        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var service = CreateService(
            new FakeLinkedAccountStore(),
            new FakeDiscordMemberRoleClient(EmptyRoles),
            new FakePlayerPrivilegeReader(
                "firststepstweaks.founder",
                "firststepstweaks.supporter"),
            privilegeMutator,
            new FakePlayerMessenger());

        service.ClearDonatorRole(CreatePlayer("player-1", "Ava"));

        Assert.Equal(
            new[]
            {
                "firststepstweaks.founder",
                "firststepstweaks.supporter"
            },
            privilegeMutator.RevokedPrivileges);
    }

    [Fact]
    public async Task SyncAsync_WhenManagedPrivilegesDoNotChange_DoesNotSendNotification()
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
            new FakePlayerPrivilegeReader("firststepstweaks.supporter"),
            privilegeMutator,
            messenger);

        await service.SyncAsync(CreatePlayer("player-1", "Ava"));

        Assert.Empty(privilegeMutator.GrantedPrivileges);
        Assert.Empty(privilegeMutator.RevokedPrivileges);
        Assert.Equal(0, messenger.InfoCount);
    }

    private static readonly DiscordMemberRoles EmptyRoles = new(Array.Empty<string>(), Array.Empty<DiscordGuildRole>());

    private static PlayerDonatorRoleSyncService CreateService(
        IDiscordLinkedAccountStore linkedStore,
        IDiscordMemberRoleClient memberRoleClient,
        IPlayerPrivilegeReader privilegeReader,
        IPlayerPrivilegeMutator privilegeMutator,
        IPlayerMessenger messenger)
    {
        var privilegeCatalog = new DonatorPrivilegeCatalog();
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
            new DiscordDonatorPrivilegePlanner(privilegeCatalog),
            privilegeReader,
            privilegeMutator,
            privilegeCatalog,
            messenger);
    }

    private static IServerPlayer CreatePlayer(string playerUid, string playerName)
    {
        var proxy = DispatchProxy.Create<IServerPlayer, TestServerPlayerProxy>();
        ((TestServerPlayerProxy)(object)proxy).Values["get_PlayerUID"] = playerUid;
        ((TestServerPlayerProxy)(object)proxy).Values["get_PlayerName"] = playerName;
        return proxy;
    }

    private sealed class FakeLinkedAccountStore : IDiscordLinkedAccountStore
    {
        private readonly Dictionary<string, string> links = new(StringComparer.OrdinalIgnoreCase);

        public string GetLinkedDiscordUserId(string playerUid)
        {
            return links.TryGetValue(playerUid, out string discordUserId) ? discordUserId : null;
        }

        public IReadOnlyDictionary<string, string> GetAllLinkedDiscordUserIds()
        {
            return links;
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

    private sealed class FakePlayerPrivilegeReader : IPlayerPrivilegeReader
    {
        private readonly HashSet<string> privileges;

        public FakePlayerPrivilegeReader(params string[] privileges)
        {
            this.privileges = new HashSet<string>(privileges, StringComparer.OrdinalIgnoreCase);
        }

        public bool HasPrivilege(IServerPlayer player, string privilege)
        {
            return privileges.Contains(privilege);
        }
    }

    private sealed class FakePlayerPrivilegeMutator : IPlayerPrivilegeMutator
    {
        public List<string> GrantedPrivileges { get; } = new();

        public List<string> RevokedPrivileges { get; } = new();

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

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null)
            {
                return null;
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

            return returnType.IsValueType
                ? Activator.CreateInstance(returnType)
                : null;
        }
    }
}
