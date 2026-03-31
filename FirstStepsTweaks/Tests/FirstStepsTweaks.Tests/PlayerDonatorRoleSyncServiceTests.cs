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
    public async Task SyncAsync_WhenDiscordHasMultipleMatchingRoles_AssignsHighestRole()
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
        var roleAssigner = new FakePlayerRoleAssigner();
        var resetter = new FakePlayerDefaultRoleResetter("default");
        var messenger = new FakePlayerMessenger();
        var service = CreateService(linkedStore, memberRoleClient, new FakePlayerRoleCodeReader("default"), roleAssigner, resetter, messenger);

        await service.SyncAsync(CreatePlayer("player-1", "Ava"));

        Assert.Equal("founder", roleAssigner.LastAssignedRoleCode);
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
        var roleAssigner = new FakePlayerRoleAssigner();
        var service = CreateService(linkedStore, memberRoleClient, new FakePlayerRoleCodeReader("default"), roleAssigner, new FakePlayerDefaultRoleResetter("default"), new FakePlayerMessenger());

        await service.SyncAsync(CreatePlayer("player-1", "Ava"));

        Assert.Equal("founder", roleAssigner.LastAssignedRoleCode);
    }

    [Fact]
    public async Task SyncAsync_WhenPlayerIsNotLinked_DoesNothing()
    {
        var roleAssigner = new FakePlayerRoleAssigner();
        var resetter = new FakePlayerDefaultRoleResetter("default");
        var messenger = new FakePlayerMessenger();
        var service = CreateService(new FakeLinkedAccountStore(), new FakeDiscordMemberRoleClient(EmptyRoles), new FakePlayerRoleCodeReader("supporter"), roleAssigner, resetter, messenger);

        await service.SyncAsync(CreatePlayer("player-1", "Ava"));

        Assert.Null(roleAssigner.LastAssignedRoleCode);
        Assert.Equal(0, resetter.ResetCount);
        Assert.Equal(0, messenger.InfoCount);
    }

    [Fact]
    public async Task SyncAsync_WhenDiscordHasNoMatchingRole_ResetsPlayerToDefaultRole()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");

        var resetter = new FakePlayerDefaultRoleResetter("default");
        var messenger = new FakePlayerMessenger();
        var service = CreateService(linkedStore, new FakeDiscordMemberRoleClient(EmptyRoles), new FakePlayerRoleCodeReader("supporter"), new FakePlayerRoleAssigner(), resetter, messenger);

        await service.SyncAsync(CreatePlayer("player-1", "Ava"));

        Assert.Equal(1, resetter.ResetCount);
        Assert.Equal(1, messenger.InfoCount);
    }

    [Fact]
    public void ClearDonatorRole_ResetsPlayerToDefaultRole()
    {
        var resetter = new FakePlayerDefaultRoleResetter("default");
        var service = CreateService(new FakeLinkedAccountStore(), new FakeDiscordMemberRoleClient(EmptyRoles), new FakePlayerRoleCodeReader("founder"), new FakePlayerRoleAssigner(), resetter, new FakePlayerMessenger());

        service.ClearDonatorRole(CreatePlayer("player-1", "Ava"));

        Assert.Equal(1, resetter.ResetCount);
    }

    [Fact]
    public async Task SyncAsync_WhenRoleDoesNotChange_DoesNotSendNotification()
    {
        var linkedStore = new FakeLinkedAccountStore();
        linkedStore.SetLinkedDiscordUserId("player-1", "discord-1");

        var roleAssigner = new FakePlayerRoleAssigner();
        var resetter = new FakePlayerDefaultRoleResetter("default");
        var messenger = new FakePlayerMessenger();
        var service = CreateService(
            linkedStore,
            new FakeDiscordMemberRoleClient(new DiscordMemberRoles(
                new[] { "1" },
                new[] { new DiscordGuildRole("1", "supporter") })),
            new FakePlayerRoleCodeReader("supporter"),
            roleAssigner,
            resetter,
            messenger);

        await service.SyncAsync(CreatePlayer("player-1", "Ava"));

        Assert.Null(roleAssigner.LastAssignedRoleCode);
        Assert.Equal(0, resetter.ResetCount);
        Assert.Equal(0, messenger.InfoCount);
    }

    private static readonly DiscordMemberRoles EmptyRoles = new(Array.Empty<string>(), Array.Empty<DiscordGuildRole>());

    private static PlayerDonatorRoleSyncService CreateService(
        IDiscordLinkedAccountStore linkedStore,
        IDiscordMemberRoleClient memberRoleClient,
        IPlayerRoleCodeReader roleCodeReader,
        IPlayerRoleAssigner roleAssigner,
        IPlayerDefaultRoleResetter defaultRoleResetter,
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
            roleCodeReader,
            roleAssigner,
            defaultRoleResetter,
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

    private sealed class FakePlayerRoleCodeReader : IPlayerRoleCodeReader
    {
        private readonly string roleCode;

        public FakePlayerRoleCodeReader(string roleCode)
        {
            this.roleCode = roleCode;
        }

        public string Read(IServerPlayer player)
        {
            return roleCode;
        }
    }

    private sealed class FakePlayerRoleAssigner : IPlayerRoleAssigner
    {
        public string? LastAssignedRoleCode { get; private set; }

        public void Assign(IServerPlayer player, string roleCode)
        {
            LastAssignedRoleCode = roleCode;
        }
    }

    private sealed class FakePlayerDefaultRoleResetter : IPlayerDefaultRoleResetter
    {
        private readonly string defaultRoleCode;

        public FakePlayerDefaultRoleResetter(string defaultRoleCode)
        {
            this.defaultRoleCode = defaultRoleCode;
        }

        public int ResetCount { get; private set; }

        public void Reset(IServerPlayer player)
        {
            ResetCount++;
        }

        public string GetDefaultRoleCode()
        {
            return defaultRoleCode;
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

            if (returnType.IsValueType)
            {
                return Activator.CreateInstance(returnType);
            }

            return null;
        }
    }
}
