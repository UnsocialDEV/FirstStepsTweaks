using System.Collections.Generic;
using FirstStepsTweaks.Teleport;

namespace FirstStepsTweaks.Services
{
    public sealed class HomeSlotPolicy
    {
        public bool CanCreate(IReadOnlyDictionary<string, HomeLocation> homes, string normalizedHomeName, int maxHomes)
        {
            if (homes == null || homes.ContainsKey(normalizedHomeName))
            {
                return true;
            }

            return homes.Count < maxHomes;
        }
    }
}
