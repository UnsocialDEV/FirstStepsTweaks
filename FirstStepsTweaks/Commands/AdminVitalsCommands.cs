using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class AdminVitalsCommands
    {
        private const float DefaultMaxHealth = 20f;
        private const float DefaultMaxSaturation = 1500f;
        private readonly ICoreServerAPI api;

        public AdminVitalsCommands(ICoreServerAPI api)
        {
            this.api = api;
        }

        public void Register()
        {
            api.ChatCommands
                .Create("heal")
                .WithDescription("Fully heal yourself or another online player")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("player"))
                .HandleWith(Heal);

            api.ChatCommands
                .Create("feed")
                .WithDescription("Fully restore satiety for yourself or another online player")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("player"))
                .HandleWith(Feed);
        }

        private TextCommandResult Heal(TextCommandCallingArgs args)
        {
            IServerPlayer caller = (IServerPlayer)args.Caller.Player;
            IServerPlayer target = ResolveTargetPlayer(caller, args[0] as string);
            if (target == null)
            {
                return TextCommandResult.Success();
            }

            ITreeAttribute healthTree = target.Entity?.WatchedAttributes?.GetTreeAttribute("health");
            if (healthTree == null)
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "Target does not have a health state to modify.", EnumChatType.CommandSuccess);
                caller.SendMessage(GlobalConstants.GeneralChatGroup, "Target does not have a health state to modify.", EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            float maxHealth = healthTree.TryGetFloat("maxhealth")
                ?? healthTree.TryGetFloat("basemaxhealth")
                ?? DefaultMaxHealth;

            if (maxHealth <= 0)
            {
                maxHealth = DefaultMaxHealth;
            }

            healthTree.SetFloat("currenthealth", maxHealth);
            target.Entity.WatchedAttributes.MarkPathDirty("health");

            if (target.PlayerUID == caller.PlayerUID)
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "You are fully healed.", EnumChatType.CommandSuccess);
                caller.SendMessage(GlobalConstants.GeneralChatGroup, "You are fully healed.", EnumChatType.Notification);
            }
            else
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, $"Healed {target.PlayerName}.", EnumChatType.CommandSuccess);
                caller.SendMessage(GlobalConstants.GeneralChatGroup, $"Healed {target.PlayerName}.", EnumChatType.Notification);
                target.SendMessage(GlobalConstants.InfoLogChatGroup, $"{caller.PlayerName} healed you.", EnumChatType.CommandSuccess);
                target.SendMessage(GlobalConstants.GeneralChatGroup, $"{caller.PlayerName} healed you.", EnumChatType.Notification);
            }

            return TextCommandResult.Success();
        }

        private TextCommandResult Feed(TextCommandCallingArgs args)
        {
            IServerPlayer caller = (IServerPlayer)args.Caller.Player;
            IServerPlayer target = ResolveTargetPlayer(caller, args[0] as string);
            if (target == null)
            {
                return TextCommandResult.Success();
            }

            ITreeAttribute hungerTree = target.Entity?.WatchedAttributes?.GetTreeAttribute("hunger");
            if (hungerTree == null)
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "Target does not have a hunger state to modify.", EnumChatType.CommandSuccess);
                caller.SendMessage(GlobalConstants.GeneralChatGroup, "Target does not have a hunger state to modify.", EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            float maxSaturation = hungerTree.TryGetFloat("maxsaturation") ?? DefaultMaxSaturation;
            if (maxSaturation <= 0)
            {
                maxSaturation = DefaultMaxSaturation;
            }

            hungerTree.SetFloat("currentsaturation", maxSaturation);
            target.Entity.WatchedAttributes.MarkPathDirty("hunger");

            if (target.PlayerUID == caller.PlayerUID)
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "Your satiety is fully restored.", EnumChatType.CommandSuccess);
                caller.SendMessage(GlobalConstants.GeneralChatGroup, "Your satiety is fully restored.", EnumChatType.Notification);
            }
            else
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, $"Fed {target.PlayerName}.", EnumChatType.CommandSuccess);
                caller.SendMessage(GlobalConstants.GeneralChatGroup, $"Fed {target.PlayerName}.", EnumChatType.Notification);
                target.SendMessage(GlobalConstants.InfoLogChatGroup, $"{caller.PlayerName} restored your satiety.", EnumChatType.CommandSuccess);
                target.SendMessage(GlobalConstants.GeneralChatGroup, $"{caller.PlayerName} restored your satiety.", EnumChatType.Notification);
            }

            return TextCommandResult.Success();
        }

        private IServerPlayer ResolveTargetPlayer(IServerPlayer caller, string targetName)
        {
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return caller;
            }

            foreach (IServerPlayer player in api.World.AllOnlinePlayers)
            {
                if (player.PlayerName.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return player;
                }
            }

            caller.SendMessage(GlobalConstants.InfoLogChatGroup, "Target player is not online.", EnumChatType.CommandSuccess);
            caller.SendMessage(GlobalConstants.GeneralChatGroup, "Target player is not online.", EnumChatType.Notification);
            return null;
        }
    }
}
