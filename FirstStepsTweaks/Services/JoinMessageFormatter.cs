namespace FirstStepsTweaks.Services
{
    public sealed class JoinMessageFormatter
    {
        public string FormatFirstJoin(string template, string playerName)
        {
            return (template ?? string.Empty).Replace("{player}", playerName ?? string.Empty);
        }

        public string FormatReturningJoin(string template, string playerName, int daysSinceLastSeen)
        {
            return (template ?? string.Empty)
                .Replace("{player}", playerName ?? string.Empty)
                .Replace("{days}", daysSinceLastSeen.ToString());
        }
    }
}
