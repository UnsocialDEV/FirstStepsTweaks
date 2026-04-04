using FirstStepsTweaks.Teleport;
using Xunit;

namespace FirstStepsTweaks.Tests;

public sealed class TpaRequestStoreTests
{
    [Fact]
    public void TryTakeFirst_ReturnsRequestsInInsertionOrder_ForMixedDirections()
    {
        var store = new TpaRequestStore();
        var first = CreateRequest("req-1", "Requester One", "target", "Target", TpaRequestDirection.RequesterToTarget);
        var second = CreateRequest("req-2", "Requester Two", "target", "Target", TpaRequestDirection.TargetToRequester);

        store.Add(first);
        store.Add(second);

        Assert.True(store.TryTakeFirst("target", out TpaRequestRecord acceptedFirst));
        Assert.Equal(first.CreatedSequence, acceptedFirst.CreatedSequence);
        Assert.Equal(TpaRequestDirection.RequesterToTarget, acceptedFirst.Direction);

        Assert.True(store.TryTakeFirst("target", out TpaRequestRecord acceptedSecond));
        Assert.Equal(second.CreatedSequence, acceptedSecond.CreatedSequence);
        Assert.Equal(TpaRequestDirection.TargetToRequester, acceptedSecond.Direction);
    }

    [Fact]
    public void TryCancelByRequester_CancelsOldestOutgoingRequest_AcrossTargets()
    {
        var store = new TpaRequestStore();
        var oldest = CreateRequest("req-1", "Requester", "target-1", "Target One", TpaRequestDirection.RequesterToTarget);
        var newest = CreateRequest("req-1", "Requester", "target-2", "Target Two", TpaRequestDirection.TargetToRequester);

        store.Add(oldest);
        store.Add(newest);

        Assert.True(store.TryCancelByRequester("req-1", out TpaRequestRecord cancelled));
        Assert.Equal(oldest.CreatedSequence, cancelled.CreatedSequence);
        Assert.Equal("target-1", cancelled.TargetUid);

        Assert.True(store.TryTakeFirst("target-2", out TpaRequestRecord remaining));
        Assert.Equal(newest.CreatedSequence, remaining.CreatedSequence);
    }

    [Fact]
    public void TryCancelByRequester_UsesStableOrder_WhenDirectionsAreMixed()
    {
        var store = new TpaRequestStore();
        var first = CreateRequest("req-1", "Requester", "target-1", "Target One", TpaRequestDirection.TargetToRequester);
        var second = CreateRequest("req-1", "Requester", "target-2", "Target Two", TpaRequestDirection.RequesterToTarget);
        var third = CreateRequest("req-1", "Requester", "target-3", "Target Three", TpaRequestDirection.TargetToRequester);

        store.Add(first);
        store.Add(second);
        store.Add(third);

        Assert.True(store.TryCancelByRequester("req-1", out TpaRequestRecord cancelledFirst));
        Assert.Equal(first.CreatedSequence, cancelledFirst.CreatedSequence);

        Assert.True(store.TryCancelByRequester("req-1", out TpaRequestRecord cancelledSecond));
        Assert.Equal(second.CreatedSequence, cancelledSecond.CreatedSequence);

        Assert.True(store.TryCancelByRequester("req-1", out TpaRequestRecord cancelledThird));
        Assert.Equal(third.CreatedSequence, cancelledThird.CreatedSequence);
    }

    private static TpaRequestRecord CreateRequest(string requesterUid, string requesterName, string targetUid, string targetName, TpaRequestDirection direction)
    {
        return new TpaRequestRecord
        {
            RequesterUid = requesterUid,
            RequesterName = requesterName,
            TargetUid = targetUid,
            TargetName = targetName,
            Direction = direction
        };
    }
}
