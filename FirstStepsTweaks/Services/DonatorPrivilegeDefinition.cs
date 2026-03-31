namespace FirstStepsTweaks.Services
{
    public sealed class DonatorPrivilegeDefinition
    {
        public DonatorPrivilegeDefinition(DonatorTier tier, string label, string roleName, string privilege)
        {
            Tier = tier;
            Label = label;
            RoleName = roleName;
            Privilege = privilege;
        }

        public DonatorTier Tier { get; }

        public string Label { get; }

        public string RoleName { get; }

        public string Privilege { get; }
    }
}
