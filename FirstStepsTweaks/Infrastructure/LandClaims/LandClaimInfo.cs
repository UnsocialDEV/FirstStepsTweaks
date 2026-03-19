namespace FirstStepsTweaks.Infrastructure.LandClaims
{
    public sealed class LandClaimInfo
    {
        public static LandClaimInfo None { get; } = new LandClaimInfo(string.Empty, string.Empty, string.Empty, string.Empty);

        public LandClaimInfo(string key, string claimName, string ownerUid, string ownerName)
        {
            Key = key ?? string.Empty;
            ClaimName = claimName ?? string.Empty;
            OwnerUid = ownerUid ?? string.Empty;
            OwnerName = ownerName ?? string.Empty;
        }

        public string Key { get; }
        public string ClaimName { get; }
        public string OwnerUid { get; }
        public string OwnerName { get; }
        public bool Exists => !string.IsNullOrWhiteSpace(Key);
    }
}
