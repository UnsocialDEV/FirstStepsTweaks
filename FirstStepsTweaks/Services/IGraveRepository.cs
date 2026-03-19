using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace FirstStepsTweaks.Services
{
    public interface IGraveRepository
    {
        List<GraveData> GetAll();
        bool TryGetById(string graveId, out GraveData grave);
        bool TryGetByPosition(BlockPos pos, out GraveData grave);
        bool Upsert(GraveData grave);
        bool Remove(string graveId, out GraveData removed);
        void Save();
    }
}
