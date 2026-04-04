using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    internal static class TeleportBypass
    {
        public const string Privilege = "firststepstweaks.bypassteleportcooldown";

        public static bool HasBypass(IServerPlayer player)
        {
            return player?.HasPrivilege(Privilege) == true;
        }

        public static void NotifyBypassingCooldown(IServerPlayer player, string context)
        {
            if (player == null)
            {
                return;
            }

            string message = $"You bypassed the teleport cooldown.";

            player.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                message,
                EnumChatType.CommandSuccess
            );

            player.SendMessage(
                GlobalConstants.GeneralChatGroup,
                message,
                EnumChatType.Notification
            );
        }

        public static void NotifyBypassingRtpCooldown(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            const string message = "You bypassed the /rtp cooldown.";

            player.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                message,
                EnumChatType.CommandSuccess
            );

            player.SendMessage(
                GlobalConstants.GeneralChatGroup,
                message,
                EnumChatType.Notification
            );
        }
    }
}
