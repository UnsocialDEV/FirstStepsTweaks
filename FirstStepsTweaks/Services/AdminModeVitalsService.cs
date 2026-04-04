using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class AdminModeVitalsService : IAdminModeVitalsService
    {
        private const float DefaultMaxHealth = 20f;
        private const float DefaultMaxSaturation = 1500f;

        public void CaptureAndFill(IServerPlayer player, AdminModeState state)
        {
            if (player?.Entity?.WatchedAttributes == null || state == null)
            {
                return;
            }

            CaptureAndFillFromAttributes(player.Entity.WatchedAttributes, state);
            player.Entity.WatchedAttributes.MarkPathDirty("health");
            player.Entity.WatchedAttributes.MarkPathDirty("hunger");
        }

        public void EnsureFull(IServerPlayer player)
        {
            if (player?.Entity?.WatchedAttributes == null)
            {
                return;
            }

            EnsureFullFromAttributes(player.Entity.WatchedAttributes);
            player.Entity.WatchedAttributes.MarkPathDirty("health");
            player.Entity.WatchedAttributes.MarkPathDirty("hunger");
        }

        public void RestoreOrFull(IServerPlayer player, AdminModeState state)
        {
            if (player?.Entity?.WatchedAttributes == null)
            {
                return;
            }

            RestoreOrFullFromAttributes(player.Entity.WatchedAttributes, state);
            player.Entity.WatchedAttributes.MarkPathDirty("health");
            player.Entity.WatchedAttributes.MarkPathDirty("hunger");
        }

        private void CaptureAndFillFromAttributes(TreeAttribute watchedAttributes, AdminModeState state)
        {
            ITreeAttribute healthTree = watchedAttributes?.GetTreeAttribute("health");
            if (healthTree != null)
            {
                state.PriorCurrentHealth = healthTree.TryGetFloat("currenthealth");
                healthTree.SetFloat("currenthealth", ResolveMaxHealth(healthTree));
            }

            ITreeAttribute hungerTree = watchedAttributes?.GetTreeAttribute("hunger");
            if (hungerTree != null)
            {
                state.PriorCurrentSaturation = hungerTree.TryGetFloat("currentsaturation");
                hungerTree.SetFloat("currentsaturation", ResolveMaxSaturation(hungerTree));
            }
        }

        private void EnsureFullFromAttributes(TreeAttribute watchedAttributes)
        {
            ITreeAttribute healthTree = watchedAttributes?.GetTreeAttribute("health");
            if (healthTree != null)
            {
                healthTree.SetFloat("currenthealth", ResolveMaxHealth(healthTree));
            }

            ITreeAttribute hungerTree = watchedAttributes?.GetTreeAttribute("hunger");
            if (hungerTree != null)
            {
                hungerTree.SetFloat("currentsaturation", ResolveMaxSaturation(hungerTree));
            }
        }

        private void RestoreOrFullFromAttributes(TreeAttribute watchedAttributes, AdminModeState state)
        {
            ITreeAttribute healthTree = watchedAttributes?.GetTreeAttribute("health");
            if (healthTree != null)
            {
                float restoredHealth = state?.PriorCurrentHealth ?? ResolveMaxHealth(healthTree);
                healthTree.SetFloat("currenthealth", restoredHealth);
            }

            ITreeAttribute hungerTree = watchedAttributes?.GetTreeAttribute("hunger");
            if (hungerTree != null)
            {
                float restoredSaturation = state?.PriorCurrentSaturation ?? ResolveMaxSaturation(hungerTree);
                hungerTree.SetFloat("currentsaturation", restoredSaturation);
            }
        }

        private static float ResolveMaxHealth(ITreeAttribute healthTree)
        {
            float maxHealth = healthTree.TryGetFloat("maxhealth")
                ?? healthTree.TryGetFloat("basemaxhealth")
                ?? DefaultMaxHealth;

            return maxHealth <= 0 ? DefaultMaxHealth : maxHealth;
        }

        private static float ResolveMaxSaturation(ITreeAttribute hungerTree)
        {
            float maxSaturation = hungerTree.TryGetFloat("maxsaturation") ?? DefaultMaxSaturation;
            return maxSaturation <= 0 ? DefaultMaxSaturation : maxSaturation;
        }
    }
}
