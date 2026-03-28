namespace FirstStepsTweaks.Config
{
    public sealed class TeleportConfigUpgrader
    {
        public bool TryUpgradeDonatorWarmupSeconds(FirstStepsTweaksConfig config)
        {
            if (config == null)
            {
                return false;
            }

            if (config.Teleport == null)
            {
                config.Teleport = new TeleportConfig
                {
                    DonatorWarmupSeconds = TeleportConfig.DefaultDonatorWarmupSeconds
                };
                return true;
            }

            if (config.Teleport.DonatorWarmupSeconds.HasValue)
            {
                return false;
            }

            config.Teleport.DonatorWarmupSeconds = TeleportConfig.DefaultDonatorWarmupSeconds;
            return true;
        }
    }
}
