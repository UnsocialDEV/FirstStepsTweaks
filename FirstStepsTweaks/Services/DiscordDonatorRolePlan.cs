namespace FirstStepsTweaks.Services
{
    public sealed class DiscordDonatorRolePlan
    {
        public DiscordDonatorRolePlan(string targetPrivilege)
        {
            TargetPrivilege = targetPrivilege;
        }

        public string TargetPrivilege { get; }
    }
}
