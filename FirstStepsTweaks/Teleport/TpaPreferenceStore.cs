using Vintagestory.API.Server;

namespace FirstStepsTweaks.Teleport
{
    public sealed class TpaPreferenceStore
    {
        private const string TpaDisabledKey = "fst_tpa_disabled";

        public bool IsDisabled(IServerPlayer player)
        {
            return player?.GetModData<bool>(TpaDisabledKey) == true;
        }

        public void SetDisabled(IServerPlayer player, bool value)
        {
            player?.SetModData(TpaDisabledKey, value);
        }
    }
}
