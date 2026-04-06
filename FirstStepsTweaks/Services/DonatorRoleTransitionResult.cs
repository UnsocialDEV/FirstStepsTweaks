namespace FirstStepsTweaks.Services
{
    public sealed class DonatorRoleTransitionResult
    {
        public DonatorRoleTransitionResult(bool succeeded, bool changed, string effectiveRoleCode)
        {
            Succeeded = succeeded;
            Changed = changed;
            EffectiveRoleCode = effectiveRoleCode;
        }

        public bool Succeeded { get; }

        public bool Changed { get; }

        public string EffectiveRoleCode { get; }
    }
}
