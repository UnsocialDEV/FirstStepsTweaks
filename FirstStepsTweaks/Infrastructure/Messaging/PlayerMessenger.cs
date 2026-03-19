using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Messaging
{
    public sealed class PlayerMessenger : IPlayerMessenger
    {
        public void SendInfo(IServerPlayer player, string message, int groupId, int chatType)
        {
            if (player == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            player.SendMessage(groupId, message, (EnumChatType)chatType);
        }

        public void SendGeneral(IServerPlayer player, string message, int groupId, int chatType)
        {
            if (player == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            player.SendMessage(groupId, message, (EnumChatType)chatType);
        }

        public void SendDual(IServerPlayer player, string message, int infoChatType, int generalChatType)
        {
            SendDual(
                player,
                message,
                GlobalConstants.InfoLogChatGroup,
                infoChatType,
                GlobalConstants.GeneralChatGroup,
                generalChatType
            );
        }

        public void SendDual(IServerPlayer player, string message, int infoGroupId, int infoChatType, int generalGroupId, int generalChatType)
        {
            if (player == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            player.SendMessage(infoGroupId, message, (EnumChatType)infoChatType);
            player.SendMessage(generalGroupId, message, (EnumChatType)generalChatType);
        }

        public void SendIngameError(IServerPlayer player, string code, string message)
        {
            if (player == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            player.SendIngameError(code, message);
        }
    }
}
