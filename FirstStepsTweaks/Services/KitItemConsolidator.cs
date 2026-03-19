using System.Collections.Generic;
using FirstStepsTweaks.Config;

namespace FirstStepsTweaks.Services
{
    public sealed class KitItemConsolidator
    {
        public Dictionary<string, int> Consolidate(List<KitItemConfig> items)
        {
            var consolidated = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            if (items == null)
            {
                return consolidated;
            }

            foreach (KitItemConfig item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Code) || item.Quantity <= 0)
                {
                    continue;
                }

                if (consolidated.ContainsKey(item.Code))
                {
                    continue;
                }

                consolidated[item.Code] = item.Quantity;
            }

            return consolidated;
        }
    }
}
