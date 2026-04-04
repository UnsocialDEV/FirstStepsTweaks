using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public interface IAdminModeVitalsService
    {
        void CaptureAndFill(IServerPlayer player, AdminModeState state);

        void EnsureFull(IServerPlayer player);

        void RestoreOrFull(IServerPlayer player, AdminModeState state);
    }
}
