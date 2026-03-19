namespace FirstStepsTweaks.Services
{
    public sealed class LandClaimMessageFormatter
    {
        public string BuildMessage(string template, string claimName, string ownerUid, string ownerName, string playerUid, string playerName)
        {
            string normalizedOwnerName = NormalizeOwnerName(ownerName);
            if (string.IsNullOrWhiteSpace(normalizedOwnerName) || normalizedOwnerName == playerUid)
            {
                normalizedOwnerName = "Unknown";
            }

            if (!string.IsNullOrWhiteSpace(ownerUid) && ownerUid == playerUid)
            {
                normalizedOwnerName = "your";
            }

            string safeClaimName = string.IsNullOrWhiteSpace(claimName) ? "Unnamed claim" : claimName;

            return (template ?? string.Empty)
                .Replace("{owner}", normalizedOwnerName)
                .Replace("{claim}", safeClaimName)
                .Replace("{player}", playerName ?? string.Empty);
        }

        public string NormalizeOwnerName(string ownerName)
        {
            if (string.IsNullOrWhiteSpace(ownerName))
            {
                return ownerName;
            }

            const string playerPrefix = "Player ";
            string normalized = ownerName.Trim();
            if (normalized.StartsWith(playerPrefix))
            {
                return normalized.Substring(playerPrefix.Length).TrimStart();
            }

            return normalized;
        }
    }
}
