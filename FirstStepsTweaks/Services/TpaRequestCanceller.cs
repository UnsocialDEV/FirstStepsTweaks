using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class TpaRequestCanceller
    {
        private readonly ICoreServerAPI api;
        private readonly IPlayerMessenger messenger;
        private readonly IPlayerLookup playerLookup;
        private readonly TpaRequestStore requestStore;
        private readonly TpaRequestMessageFormatter messageFormatter;

        public TpaRequestCanceller(
            ICoreServerAPI api,
            IPlayerMessenger messenger,
            IPlayerLookup playerLookup,
            TpaRequestStore requestStore,
            TpaRequestMessageFormatter messageFormatter)
        {
            this.api = api;
            this.messenger = messenger;
            this.playerLookup = playerLookup;
            this.requestStore = requestStore;
            this.messageFormatter = messageFormatter;
        }

        public void Cancel(IServerPlayer requester)
        {
            if (!requestStore.TryCancelByRequester(requester.PlayerUID, out TpaRequestRecord request))
            {
                messenger.SendDual(requester, "You have no pending requests.", (int)EnumChatType.Notification, (int)EnumChatType.Notification);
                return;
            }

            api.Event.UnregisterCallback(request.ExpireListenerId);

            messenger.SendDual(
                requester,
                messageFormatter.FormatCancelledForRequester(request.Direction, request.TargetName),
                (int)EnumChatType.CommandSuccess,
                (int)EnumChatType.Notification);

            IServerPlayer target = playerLookup.FindOnlinePlayerByUid(request.TargetUid);
            if (target != null)
            {
                messenger.SendDual(
                    target,
                    messageFormatter.FormatCancelledForTarget(request.Direction, request.RequesterName),
                    (int)EnumChatType.CommandSuccess,
                    (int)EnumChatType.Notification);
            }
        }
    }
}
