namespace FirstStepsTweaks.Services
{
    public sealed class HomeNameNormalizer
    {
        public string Normalize(string homeName)
        {
            return (homeName ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
