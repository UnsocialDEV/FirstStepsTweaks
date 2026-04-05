using System;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Players
{
    public interface IDelayedPlayerActionScheduler
    {
        void Schedule(string playerUid, int delayMs, Action<IServerPlayer> action);
    }
}
