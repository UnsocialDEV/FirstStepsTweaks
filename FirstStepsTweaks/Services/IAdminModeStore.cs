using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public interface IAdminModeStore : IAdminModeStatusReader
    {
        bool TryLoad(IServerPlayer player, out AdminModeState state, out string errorMessage);

        void Save(IServerPlayer player, AdminModeState state);

        void Clear(IServerPlayer player);
    }
}
