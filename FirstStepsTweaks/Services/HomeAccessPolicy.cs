using System;
using System.Collections.Generic;
using System.Linq;
using FirstStepsTweaks.Teleport;

namespace FirstStepsTweaks.Services
{
    public sealed class HomeAccessPolicy
    {
        public IReadOnlyDictionary<string, HomeLocation> GetAccessibleHomes(IReadOnlyDictionary<string, HomeLocation> homes, int maxHomes)
        {
            var allHomes = homes ?? new Dictionary<string, HomeLocation>(StringComparer.OrdinalIgnoreCase);
            if (allHomes.Count == 0 || maxHomes <= 0)
            {
                return new Dictionary<string, HomeLocation>(StringComparer.OrdinalIgnoreCase);
            }

            return allHomes
                .OrderBy(pair => pair.Value?.CreatedOrder ?? long.MaxValue)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Take(maxHomes)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        public bool CanUseHome(IReadOnlyDictionary<string, HomeLocation> homes, string normalizedHomeName, int maxHomes)
        {
            return GetAccessibleHomes(homes, maxHomes).ContainsKey(normalizedHomeName);
        }
    }
}
