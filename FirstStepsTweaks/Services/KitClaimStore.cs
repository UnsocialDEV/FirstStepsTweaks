using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class KitClaimStore
    {
        private const string StarterKey = "fst_starterclaimed";
        private const string WinterKey = "fst_winterclaimed";
        private const string SupporterKey = "fst_supporterclaimed";

        public bool HasStarterClaim(IServerPlayer player)
        {
            return player?.GetModdata(StarterKey) != null;
        }

        public bool HasWinterClaim(IServerPlayer player)
        {
            return player?.GetModdata(WinterKey) != null;
        }

        public bool HasSupporterClaim(IServerPlayer player)
        {
            return player?.GetModdata(SupporterKey) != null;
        }

        public void MarkStarterClaimed(IServerPlayer player)
        {
            player?.SetModdata(StarterKey, new byte[] { 1 });
        }

        public void MarkWinterClaimed(IServerPlayer player)
        {
            player?.SetModdata(WinterKey, new byte[] { 1 });
        }

        public void MarkSupporterClaimed(IServerPlayer player)
        {
            player?.SetModdata(SupporterKey, new byte[] { 1 });
        }
    }
}
