using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class TpaToggleService
    {
        private readonly ICoreServerAPI api;
        private readonly IPlayerMessenger messenger;
        private readonly IPlayerLookup playerLookup;
        private readonly TpaPreferenceStore preferenceStore;
        private readonly TpaRequestStore requestStore;
        private readonly TpaRequestMessageFormatter messageFormatter;

        public TpaToggleService(
            ICoreServerAPI api,
            IPlayerMessenger messenger,
            IPlayerLookup playerLookup,
            TpaPreferenceStore preferenceStore,
            TpaRequestStore requestStore,
            TpaRequestMessageFormatter messageFormatter)
        {
            this.api = api;
            this.messenger = messenger;
            this.playerLookup = playerLookup;
            this.preferenceStore = preferenceStore;
            this.requestStore = requestStore;
            this.messageFormatter = messageFormatter;
        }

        public void Toggle(IServerPlayer player)
        {
            bool newState = !preferenceStore.IsDisabled(player);
            preferenceStore.SetDisabled(player, newState);

            if (newState)
            {
                foreach (TpaRequestRecord request in requestStore.Clear(player.PlayerUID))
                {
                    api.Event.UnregisterCallback(request.ExpireListenerId);

                    IServerPlayer requester = playerLookup.FindOnlinePlayerByUid(request.RequesterUid);
                    if (requester != null)
                    {
                        messenger.SendDual(
                            requester,
                            messageFormatter.FormatAutoDeniedForRequester(request.Direction, request.TargetName),
                            (int)EnumChatType.CommandSuccess,
                            (int)EnumChatType.Notification);
                    }
                }

                messenger.SendDual(player, "TPA requests disabled.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return;
            }

            messenger.SendDual(player, "TPA requests enabled.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
        }
    }
}
