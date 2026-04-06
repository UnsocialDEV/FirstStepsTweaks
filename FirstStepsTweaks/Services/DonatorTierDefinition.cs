namespace FirstStepsTweaks.Services
{
    public sealed class DonatorTierDefinition
    {
        public DonatorTierDefinition(DonatorTier tier, string label, string discordRoleName, string roleCode, string legacyPrivilege)
        {
            Tier = tier;
            Label = label;
            DiscordRoleName = discordRoleName;
            RoleCode = roleCode;
            LegacyPrivilege = legacyPrivilege;
        }

        public DonatorTier Tier { get; }

        public string Label { get; }

        public string DiscordRoleName { get; }

        public string RoleCode { get; }

        public string LegacyPrivilege { get; }
    }
}
