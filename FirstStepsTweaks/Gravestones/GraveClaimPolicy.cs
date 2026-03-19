using System;
using FirstStepsTweaks.Services;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Gravestones
{
    public sealed class GraveClaimPolicy
    {
        public bool IsPubliclyClaimable(GraveData grave)
        {
            return grave != null && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= grave.ProtectionEndsUnixMs;
        }

        public bool CanPlayerClaim(IServerPlayer player, GraveData grave, out string denialMessage)
        {
            denialMessage = string.Empty;
            if (player == null || grave == null)
            {
                denialMessage = "Gravestone interaction is not available right now.";
                return false;
            }

            if (string.Equals(player.PlayerUID, grave.OwnerUid, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (now >= grave.ProtectionEndsUnixMs)
            {
                return true;
            }

            long remainingMs = grave.ProtectionEndsUnixMs - now;
            int remainingMinutes = Math.Max(1, (int)Math.Ceiling(remainingMs / 60000d));
            string ownerName = string.IsNullOrWhiteSpace(grave.OwnerName) ? "its owner" : grave.OwnerName;
            denialMessage = $"This gravestone belongs to {ownerName} and is owner-protected for {remainingMinutes} more minute(s).";
            return false;
        }
    }
}
