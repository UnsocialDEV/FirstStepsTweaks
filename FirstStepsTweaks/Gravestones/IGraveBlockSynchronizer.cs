using FirstStepsTweaks.Services;
using Vintagestory.API.Common;

namespace FirstStepsTweaks.Gravestones
{
    public interface IGraveBlockSynchronizer
    {
        string GraveBlockCode { get; }

        Block ResolveGraveBlock();

        void Ensure(GraveData grave);

        void RemoveIfPresent(GraveData grave);
    }
}
