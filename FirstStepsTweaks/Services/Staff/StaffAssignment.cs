namespace FirstStepsTweaks.Services
{
    public sealed class StaffAssignment
    {
        public string PlayerUid { get; set; } = string.Empty;

        public string LastKnownPlayerName { get; set; } = string.Empty;

        public StaffLevel Level { get; set; } = StaffLevel.None;
    }
}
