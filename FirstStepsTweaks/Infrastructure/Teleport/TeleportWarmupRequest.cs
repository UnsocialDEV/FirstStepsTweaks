using System;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Teleport
{
    public sealed class TeleportWarmupRequest
    {
        public IServerPlayer Player { get; set; }
        public string WarmupMessage { get; set; } = string.Empty;
        public string CountdownTemplate { get; set; } = "Teleporting in {0}...";
        public string CancelMessage { get; set; } = "Teleport cancelled because you moved.";
        public string SuccessIngameMessage { get; set; } = "Teleported.";
        public string BypassContext { get; set; } = string.Empty;
        public int WarmupSeconds { get; set; }
        public int TickIntervalMs { get; set; }
        public double CancelMoveThreshold { get; set; }
        public int WarmupInfoChatType { get; set; }
        public int WarmupGeneralGroupId { get; set; }
        public int WarmupGeneralChatType { get; set; }
        public int CancelInfoChatType { get; set; }
        public int CancelGeneralChatType { get; set; }
        public bool AllowBypass { get; set; } = true;
        public Action ExecuteTeleport { get; set; }
    }
}
