using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public interface IAdminModePlayerStateController
    {
        AdminModeState Capture(IServerPlayer player);

        void Enable(IServerPlayer player, AdminModeState state);

        void Reapply(IServerPlayer player, AdminModeState state);

        void Restore(IServerPlayer player, AdminModeState state);
    }
}
