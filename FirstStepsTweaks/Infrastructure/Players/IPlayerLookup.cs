using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Players
{
    public interface IPlayerLookup
    {
        IServerPlayer FindOnlinePlayerByUid(string uid);
        IServerPlayer FindOnlinePlayerByName(string name);
    }
}
