using System.Collections.Generic;

namespace FirstStepsTweaks.Config
{
    public sealed class LegacyFirstStepsTweaksConfig
    {
        public LegacyUtilityConfig Utility { get; set; } = new LegacyUtilityConfig();
    }

    public sealed class LegacyUtilityConfig
    {
        public List<string> AdminPlayerNames { get; set; } = new List<string>();
    }
}
