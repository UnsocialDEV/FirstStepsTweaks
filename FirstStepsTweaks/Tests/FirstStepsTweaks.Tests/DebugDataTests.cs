using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Services;
using FirstStepsTweaks.Teleport;
using Xunit;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Tests;

public class DebugDataTests
{
    [Fact]
    public void PlayerStores_SupportExplicitSettersAndClearers()
    {
        IServerPlayer player = CreatePlayer("uid-1", "Admin");
        var joinStore = new JoinHistoryStore();
        var kitStore = new KitClaimStore();
        var playtimeStore = new PlayerPlaytimeStore();
        var homeStore = new HomeStore();

        joinStore.SetFirstJoinRecorded(player, true);
        joinStore.SetLastSeenDay(player, 42.5);
        kitStore.SetStarterClaimed(player, true);
        kitStore.SetWinterClaimed(player, false);
        playtimeStore.SetTotalPlayedSeconds(player, 120);
        homeStore.Set(player, "base", 1, 2, 3);

        Assert.True(joinStore.HasJoinedBefore(player));
        Assert.Equal(42.5, joinStore.GetLastSeenTotalDays(player));
        Assert.True(kitStore.HasStarterClaim(player));
        Assert.False(kitStore.HasWinterClaim(player));
        Assert.Equal(120, playtimeStore.GetTotalPlayedSeconds(player));
        Assert.True(homeStore.Contains(player, "base"));

        joinStore.SetFirstJoinRecorded(player, false);
        joinStore.SetLastSeenDay(player, null);
        kitStore.SetStarterClaimed(player, false);
        playtimeStore.ResetTotalPlayedSeconds(player);
        homeStore.Clear(player);

        Assert.False(joinStore.HasJoinedBefore(player));
        Assert.Null(joinStore.GetLastSeenTotalDays(player));
        Assert.False(kitStore.HasStarterClaim(player));
        Assert.Equal(0, playtimeStore.GetTotalPlayedSeconds(player));
        Assert.Empty(homeStore.GetAll(player));
    }

    [Fact]
    public void PlayerDebugInspector_FormatsDecodedSummary()
    {
        IServerPlayer player = CreatePlayer("uid-2", "Builder");
        var joinStore = new JoinHistoryStore();
        var kitStore = new KitClaimStore();
        var playtimeStore = new PlayerPlaytimeStore();
        var homeStore = new HomeStore();
        var tpaStore = new TpaPreferenceStore();
        var inspector = new PlayerDebugDataInspector(joinStore, kitStore, playtimeStore, homeStore, tpaStore);

        joinStore.SetFirstJoinRecorded(player, true);
        joinStore.SetLastSeenDay(player, 12.25);
        kitStore.SetStarterClaimed(player, true);
        playtimeStore.SetTotalPlayedSeconds(player, 3600);
        homeStore.Set(player, "mine", 9, 8, 7);
        tpaStore.SetDisabled(player, true);

        string summary = inspector.Format(inspector.Capture(player));

        Assert.Contains("Player data for Builder (uid-2)", summary);
        Assert.Contains("firstJoinRecorded: True", summary);
        Assert.Contains("lastSeenTotalDays: 12.25", summary);
        Assert.Contains("starterKitClaimed: True", summary);
        Assert.Contains("totalPlayedSeconds: 3600", summary);
        Assert.Contains("tpaDisabled: True", summary);
        Assert.Contains("mine: 9, 8, 7", summary);
    }

    [Fact]
    public void DiscordDebugReader_FormatsAllTrackedDiscordState()
    {
        var linkedStore = new FakeLinkedAccountStore();
        var pendingStore = new FakePendingCodeStore();
        var rewardStore = new FakeRewardStateStore();
        var relayCursorStore = new FakeLastMessageStore();
        var linkCursorStore = new FakeLinkLastMessageStore();
        var linkPollerStatusTracker = new DiscordLinkPollerStatusTracker();
        var reader = new DiscordDebugStateReader(linkedStore, pendingStore, rewardStore, relayCursorStore, linkCursorStore, linkPollerStatusTracker);

        linkedStore.SetLinkedDiscordUserId("uid-1", "discord-1");
        pendingStore.SaveCode("ABC123", new PendingDiscordLinkCodeRecord("uid-1", 999));
        rewardStore.MarkClaimed("uid-1");
        rewardStore.MarkPendingReward("uid-2");
        relayCursorStore.Save("relay-55");
        linkCursorStore.Save("link-77");
        linkPollerStatusTracker.RecordSuccess(new DateTime(2026, 03, 24, 12, 0, 0, DateTimeKind.Utc), 3, 42, true);
        linkPollerStatusTracker.RecordFailure("Discord link poll returned 401 Unauthorized. Check the configured bot token.");
        linkPollerStatusTracker.RecordSuccess(new DateTime(2026, 03, 24, 12, 5, 0, DateTimeKind.Utc), 2, 12, true);

        string summary = reader.Format(reader.Capture(DateTime.UnixEpoch));

        Assert.Contains("linkedAccounts: 1", summary);
        Assert.Contains("uid-1 => discord-1", summary);
        Assert.Contains("pendingCodes: 1", summary);
        Assert.Contains("ABC123: playerUid=uid-1, expiresUtcTicks=999", summary);
        Assert.Contains("claimedRewards: 1", summary);
        Assert.Contains("pendingRewards: 1", summary);
        Assert.Contains("relayCursorMessageId: relay-55", summary);
        Assert.Contains("linkCursorMessageId: link-77", summary);
        Assert.Contains("linkPollLastSuccessfulUtc: 2026-03-24 12:05:00Z", summary);
        Assert.Contains("linkPollLastFailureSummary: unset", summary);
        Assert.Contains("linkPollLastProcessedPageCount: 2", summary);
        Assert.Contains("linkPollLastProcessedMessageCount: 12", summary);
        Assert.Contains("linkPollLastPollReachedProcessingCap: True", summary);
    }

