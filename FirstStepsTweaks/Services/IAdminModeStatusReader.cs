using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public interface IAdminModeStatusReader
    {
        bool IsActive(IServerPlayer player);
    }
}
