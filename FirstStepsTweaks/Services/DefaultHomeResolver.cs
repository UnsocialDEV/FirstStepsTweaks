using System;
using System.Collections.Generic;
using System.Linq;
using FirstStepsTweaks.Teleport;

namespace FirstStepsTweaks.Services
{
    public sealed class DefaultHomeResolver
    {
        public KeyValuePair<string, HomeLocation>? Resolve(IReadOnlyDictionary<string, HomeLocation> homes)
        {
            if (homes == null || homes.Count == 0)
            {
                return null;
            }

            if (homes.TryGetValue(HomeStore.MigratedLegacyHomeName, out HomeLocation defaultHome))
            {
                return new KeyValuePair<string, HomeLocation>(HomeStore.MigratedLegacyHomeName, defaultHome);
            }

            return homes
                .OrderBy(pair => pair.Value?.CreatedOrder ?? long.MaxValue)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .First();
        }
    }
}
