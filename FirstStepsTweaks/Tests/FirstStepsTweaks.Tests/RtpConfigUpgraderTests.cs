using FirstStepsTweaks.Config;
using Xunit;

namespace FirstStepsTweaks.Tests
{
    public sealed class RtpConfigUpgraderTests
    {
        [Fact]
        public void TryUpgradeLegacyDefaults_UpdatesLegacyOriginSettings()
        {
            var config = new FirstStepsTweaksConfig
            {
                Rtp = new RtpConfig
                {
                    MinRadius = RtpConfig.LegacyMinRadius,
                    MaxRadius = RtpConfig.LegacyMaxRadius,
                    UsePlayerPositionAsCenter = RtpConfig.LegacyUsePlayerPositionAsCenter
                }
            };
            var upgrader = new RtpConfigUpgrader();

            bool changed = upgrader.TryUpgradeLegacyDefaults(config);

            Assert.True(changed);
            Assert.Equal(RtpConfig.DefaultMinRadius, config.Rtp.MinRadius);
            Assert.Equal(RtpConfig.DefaultMaxRadius, config.Rtp.MaxRadius);
            Assert.Equal(RtpConfig.DefaultUsePlayerPositionAsCenter, config.Rtp.UsePlayerPositionAsCenter);
        }

        [Fact]
        public void TryUpgradeLegacyDefaults_PreservesCustomizedSettings()
        {
            var config = new FirstStepsTweaksConfig
            {
                Rtp = new RtpConfig
                {
                    MinRadius = 3000,
                    MaxRadius = 4200,
                    UsePlayerPositionAsCenter = true
                }
            };
            var upgrader = new RtpConfigUpgrader();

            bool changed = upgrader.TryUpgradeLegacyDefaults(config);

            Assert.False(changed);
            Assert.Equal(3000, config.Rtp.MinRadius);
            Assert.Equal(4200, config.Rtp.MaxRadius);
            Assert.True(config.Rtp.UsePlayerPositionAsCenter);
        }
    }
}
