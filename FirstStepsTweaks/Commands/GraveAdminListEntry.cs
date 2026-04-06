using FirstStepsTweaks.Services;

namespace FirstStepsTweaks.Commands
{
    public sealed class GraveAdminListEntry
    {
        public GraveAdminListEntry(int index, GraveData grave, double distanceBlocks, string claimState)
        {
            Index = index;
            Grave = grave;
            DistanceBlocks = distanceBlocks;
            ClaimState = claimState ?? "owner-only";
        }

        public int Index { get; }

        public GraveData Grave { get; }

        public double DistanceBlocks { get; }

        public string ClaimState { get; }
    }
}
