using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class TpaRequestAccepter
    {
        private readonly ICoreServerAPI api;
        private readonly IPlayerMessenger messenger;
        private readonly IPlayerLookup playerLookup;
        private readonly TpaRequestStore requestStore;
        private readonly TpaTeleportService teleportService;
        private readonly TpaRequestMessageFormatter messageFormatter;

        public TpaRequestAccepter(
            ICoreServerAPI api,
            IPlayerMessenger messenger,
            IPlayerLookup playerLookup,
            TpaRequestStore requestStore,
            TpaTeleportService teleportService,
            TpaRequestMessageFormatter messageFormatter)
        {
            this.api = api;
            this.messenger = messenger;
            this.playerLookup = playerLookup;
            this.requestStore = requestStore;
            this.teleportService = teleportService;
            this.messageFormatter = messageFormatter;
        }

        public void Accept(IServerPlayer target)
        {
            if (!requestStore.TryTakeFirst(target.PlayerUID, out TpaRequestRecord request))
            {
                messenger.SendDual(target, "No pending teleport requests.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return;
            }

            api.Event.UnregisterCallback(request.ExpireListenerId);

            IServerPlayer requester = playerLookup.FindOnlinePlayerByUid(request.RequesterUid);
            if (requester == null)
            {
                messenger.SendDual(target, $"{request.RequesterName} is no longer online.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return;
            }

            messenger.SendDual(
                requester,
                messageFormatter.FormatAcceptedForRequester(request.Direction, request.TargetName),
                (int)EnumChatType.CommandSuccess,
                (int)EnumChatType.Notification);
            messenger.SendDual(
                target,
                messageFormatter.FormatAcceptedForTarget(request.Direction, request.RequesterName),
                (int)EnumChatType.CommandSuccess,
                (int)EnumChatType.Notification);

            teleportService.BeginTeleport(request, requester, target);
        }
    }
}
