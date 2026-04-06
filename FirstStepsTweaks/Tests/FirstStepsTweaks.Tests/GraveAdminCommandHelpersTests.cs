using System.Reflection;
using System.Runtime.CompilerServices;
using FirstStepsTweaks.Commands;
using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class GraveAdminCommandHelpersTests
{
    [Fact]
    public void EntryFormatter_FormatsSingleGrave_WithOptionalDistance()
    {
        var formatter = new GraveAdminEntryFormatter(new FakeCoordinateDisplayFormatter());
        GraveData grave = new()
        {
            GraveId = "grave-1",
            OwnerName = "Owner 1",
            OwnerUid = "uid-1",
            X = 1,
            Y = 70,
            Z = -1,
            Dimension = 0,
            CreatedUnixMs = DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeMilliseconds()
        };

        string message = formatter.Format(grave, "owner-only", distanceBlocks: 1.5);

        Assert.Contains("graveId=grave-1", message);
        Assert.Contains("owner=Owner 1", message);
        Assert.Contains("ownerUid=uid-1", message);
        Assert.Contains("displayPos=display:0:1,70,-1", message);
        Assert.Contains("worldPos=0:1,70,-1", message);
        Assert.Contains("distanceBlocks=1.5", message);
        Assert.Contains("ageMinutes=", message);
        Assert.Contains("claimState=owner-only", message);
    }

    [Fact]
    public void NearbyQuery_FiltersByDimensionAndRadius_AndSortsNearestFirst()
    {
        IServerPlayer caller = CreatePlayer("admin-uid", "Admin", 0.25, 65, 0.25, 2);
        var query = new GraveAdminNearbyQuery(new WorldCoordinateReader());
        var graves = new[]
        {
            new GraveData { GraveId = "far", OwnerName = "Far", OwnerUid = "far-uid", X = 150, Y = 65, Z = 0, Dimension = 2, CreatedUnixMs = 400 },
            new GraveData { GraveId = "other-dim", OwnerName = "Other", OwnerUid = "other-uid", X = 1, Y = 65, Z = 1, Dimension = 3, CreatedUnixMs = 100 },
            new GraveData { GraveId = "second", OwnerName = "Second", OwnerUid = "second-uid", X = 4, Y = 65, Z = 0, Dimension = 2, CreatedUnixMs = 300 },
            new GraveData { GraveId = "first", OwnerName = "First", OwnerUid = "first-uid", X = 1, Y = 65, Z = 0, Dimension = 2, CreatedUnixMs = 200 }
        };

        bool success = query.TryQuery(
            caller,
            graves,
            grave => string.Equals(grave.GraveId, "second", StringComparison.OrdinalIgnoreCase),
            radius: 100,
            out IReadOnlyList<GraveAdminListEntry> entries,
            out string message);

        Assert.True(success);
        Assert.Equal(string.Empty, message);
        Assert.Collection(
            entries,
            first =>
            {
                Assert.Equal(1, first.Index);
                Assert.Equal("first", first.Grave.GraveId);
                Assert.Equal("owner-only", first.ClaimState);
            },
            second =>
            {
                Assert.Equal(2, second.Index);
                Assert.Equal("second", second.Grave.GraveId);
                Assert.Equal("public", second.ClaimState);
            });
    }

    [Fact]
    public void PageFormatter_FormatsRequestedPage_WithGlobalIndexesAndRawData()
    {
        var formatter = new GraveAdminPageFormatter(new GraveAdminEntryFormatter(new FakeCoordinateDisplayFormatter()));
        GraveAdminListEntry[] entries =
        [
            CreateEntry(1, "grave-1", 0.5, "owner-only"),
            CreateEntry(2, "grave-2", 1.5, "owner-only"),
            CreateEntry(3, "grave-3", 2.5, "public"),
            CreateEntry(4, "grave-4", 3.5, "owner-only"),
            CreateEntry(5, "grave-5", 4.5, "owner-only"),
            CreateEntry(6, "grave-6", 5.5, "public")
        ];

        bool success = formatter.TryFormat(entries, radius: 100, page: 2, out string message);

        Assert.True(success);
        Assert.Contains("Nearby gravestones (6) within 100 blocks | Page 2/2", message);
        Assert.Contains("========================================", message);
        Assert.Contains("[6] graveId=grave-6", message);
        Assert.Contains("owner=Owner 6", message);
        Assert.Contains("ownerUid=uid-6", message);
        Assert.Contains("displayPos=display:0:6,70,-6", message);
        Assert.Contains("worldPos=0:6,70,-6", message);
        Assert.Contains("distanceBlocks=5.5", message);
        Assert.Contains("claimState=public", message);
        Assert.DoesNotContain("[5] graveId=grave-5", message);
    }

    [Fact]
    public void PageFormatter_RejectsOutOfRangePage()
    {
        var formatter = new GraveAdminPageFormatter(new GraveAdminEntryFormatter(new FakeCoordinateDisplayFormatter()));

        bool success = formatter.TryFormat([CreateEntry(1, "grave-1", 0.5, "owner-only")], radius: 100, page: 2, out string message);

        Assert.False(success);
        Assert.Equal("Page 2 is out of range. Valid pages: 1-1.", message);
    }

    [Fact]
    public void SnapshotStore_OverwritesLastListForPlayer()
    {
        var store = new GraveAdminListSnapshotStore();
        IServerPlayer caller = CreatePlayer("admin-uid", "Admin", 0, 65, 0, 0);

        store.Save(caller, 100, [CreateEntry(1, "grave-1", 1, "owner-only")]);
        store.Save(caller, 50, [CreateEntry(1, "grave-2", 2, "public")]);

        Assert.True(store.TryGet(caller, out GraveAdminListSnapshot? snapshot));
        Assert.NotNull(snapshot);
        Assert.Equal(50, snapshot!.Radius);
        Assert.Single(snapshot.Entries);
        Assert.Equal("grave-2", snapshot.Entries[0].Grave.GraveId);
    }

    [Fact]
    public void SelectorResolver_ResolvesRawId_CurrentLoc_AndNumericIndex()
    {
        var store = new GraveAdminListSnapshotStore();
        IServerPlayer caller = CreatePlayer("admin-uid", "Admin", 0, 65, 0, 0);
        store.Save(caller, 100, [CreateEntry(1, "grave-1", 1, "owner-only"), CreateEntry(2, "grave-2", 2, "public")]);

        var resolver = new GraveAdminSelectorResolver(store, new FakeGraveAdminGraveResolver("grave-current"));

        Assert.True(resolver.TryResolve(caller, "grave-raw-id", out string rawId, out string rawMessage));
        Assert.Equal("grave-raw-id", rawId);
        Assert.Equal(string.Empty, rawMessage);

        Assert.True(resolver.TryResolve(caller, "currentloc", out string currentId, out string currentMessage));
        Assert.Equal("grave-current", currentId);
        Assert.Equal(string.Empty, currentMessage);

        Assert.True(resolver.TryResolve(caller, "2", out string indexedId, out string indexedMessage));
        Assert.Equal("grave-2", indexedId);
        Assert.Equal(string.Empty, indexedMessage);
    }

    [Fact]
    public void SelectorResolver_RejectsMissingSnapshot_AndOutOfRangeIndex()
    {
        IServerPlayer caller = CreatePlayer("admin-uid", "Admin", 0, 65, 0, 0);
        var store = new GraveAdminListSnapshotStore();
        var resolver = new GraveAdminSelectorResolver(store, new FakeGraveAdminGraveResolver("grave-current"));

        Assert.False(resolver.TryResolve(caller, "1", out _, out string missingSnapshotMessage));
        Assert.Equal("Run /graveadmin list first, then use the grave number from that list.", missingSnapshotMessage);

        store.Save(caller, 100, [CreateEntry(1, "grave-1", 1, "owner-only")]);

        Assert.False(resolver.TryResolve(caller, "3", out _, out string outOfRangeMessage));
        Assert.Equal("Grave number 3 is not on your last /graveadmin list. Valid range is 1-1.", outOfRangeMessage);
    }

    [Fact]
    public void RestoreTargetResolver_UsesExplicitTargetWhenProvided()
    {
        IServerPlayer explicitTarget = CreatePlayer("explicit-uid", "Explicit", 0, 65, 0, 0);
        var resolver = new GraveAdminRestoreTargetResolver(new FakePlayerLookup(explicitTarget));

        bool success = resolver.TryResolve("Explicit", new GraveData { OwnerUid = "owner-uid", OwnerName = "Owner" }, out IServerPlayer? target, out string message);

        Assert.True(success);
        Assert.Same(explicitTarget, target);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void RestoreTargetResolver_UsesOwnerWhenOnline()
    {
        IServerPlayer owner = CreatePlayer("owner-uid", "Owner", 0, 65, 0, 0);
        var resolver = new GraveAdminRestoreTargetResolver(new FakePlayerLookup(owner));

        bool success = resolver.TryResolve(null, new GraveData { OwnerUid = "owner-uid", OwnerName = "Owner" }, out IServerPlayer? target, out string message);

        Assert.True(success);
        Assert.Same(owner, target);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void RestoreTargetResolver_FailsWhenOwnerIsOffline()
    {
        var resolver = new GraveAdminRestoreTargetResolver(new FakePlayerLookup(null));

        bool success = resolver.TryResolve(null, new GraveData { OwnerUid = "owner-uid", OwnerName = "Owner" }, out IServerPlayer? target, out string message);

        Assert.False(success);
        Assert.Null(target);
        Assert.Equal("Grave owner 'Owner' must be online to restore without specifying a player.", message);
    }

    [Fact]
    public void InfoPresenter_SendsInfoAndGeneral_WhenLookedAtGraveExists()
    {
        IServerPlayer caller = CreatePlayer("admin-uid", "Admin", 0, 65, 0, 0);
        GraveData grave = CreateEntry(1, "grave-1", 1, "owner-only").Grave;
        var messenger = new FakePlayerMessenger();
        var presenter = new GraveAdminInfoPresenter(
            new FakeGraveAdminGraveResolver("grave-1", grave),
            graveData => false,
            messenger,
            new FakeWorldCoordinateReader(new Vec3d(0, 65, 0), 0),
            new GraveAdminEntryFormatter(new FakeCoordinateDisplayFormatter()));

        presenter.ShowLookedAtGraveInfo(caller);

        Assert.Single(messenger.InfoMessages);
        Assert.Contains("Looked-at gravestone:", messenger.InfoMessages[0]);
        Assert.Contains("graveId=grave-1", messenger.InfoMessages[0]);
        Assert.Contains("distanceBlocks=", messenger.InfoMessages[0]);
        Assert.Single(messenger.GeneralMessages);
        Assert.Equal("Looked-at gravestone details were sent to your Info log channel.", messenger.GeneralMessages[0]);
        Assert.Empty(messenger.DualMessages);
    }

    [Fact]
    public void InfoPresenter_SendsDual_WhenTargetResolutionFails()
    {
        IServerPlayer caller = CreatePlayer("admin-uid", "Admin", 0, 65, 0, 0);
        var messenger = new FakePlayerMessenger();
        var presenter = new GraveAdminInfoPresenter(
            new FakeGraveAdminGraveResolver(null, null, false, "Look directly at a valid gravestone or specify the grave ID."),
            graveData => false,
            messenger,
            new FakeWorldCoordinateReader(new Vec3d(0, 65, 0), 0),
            new GraveAdminEntryFormatter(new FakeCoordinateDisplayFormatter()));

        presenter.ShowLookedAtGraveInfo(caller);

        Assert.Single(messenger.DualMessages);
        Assert.Equal("Look directly at a valid gravestone or specify the grave ID.", messenger.DualMessages[0]);
        Assert.Empty(messenger.InfoMessages);
        Assert.Empty(messenger.GeneralMessages);
    }

    [Fact]
    public void InfoPresenter_SendsDual_WhenResolvedGraveIsMissing()
    {
        IServerPlayer caller = CreatePlayer("admin-uid", "Admin", 0, 65, 0, 0);
        var messenger = new FakePlayerMessenger();
        var presenter = new GraveAdminInfoPresenter(
            new FakeGraveAdminGraveResolver("grave-missing", null),
            graveData => false,
            messenger,
            new FakeWorldCoordinateReader(new Vec3d(0, 65, 0), 0),
            new GraveAdminEntryFormatter(new FakeCoordinateDisplayFormatter()));

        presenter.ShowLookedAtGraveInfo(caller);

        Assert.Single(messenger.DualMessages);
        Assert.Equal("Gravestone 'grave-missing' was not found.", messenger.DualMessages[0]);
        Assert.Empty(messenger.InfoMessages);
        Assert.Empty(messenger.GeneralMessages);
    }

    private static GraveAdminListEntry CreateEntry(int index, string graveId, double distanceBlocks, string claimState)
    {
        return new GraveAdminListEntry(
            index,
            new GraveData
            {
                GraveId = graveId,
                OwnerName = $"Owner {index}",
                OwnerUid = $"uid-{index}",
                X = index,
                Y = 70,
                Z = -index,
                Dimension = 0,
                CreatedUnixMs = DateTimeOffset.UtcNow.AddMinutes(-index).ToUnixTimeMilliseconds()
            },
            distanceBlocks,
            claimState);
    }

    private static IServerPlayer CreatePlayer(string uid, string name, double x, double y, double z, int dimension)
    {
        var player = DispatchProxy.Create<IServerPlayer, GraveAdminServerPlayerProxy>();
        var proxy = (GraveAdminServerPlayerProxy)(object)player;
        proxy.PlayerUid = uid;
        proxy.PlayerName = name;
        proxy.Entity = CreateEntity(x, y, z, dimension);
        return player;
    }

    private static EntityPlayer CreateEntity(double x, double y, double z, int dimension)
    {
        var entity = (EntityPlayer)RuntimeHelpers.GetUninitializedObject(typeof(EntityPlayer));
        var position = new EntityPos(x, y, z)
        {
            Dimension = dimension
        };

        typeof(Vintagestory.API.Common.Entities.Entity).GetField("Pos", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(entity, position);

        return entity;
    }

    private sealed class FakeCoordinateDisplayFormatter : IWorldCoordinateDisplayFormatter
    {
        public Vec3d ToDisplayPosition(Vec3d worldPosition)
        {
            return worldPosition;
        }

        public BlockPos ToDisplayPosition(BlockPos worldPosition)
        {
            return worldPosition;
        }

        public string FormatBlockPosition(int dimension, int x, int y, int z)
        {
            return $"display:{dimension}:{x},{y},{z}";
        }

        public string FormatBlockPosition(BlockPos worldPosition)
        {
            return $"display:{worldPosition.dimension}:{worldPosition.X},{worldPosition.Y},{worldPosition.Z}";
        }

        public string FormatBlockPositionWithoutDimension(BlockPos worldPosition)
        {
            return $"display:{worldPosition.X},{worldPosition.Y},{worldPosition.Z}";
        }
    }

    private sealed class FakePlayerLookup : IPlayerLookup
    {
        private readonly IServerPlayer? player;

        public FakePlayerLookup(IServerPlayer? player)
        {
            this.player = player;
        }

        public IServerPlayer FindOnlinePlayerByUid(string uid)
        {
            return player != null && string.Equals(player.PlayerUID, uid, StringComparison.OrdinalIgnoreCase) ? player : null!;
        }

        public IServerPlayer FindOnlinePlayerByName(string name)
        {
            return player != null && string.Equals(player.PlayerName, name, StringComparison.OrdinalIgnoreCase) ? player : null!;
        }
    }

    private sealed class FakeGraveAdminGraveResolver : IGraveAdminGraveResolver
    {
        private readonly string? targetedGraveId;
        private readonly GraveData? activeGrave;
        private readonly bool resolveSucceeds;
        private readonly string resolutionMessage;

        public FakeGraveAdminGraveResolver(string? targetedGraveId, GraveData? activeGrave = null, bool resolveSucceeds = true, string resolutionMessage = "")
        {
            this.targetedGraveId = targetedGraveId;
            this.activeGrave = activeGrave;
            this.resolveSucceeds = resolveSucceeds;
            this.resolutionMessage = resolutionMessage;
        }

        public bool TryResolveTargetedGraveId(IServerPlayer player, out string graveId, out string message)
        {
            graveId = targetedGraveId ?? string.Empty;
            message = resolutionMessage;
            return resolveSucceeds;
        }

        public bool TryGetActiveGrave(string graveId, out GraveData grave)
        {
            grave = activeGrave!;
            return activeGrave != null && string.Equals(activeGrave.GraveId, graveId, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class FakeWorldCoordinateReader : IWorldCoordinateReader
    {
        private readonly Vec3d? exactPosition;
        private readonly int? dimension;

        public FakeWorldCoordinateReader(Vec3d? exactPosition, int? dimension)
        {
            this.exactPosition = exactPosition;
            this.dimension = dimension;
        }

        public Vec3d GetExactPosition(IServerPlayer player)
        {
            return exactPosition;
        }

        public Vec3d GetExactPosition(Vintagestory.API.Common.Entities.Entity entity)
        {
            return exactPosition;
        }

        public BlockPos GetBlockPosition(IServerPlayer player)
        {
            return null!;
        }

        public BlockPos GetBlockPosition(Vintagestory.API.Common.Entities.Entity entity)
        {
            return null!;
        }

        public int? GetDimension(IServerPlayer player)
        {
            return dimension;
        }

        public int? GetDimension(Vintagestory.API.Common.Entities.Entity entity)
        {
            return dimension;
        }
    }

    private sealed class FakePlayerMessenger : IPlayerMessenger
    {
        public List<string> InfoMessages { get; } = [];
        public List<string> GeneralMessages { get; } = [];
        public List<string> DualMessages { get; } = [];

        public void SendInfo(IServerPlayer player, string message, int groupId, int chatType)
        {
            InfoMessages.Add(message);
        }

        public void SendGeneral(IServerPlayer player, string message, int groupId, int chatType)
        {
            GeneralMessages.Add(message);
        }

        public void SendDual(IServerPlayer player, string message, int infoChatType, int generalChatType)
        {
            DualMessages.Add(message);
        }

        public void SendDual(IServerPlayer player, string message, int infoGroupId, int infoChatType, int generalGroupId, int generalChatType)
        {
            DualMessages.Add(message);
        }

        public void SendIngameError(IServerPlayer player, string code, string message)
        {
        }
    }

    private class GraveAdminServerPlayerProxy : DispatchProxy
    {
        public string PlayerUid { get; set; } = string.Empty;

        public string PlayerName { get; set; } = string.Empty;

        public EntityPlayer? Entity { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null)
            {
                return null;
            }

            return targetMethod.Name switch
            {
                "get_PlayerUID" => PlayerUid,
                "get_PlayerName" => PlayerName,
                "get_Entity" => Entity,
                _ => targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null
            };
        }
    }
}
