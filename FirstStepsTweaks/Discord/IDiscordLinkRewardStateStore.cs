using System.Collections.Generic;

namespace FirstStepsTweaks.Discord
{
    public interface IDiscordLinkRewardStateStore
    {
        bool HasClaimed(string playerUid);
        void MarkClaimed(string playerUid);
        void ClearClaimed(string playerUid);
        bool HasPendingReward(string playerUid);
        void MarkPendingReward(string playerUid);
        void ClearPendingReward(string playerUid);
        IReadOnlyCollection<string> GetClaimedPlayerUids();
        IReadOnlyCollection<string> GetPendingRewardPlayerUids();
    }
}
