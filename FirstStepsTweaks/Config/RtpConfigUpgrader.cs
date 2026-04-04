namespace FirstStepsTweaks.Config
{
    public sealed class RtpConfigUpgrader
    {
        public bool TryUpgradeLegacyDefaults(FirstStepsTweaksConfig config)
        {
            if (config == null)
            {
                return false;
            }

            if (config.Rtp == null)
            {
                config.Rtp = new RtpConfig();
                return true;
            }

            if (config.Rtp.MinRadius != RtpConfig.LegacyMinRadius
                || config.Rtp.MaxRadius != RtpConfig.LegacyMaxRadius
                || config.Rtp.UsePlayerPositionAsCenter != RtpConfig.LegacyUsePlayerPositionAsCenter)
            {
                return false;
            }

            config.Rtp.MinRadius = RtpConfig.DefaultMinRadius;
            config.Rtp.MaxRadius = RtpConfig.DefaultMaxRadius;
            config.Rtp.UsePlayerPositionAsCenter = RtpConfig.DefaultUsePlayerPositionAsCenter;
            return true;
        }
    }
}
