using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public interface IRtpDestinationResolver
    {
        RtpDestinationResolutionResult ResolveDestination(IServerPlayer player, RtpSearchSession searchSession = null);
    }
}
