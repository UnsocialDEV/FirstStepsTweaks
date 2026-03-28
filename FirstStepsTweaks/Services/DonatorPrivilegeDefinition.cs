namespace FirstStepsTweaks.Services
{
    public sealed class DonatorPrivilegeDefinition
    {
        public DonatorPrivilegeDefinition(DonatorTier tier, string label, string roleName, string privilege, string inGameRoleCode)
        {
            Tier = tier;
            Label = label;
            RoleName = roleName;
            Privilege = privilege;
            InGameRoleCode = inGameRoleCode;
        }

        public DonatorTier Tier { get; }

        public string Label { get; }

        public string RoleName { get; }

        public string Privilege { get; }

        public string InGameRoleCode { get; }
    }
}
