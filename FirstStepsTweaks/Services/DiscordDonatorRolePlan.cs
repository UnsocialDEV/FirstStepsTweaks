namespace FirstStepsTweaks.Services
{
    public sealed class DiscordDonatorRolePlan
    {
        public DiscordDonatorRolePlan(string targetRoleCode)
        {
            TargetRoleCode = targetRoleCode;
        }

        public string TargetRoleCode { get; }
    }
}
