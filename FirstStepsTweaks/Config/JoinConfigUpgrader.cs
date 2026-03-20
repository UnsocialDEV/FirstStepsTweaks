namespace FirstStepsTweaks.Config
{
    public sealed class JoinConfigUpgrader
    {
        public bool TryUpgradeReturningJoinMessage(FirstStepsTweaksConfig config)
        {
            if (config == null)
            {
                return false;
            }

            if (config.Join == null)
            {
                config.Join = new JoinConfig();
                return false;
            }

            if (config.Join.ReturningJoinMessage != JoinConfig.LegacyReturningJoinMessage)
            {
                return false;
            }

            config.Join.ReturningJoinMessage = JoinConfig.DefaultReturningJoinMessage;
            return true;
        }
    }
}
