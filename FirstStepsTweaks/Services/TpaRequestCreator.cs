using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class TpaRequestCreator
    {
        private readonly ICoreServerAPI api;
        private readonly TeleportConfig teleportConfig;
        private readonly IPlayerMessenger messenger;
        private readonly IPlayerLookup playerLookup;
        private readonly TpaPreferenceStore preferenceStore;
        private readonly TpaRequestStore requestStore;
        private readonly TpaRequestMessageFormatter messageFormatter;

        public TpaRequestCreator(
            ICoreServerAPI api,
            FirstStepsTweaksConfig config,
            IPlayerMessenger messenger,
            IPlayerLookup playerLookup,
            TpaPreferenceStore preferenceStore,
            TpaRequestStore requestStore,
            TpaRequestMessageFormatter messageFormatter)
        {
            this.api = api;
            teleportConfig = config?.Teleport ?? new TeleportConfig();
            this.messenger = messenger;
            this.playerLookup = playerLookup;
            this.preferenceStore = preferenceStore;
            this.requestStore = requestStore;
            this.messageFormatter = messageFormatter;
        }

        public void Create(IServerPlayer caller, string targetName, TpaRequestDirection direction)
        {
            IServerPlayer target = playerLookup.FindOnlinePlayerByName(targetName);

            if (target == null)
            {
                messenger.SendDual(caller, "Player not found.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return;
            }

            if (target.PlayerUID == caller.PlayerUID)
            {
                messenger.SendDual(caller, "You cannot teleport to yourself.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return;
            }

            if (preferenceStore.IsDisabled(target))
            {
                messenger.SendDual(caller, $"{target.PlayerName} is not accepting teleport requests.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return;
            }

            var request = new TpaRequestRecord
            {
                RequesterUid = caller.PlayerUID,
                RequesterName = caller.PlayerName,
                TargetUid = target.PlayerUID,
                TargetName = target.PlayerName,
                Direction = direction
            };

            requestStore.Add(request);
            request.ExpireListenerId = api.Event.RegisterCallback(_ => Expire(request), teleportConfig.TpaExpireMs);

            messenger.SendDual(caller, messageFormatter.FormatSent(direction, target.PlayerName), (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
            messenger.SendDual(target, messageFormatter.FormatReceived(direction, caller.PlayerName), (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
        }

        private void Expire(TpaRequestRecord request)
        {
            requestStore.Remove(request.TargetUid, request);

            IServerPlayer requester = playerLookup.FindOnlinePlayerByUid(request.RequesterUid);
            if (requester != null)
            {
                messenger.SendDual(
                    requester,
                    messageFormatter.FormatExpiredForRequester(request.Direction, request.TargetName),
                    (int)EnumChatType.CommandSuccess,
                    (int)EnumChatType.Notification);
            }

            IServerPlayer target = playerLookup.FindOnlinePlayerByUid(request.TargetUid);
            if (target != null)
            {
                messenger.SendDual(
                    target,
                    messageFormatter.FormatExpiredForTarget(request.Direction, request.RequesterName),
                    (int)EnumChatType.CommandSuccess,
                    (int)EnumChatType.Notification);
            }
        }
    }
}
