using Vintagestory.API.MathTools;

namespace FirstStepsTweaks.Gravestones
{
    public readonly struct GravePlacementResult
    {
        public GravePlacementResult(BlockPos position, bool movedOutsideForeignClaim)
        {
            Position = position;
            MovedOutsideForeignClaim = movedOutsideForeignClaim;
        }

        public BlockPos Position { get; }
        public bool MovedOutsideForeignClaim { get; }
    }
}