    private static IServerPlayer CreatePlayer(string playerUid, string playerName)
    {
        IServerPlayer proxy = DispatchProxy.Create<IServerPlayer, TestServerPlayerProxy>();
        var state = (TestServerPlayerProxy)(object)proxy;
        state.PlayerUid = playerUid;
        state.PlayerName = playerName;
        return proxy;
    }

    private class TestServerPlayerProxy : DispatchProxy
    {
        private readonly Dictionary<string, byte[]?> modData = new(StringComparer.Ordinal);
        private readonly Dictionary<string, object?> typedModData = new(StringComparer.Ordinal);

        public string PlayerUid { get; set; } = string.Empty;

        public string PlayerName { get; set; } = string.Empty;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null)
            {
                return null;
            }

            switch (targetMethod.Name)
            {
                case "get_PlayerUID":
                    return PlayerUid;
                case "get_PlayerName":
                    return PlayerName;
                case "GetModdata":
                    return modData.TryGetValue((string)args![0]!, out byte[]? value) ? value : null;
                case "SetModdata":
                    modData[(string)args![0]!] = (byte[]?)args[1];
                    return null;
                case "GetModData":
                    {
                        string key = (string)args![0]!;
                        if (typedModData.TryGetValue(key, out object? storedValue))
                        {
                            return storedValue;
                        }

                        Type returnType = targetMethod.ReturnType;
                        return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
                    }
                case "SetModData":
                    typedModData[(string)args![0]!] = args![1];
                    return null;
                default:
                    return targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
            }
        }
    }

    private sealed class FakeLinkedAccountStore : IDiscordLinkedAccountStore
    {
        private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        public string GetLinkedDiscordUserId(string playerUid) => values.TryGetValue(playerUid, out string? value) ? value : null;

        public IReadOnlyDictionary<string, string> GetAllLinkedDiscordUserIds() => values;

        public void SetLinkedDiscordUserId(string playerUid, string discordUserId) => values[playerUid] = discordUserId;

        public void ClearLinkedDiscordUserId(string playerUid) => values.Remove(playerUid);
    }

    private sealed class FakePendingCodeStore : IPendingDiscordLinkCodeStore
    {
        private readonly Dictionary<string, PendingDiscordLinkCodeRecord> values = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> GetPendingCodes(DateTime nowUtc) => GetPendingCodeRecords(nowUtc).Keys.ToArray();

        public IReadOnlyDictionary<string, PendingDiscordLinkCodeRecord> GetPendingCodeRecords(DateTime nowUtc) => values;

        public bool TryGetCode(string code, DateTime nowUtc, out PendingDiscordLinkCodeRecord record) => values.TryGetValue(code, out record);

        public void SaveCode(string code, PendingDiscordLinkCodeRecord record) => values[code] = record;

        public void RemoveCode(string code) => values.Remove(code);

        public void RemoveCodesForPlayer(string playerUid)
        {
            foreach (string code in values.Where(pair => pair.Value.PlayerUid == playerUid).Select(pair => pair.Key).ToArray())
            {
                values.Remove(code);
            }
        }
    }

    private sealed class FakeRewardStateStore : IDiscordLinkRewardStateStore
    {
        private readonly HashSet<string> claimed = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> pending = new(StringComparer.OrdinalIgnoreCase);

        public bool HasClaimed(string playerUid) => claimed.Contains(playerUid);

        public void MarkClaimed(string playerUid) => claimed.Add(playerUid);

        public void ClearClaimed(string playerUid) => claimed.Remove(playerUid);

        public bool HasPendingReward(string playerUid) => pending.Contains(playerUid);

        public void MarkPendingReward(string playerUid) => pending.Add(playerUid);

        public void ClearPendingReward(string playerUid) => pending.Remove(playerUid);

        public IReadOnlyCollection<string> GetClaimedPlayerUids() => claimed.ToArray();

        public IReadOnlyCollection<string> GetPendingRewardPlayerUids() => pending.ToArray();
    }

    private sealed class FakeLastMessageStore : IDiscordLastMessageStore
    {
        private string? value;

        public string Load() => value;

        public void Save(string lastMessageId) => value = lastMessageId;

        public void Clear() => value = null;
    }

    private sealed class FakeLinkLastMessageStore : IDiscordLinkLastMessageStore
    {
        private string? value;

        public string Load() => value;

        public void Save(string lastMessageId) => value = lastMessageId;

        public void Clear() => value = null;
    }
}
