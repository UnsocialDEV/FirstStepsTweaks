using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class KitClaimStore
    {
        private const string StarterKey = "fst_starterclaimed";
        private const string WinterKey = "fst_winterclaimed";

        public bool HasStarterClaim(IServerPlayer player)
        {
            return player?.GetModdata(StarterKey) != null;
        }

        public bool HasWinterClaim(IServerPlayer player)
        {
            return player?.GetModdata(WinterKey) != null;
        }

        public void MarkStarterClaimed(IServerPlayer player)
        {
            player?.SetModdata(StarterKey, new byte[] { 1 });
        }

        public void MarkWinterClaimed(IServerPlayer player)
        {
            player?.SetModdata(WinterKey, new byte[] { 1 });
        }

        public void SetStarterClaimed(IServerPlayer player, bool value)
        {
            player?.SetModdata(StarterKey, value ? new byte[] { 1 } : null);
        }

        public void SetWinterClaimed(IServerPlayer player, bool value)
        {
            player?.SetModdata(WinterKey, value ? new byte[] { 1 } : null);
        }

    }
}
