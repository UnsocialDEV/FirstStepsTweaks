using FirstStepsTweaks.Config;
using FirstStepsTweaks.Services;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Features
{
    public sealed class ChatFeature : IFeatureModule
    {
        private readonly ICoreServerAPI api;
        private readonly FirstStepsTweaksConfig config;
        private readonly DonatorChatPrefixApplicator donatorChatPrefixApplicator;

        public ChatFeature(ICoreServerAPI api, FirstStepsTweaksConfig config)
        {
            this.api = api;
            this.config = config;
            donatorChatPrefixApplicator = new DonatorChatPrefixApplicator();
        }

        public void Register()
        {
            if (!config.Chat.EnableDonatorPrefixes)
            {
                return;
            }

            api.Event.PlayerChat += OnPlayerChat;
        }

        private void OnPlayerChat(IServerPlayer player, int channelId, ref string message, ref string data, BoolRef consumed)
        {
            if (consumed?.value == true)
            {
                return;
            }

            message = donatorChatPrefixApplicator.Apply(message, player.HasPrivilege, config.Chat);
        }
    }
}
