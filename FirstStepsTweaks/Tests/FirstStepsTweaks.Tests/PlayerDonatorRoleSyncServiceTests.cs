using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class PlayerDonatorRoleSyncServiceTests
{
    [Fact]
    public async Task SyncAsync_WhenDiscordHasMultipleMatchingRoles_AssignsHighestTierRole()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");
        var roleAssigner = new FakePlayerRoleAssigner();
        var defaultRoleResetter = new FakePlayerDefaultRoleResetter();
        var privilegeReader = new FakePlayerPrivilegeReader("firststepstweaks.supporter", "firststepstweaks.founder");
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
            roleAssigner,
            defaultRoleResetter,
            privilegeReader,
            privilegeMutator,
            new FakeAdminModeStore(),
            messenger);
        var player = CreatePlayer("player-1", "Ava", "suplayer");

        await service.SyncAsync(player);

        Assert.Equal(new[] { "founder" }, roleAssigner.AssignedRoleCodes);
        Assert.Equal(Array.Empty<string>(), defaultRoleResetter.ResetPlayers);
        Assert.Equal(
            new[] { "firststepstweaks.founder", "firststepstweaks.supporter" },
            privilegeMutator.RevokedPrivileges);
        Assert.Equal("Discord donator role synced.", messenger.LastInfoMessage);
        Assert.Equal("founder", GetPlayerProxy(player).RoleCode);
    }

    [Fact]
    public async Task SyncAsync_TreatsDiscordRoleNamesAsCaseInsensitive()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");
        var roleAssigner = new FakePlayerRoleAssigner();
        var service = CreateService(
            linkedStore,
            new FakeDiscordMemberRoleClient(new DiscordMemberRoles(
                new[] { "1" },
                new[] { new DiscordGuildRole("1", "FoUnDeR") })),
            roleAssigner,
            new FakePlayerDefaultRoleResetter(),
            new FakePlayerPrivilegeReader(),
            new FakePlayerPrivilegeMutator(),
            new FakeAdminModeStore(),
            new FakePlayerMessenger());

        await service.SyncAsync(CreatePlayer("player-1", "Ava", "suplayer"));

        Assert.Contains("founder", roleAssigner.AssignedRoleCodes);
    }

    [Fact]
    public async Task SyncAsync_WhenPlayerIsNotLinked_DoesNothing()
    {
        var roleAssigner = new FakePlayerRoleAssigner();
        var defaultRoleResetter = new FakePlayerDefaultRoleResetter();
        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var messenger = new FakePlayerMessenger();
        var service = CreateService(
            new FakeLinkedAccountStore(),
            new FakeDiscordMemberRoleClient(EmptyRoles),
            roleAssigner,
            defaultRoleResetter,
            new FakePlayerPrivilegeReader(),
            privilegeMutator,
            new FakeAdminModeStore(),
            messenger);

        await service.SyncAsync(CreatePlayer("player-1", "Ava", "suplayer"));

        Assert.Empty(roleAssigner.AssignedRoleCodes);
        Assert.Empty(defaultRoleResetter.ResetPlayers);
        Assert.Empty(privilegeMutator.RevokedPrivileges);
        Assert.Equal(0, messenger.InfoCount);
    }

    [Fact]
    public async Task SyncAsync_WhenDiscordHasNoMatchingRole_ResetsToDefaultAndCleansLegacyPrivileges()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");
        var roleAssigner = new FakePlayerRoleAssigner();
        var defaultRoleResetter = new FakePlayerDefaultRoleResetter();
        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var service = CreateService(
            linkedStore,
            new FakeDiscordMemberRoleClient(EmptyRoles),
            roleAssigner,
            defaultRoleResetter,
            new FakePlayerPrivilegeReader("firststepstweaks.supporter", "firststepstweaks.contributor", "custom"),
            privilegeMutator,
            new FakeAdminModeStore(),
            new FakePlayerMessenger());
        var player = CreatePlayer("player-1", "Ava", "supporter");

        await service.SyncAsync(player);

        Assert.Empty(roleAssigner.AssignedRoleCodes);
        Assert.Equal(new[] { "player-1" }, defaultRoleResetter.ResetPlayers);
        Assert.Equal(
            new[] { "firststepstweaks.contributor", "firststepstweaks.supporter" },
            privilegeMutator.RevokedPrivileges);
        Assert.Equal("suplayer", GetPlayerProxy(player).RoleCode);
    }

    [Fact]
    public async Task SyncAsync_DoesNotTouchUnrelatedStaffPrivileges()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");
        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var service = CreateService(
            linkedStore,
            new FakeDiscordMemberRoleClient(new DiscordMemberRoles(
                new[] { "1" },
                new[] { new DiscordGuildRole("1", "supporter") })),
            new FakePlayerRoleAssigner(),
            new FakePlayerDefaultRoleResetter(),
            new FakePlayerPrivilegeReader("firststepstweaks.supporter", StaffPrivilegeCatalog.AdminPrivilege),
            privilegeMutator,
            new FakeAdminModeStore(),
            new FakePlayerMessenger());

        await service.SyncAsync(CreatePlayer("player-1", "Ava", "suplayer"));

        Assert.Contains("firststepstweaks.supporter", privilegeMutator.RevokedPrivileges);
        Assert.DoesNotContain(StaffPrivilegeCatalog.AdminPrivilege, privilegeMutator.RevokedPrivileges);
    }

    [Fact]
    public async Task SyncAsync_WhenManagedStateDoesNotChange_DoesNotSendNotification()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");
        var messenger = new FakePlayerMessenger();
        var service = CreateService(
            linkedStore,
            new FakeDiscordMemberRoleClient(new DiscordMemberRoles(
                new[] { "1" },
                new[] { new DiscordGuildRole("1", "supporter") })),
            new FakePlayerRoleAssigner(),
            new FakePlayerDefaultRoleResetter(),
            new FakePlayerPrivilegeReader(),
            new FakePlayerPrivilegeMutator(),
            new FakeAdminModeStore(),
            messenger);

        await service.SyncAsync(CreatePlayer("player-1", "Ava", "supporter"));

        Assert.Equal(0, messenger.InfoCount);
    }

    [Fact]
    public void ClearDonatorRole_ResetsDefaultRole_AndClearsLegacyPrivilegesOnly()
    {
        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var defaultRoleResetter = new FakePlayerDefaultRoleResetter();
        var service = CreateService(
            new FakeLinkedAccountStore(),
            new FakeDiscordMemberRoleClient(EmptyRoles),
            new FakePlayerRoleAssigner(),
            defaultRoleResetter,
            new FakePlayerPrivilegeReader("firststepstweaks.founder", "firststepstweaks.supporter", "custom"),
            privilegeMutator,
            new FakeAdminModeStore(),
            new FakePlayerMessenger());
        var player = CreatePlayer("player-1", "Ava", "founder");

        service.ClearDonatorRole(player);

        Assert.Equal(new[] { "player-1" }, defaultRoleResetter.ResetPlayers);
        Assert.Equal(
            new[] { "firststepstweaks.founder", "firststepstweaks.supporter" },
            privilegeMutator.RevokedPrivileges);
        Assert.Equal("suplayer", GetPlayerProxy(player).RoleCode);
    }

    [Fact]
    public async Task SyncAsync_UpdatesAdminModePriorRole_WhenRoleChangesDuringAdminMode()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");
        var adminModeStore = new FakeAdminModeStore();
        adminModeStore.Store("player-1", new AdminModeState
        {
            IsActive = true,
            PriorRoleCode = "supporter"
        });
        var service = CreateService(
            linkedStore,
            new FakeDiscordMemberRoleClient(new DiscordMemberRoles(
                new[] { "1" },
                new[] { new DiscordGuildRole("1", "founder") })),
            new FakePlayerRoleAssigner(),
            new FakePlayerDefaultRoleResetter(),
            new FakePlayerPrivilegeReader(),
            new FakePlayerPrivilegeMutator(),
            adminModeStore,
            new FakePlayerMessenger());

        await service.SyncAsync(CreatePlayer("player-1", "Ava", "supporter"));

        Assert.Equal("founder", adminModeStore.Load("player-1")!.PriorRoleCode);
    }

    [Fact]
    public async Task SyncAsync_WhenRoleAssignmentFails_DoesNotCleanupOrNotify()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");
        var privilegeMutator = new FakePlayerPrivilegeMutator();
        var messenger = new FakePlayerMessenger();
        var logger = new FakeLogger();
        var service = CreateService(
            linkedStore,
            new FakeDiscordMemberRoleClient(new DiscordMemberRoles(
                new[] { "1" },
                new[] { new DiscordGuildRole("1", "founder") })),
            new FakePlayerRoleAssigner { ThrowOnAssign = true },
            new FakePlayerDefaultRoleResetter(),
            new FakePlayerPrivilegeReader("firststepstweaks.supporter"),
            privilegeMutator,
            new FakeAdminModeStore(),
            messenger,
            logger);
        var player = CreatePlayer("player-1", "Ava", "supporter");

        await service.SyncAsync(player);

        Assert.Empty(privilegeMutator.RevokedPrivileges);
        Assert.Equal(0, messenger.InfoCount);
        Assert.Equal("supporter", GetPlayerProxy(player).RoleCode);
        Assert.Single(logger.Errors);
    }

    private static readonly DiscordMemberRoles EmptyRoles = new(Array.Empty<string>(), Array.Empty<DiscordGuildRole>());

    private static PlayerDonatorRoleSyncService CreateService(
        IDiscordLinkedAccountStore linkedStore,
        IDiscordMemberRoleClient memberRoleClient,
        FakePlayerRoleAssigner roleAssigner,
        FakePlayerDefaultRoleResetter defaultRoleResetter,
        FakePlayerPrivilegeReader privilegeReader,
        FakePlayerPrivilegeMutator privilegeMutator,
        FakeAdminModeStore adminModeStore,
        FakePlayerMessenger messenger,
        FakeLogger? logger = null)
    {
        logger ??= new FakeLogger();
        var api = CreateApi(logger);
        var tierCatalog = new DonatorTierCatalog();
        var roleCodeReader = new PlayerRoleCodeReader();

        return new PlayerDonatorRoleSyncService(
            api,
            new DiscordBridgeConfig
            {
                EnableRoleSync = true,
                BotToken = "token",
                GuildId = "guild"
            },
            linkedStore,
            memberRoleClient,
            new DiscordRoleNameResolver(),
            new DiscordDonatorRolePlanner(tierCatalog),
            new DonatorRoleTransitionApplier(api, roleCodeReader, roleAssigner, defaultRoleResetter),
            new LegacyDonatorPrivilegeCleaner(privilegeReader, privilegeMutator, tierCatalog),
            new AdminModePriorRoleUpdater(api, adminModeStore),
            messenger);
    }

    private static IServerPlayer CreatePlayer(string playerUid, string playerName, string roleCode)
    {
        var proxy = DispatchProxy.Create<IServerPlayer, TestServerPlayerProxy>();
        var player = (TestServerPlayerProxy)(object)proxy;
        player.PlayerUid = playerUid;
        player.PlayerName = playerName;
        player.RoleCode = roleCode;
        return proxy;
    }

    private static TestServerPlayerProxy GetPlayerProxy(IServerPlayer player)
    {
        return (TestServerPlayerProxy)(object)player;
    }

    private static ICoreServerAPI CreateApi(FakeLogger logger)
    {
        ICoreServerAPI api = DispatchProxy.Create<ICoreServerAPI, TestCoreServerApiProxy>();
        ((TestCoreServerApiProxy)(object)api).Logger = logger.CreateProxy();
        return api;
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

    private sealed class FakePlayerRoleAssigner : IPlayerRoleAssigner
    {
        public List<string> AssignedRoleCodes { get; } = new();

        public bool ThrowOnAssign { get; set; }

        public void Assign(IServerPlayer player, string roleCode)
        {
            if (ThrowOnAssign)
            {
                throw new InvalidOperationException("Role assignment failed.");
            }

            AssignedRoleCodes.Add(roleCode);
            GetPlayerProxy(player).RoleCode = roleCode;
        }
    }

    private sealed class FakePlayerDefaultRoleResetter : IPlayerDefaultRoleResetter
    {
        public List<string> ResetPlayers { get; } = new();

        public void Reset(IServerPlayer player)
        {
            ResetPlayers.Add(player.PlayerUID);
            GetPlayerProxy(player).RoleCode = GetDefaultRoleCode();
        }

        public string GetDefaultRoleCode()
        {
            return "suplayer";
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

    private sealed class FakeAdminModeStore : IAdminModeStore
    {
        private readonly Dictionary<string, AdminModeState> states = new(StringComparer.OrdinalIgnoreCase);

        public bool IsActive(IServerPlayer player)
        {
            return TryLoad(player, out AdminModeState state, out _) && state.IsActive;
        }

        public bool TryLoad(IServerPlayer player, out AdminModeState state, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (player != null && states.TryGetValue(player.PlayerUID, out state))
            {
                return true;
            }

            state = null!;
            return false;
        }

        public void Save(IServerPlayer player, AdminModeState state)
        {
            states[player.PlayerUID] = state;
        }

        public void Clear(IServerPlayer player)
        {
            states.Remove(player.PlayerUID);
        }

        public void Store(string playerUid, AdminModeState state)
        {
            states[playerUid] = state;
        }

        public AdminModeState? Load(string playerUid)
        {
            return states.TryGetValue(playerUid, out AdminModeState state) ? state : null;
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

    private sealed class FakeLogger
    {
        public List<string> Errors { get; } = new();

        public ILogger CreateProxy()
        {
            ILogger logger = DispatchProxy.Create<ILogger, TestLoggerProxy>();
            ((TestLoggerProxy)(object)logger).Errors = Errors;
            return logger;
        }
    }

    private class TestCoreServerApiProxy : DispatchProxy
    {
        public ILogger? Logger { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_Logger" => Logger,
                _ => targetMethod?.ReturnType.IsValueType == true ? Activator.CreateInstance(targetMethod.ReturnType) : null
            };
        }
    }

    private class TestLoggerProxy : DispatchProxy
    {
        public List<string> Errors { get; set; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Error" && args?.Length > 0)
            {
                Errors.Add(args[0]?.ToString() ?? string.Empty);
                return null;
            }

            if (targetMethod?.ReturnType == typeof(void))
            {
                return null;
            }

            return targetMethod?.ReturnType.IsValueType == true ? Activator.CreateInstance(targetMethod.ReturnType) : null;
        }
    }

    private class TestServerPlayerProxy : DispatchProxy
    {
        public string PlayerUid { get; set; } = string.Empty;

        public string PlayerName { get; set; } = string.Empty;

        public string RoleCode { get; set; } = string.Empty;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_PlayerUID" => PlayerUid,
                "get_PlayerName" => PlayerName,
                "get_RoleCode" => RoleCode,
                _ => targetMethod?.ReturnType == typeof(void)
                    ? null
                    : targetMethod?.ReturnType.IsValueType == true
                        ? Activator.CreateInstance(targetMethod.ReturnType)
                        : null
            };
        }
    }
}
