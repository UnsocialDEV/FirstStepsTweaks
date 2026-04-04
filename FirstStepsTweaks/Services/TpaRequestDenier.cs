using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class TpaRequestDenier
    {
        private readonly ICoreServerAPI api;
        private readonly IPlayerMessenger messenger;
        private readonly IPlayerLookup playerLookup;
        private readonly TpaRequestStore requestStore;
        private readonly TpaRequestMessageFormatter messageFormatter;

        public TpaRequestDenier(
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

        public void Deny(IServerPlayer target)
        {
            if (!requestStore.TryTakeFirst(target.PlayerUID, out TpaRequestRecord request))
            {
                messenger.SendDual(target, "No pending teleport requests.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return;
            }

            api.Event.UnregisterCallback(request.ExpireListenerId);

            messenger.SendDual(
                target,
                messageFormatter.FormatDeniedForTarget(request.Direction, request.RequesterName),
                (int)EnumChatType.CommandSuccess,
                (int)EnumChatType.Notification);

            IServerPlayer requester = playerLookup.FindOnlinePlayerByUid(request.RequesterUid);
            if (requester != null)
            {
                messenger.SendDual(
                    requester,
                    messageFormatter.FormatDeniedForRequester(request.Direction, request.TargetName),
                    (int)EnumChatType.CommandSuccess,
                    (int)EnumChatType.Notification);
            }
        }
    }
}
