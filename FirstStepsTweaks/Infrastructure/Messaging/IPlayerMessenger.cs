using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Messaging
{
    public interface IPlayerMessenger
    {
        void SendInfo(IServerPlayer player, string message, int groupId, int chatType);
        void SendGeneral(IServerPlayer player, string message, int groupId, int chatType);
        void SendDual(IServerPlayer player, string message, int infoChatType, int generalChatType);
        void SendDual(IServerPlayer player, string message, int infoGroupId, int infoChatType, int generalGroupId, int generalChatType);
        void SendIngameError(IServerPlayer player, string code, string message);
    }
}
