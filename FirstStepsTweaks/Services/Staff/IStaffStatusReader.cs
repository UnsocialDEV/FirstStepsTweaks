using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public interface IStaffStatusReader
    {
        StaffLevel GetLevel(IServerPlayer player);

        StaffLevel GetLevel(string playerUid, string playerName = null);

        bool IsModerator(IServerPlayer player);

        bool IsModerator(string playerUid, string playerName = null);

        bool IsAdmin(IServerPlayer player);

        bool IsAdmin(string playerUid, string playerName = null);
    }
}
