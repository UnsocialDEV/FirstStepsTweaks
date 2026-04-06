using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class StaffCommandTarget
    {
        public string PlayerUid { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public IServerPlayer OnlinePlayer { get; set; }
    }
}
