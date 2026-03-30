using System.Collections.Generic;
using System.Linq;
using FirstStepsTweaks.Teleport;

namespace FirstStepsTweaks.Services
{
    public sealed class HomeDeletionTargetResolver
    {
        public (bool Success, string HomeName, string ErrorMessage) Resolve(IReadOnlyDictionary<string, HomeLocation> homes, string requestedHomeName)
        {
            string normalizedHomeName = new HomeNameNormalizer().Normalize(requestedHomeName);
            if (!string.IsNullOrWhiteSpace(normalizedHomeName))
            {
                return (true, normalizedHomeName, null);
            }

            if (homes == null || homes.Count == 0)
            {
                return (false, null, "You do not have any homes set.");
            }

            if (homes.Count == 1)
            {
                return (true, homes.Keys.Single(), null);
            }

            return (false, null, "You have multiple homes. Use /delhome <name>.");
        }
    }
}
