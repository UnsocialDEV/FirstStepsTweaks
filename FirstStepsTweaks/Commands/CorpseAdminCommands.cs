using System.Text;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public static class CorpseAdminCommands
    {
        public static void Register(ICoreServerAPI api, CorpseService corpseService)
        {
            api.ChatCommands.Create("graveadmin")
                .WithDescription("Admin tools for corpse graves")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("list")
                    .WithDescription("List active graves")
                    .HandleWith(args => ListActiveGraves(args, corpseService))
                .EndSubCommand()
                .BeginSubCommand("remove")
                    .WithDescription("Remove a grave by ID")
                    .WithArgs(api.ChatCommands.Parsers.Int("graveId"))
                    .HandleWith(args => RemoveGrave(args, corpseService))
                .EndSubCommand()
                .BeginSubCommand("duplicate")
                    .WithDescription("Duplicate grave items by ID to a player")
                    .WithArgs(
                        api.ChatCommands.Parsers.Int("graveId"),
                        api.ChatCommands.Parsers.Word("player")
                    )
                    .HandleWith(args => DuplicateGrave(args, api, corpseService))
                .EndSubCommand()
                .BeginSubCommand("give")
                    .WithDescription("Give grave items by ID to a player and remove the grave")
                    .WithArgs(
                        api.ChatCommands.Parsers.Int("graveId"),
                        api.ChatCommands.Parsers.Word("player")
                    )
                    .HandleWith(args => GiveGrave(args, api, corpseService))
                .EndSubCommand();
        }

        private static TextCommandResult ListActiveGraves(TextCommandCallingArgs args, CorpseService corpseService)
        {
            IServerPlayer caller = (IServerPlayer)args.Caller.Player;
            var graves = corpseService.GetActiveGraves();

            if (graves.Count == 0)
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "No active graves.", EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Active graves: {graves.Count}");

            int max = graves.Count > 20 ? 20 : graves.Count;
            for (int i = 0; i < max; i++)
            {
                var grave = graves[i];
                BlockPos pos = grave.Position;
                string owner = string.IsNullOrEmpty(grave.OwnerName) ? grave.OwnerUid : grave.OwnerName;
                sb.AppendLine($"- ID {grave.GraveId}: {owner} @ {pos.X} {pos.Y} {pos.Z}");
            }

            if (graves.Count > max)
            {
                sb.AppendLine($"... and {graves.Count - max} more");
            }

            caller.SendMessage(GlobalConstants.InfoLogChatGroup, sb.ToString().TrimEnd(), EnumChatType.Notification);
            return TextCommandResult.Success();
        }

        private static TextCommandResult RemoveGrave(TextCommandCallingArgs args, CorpseService corpseService)
        {
            IServerPlayer caller = (IServerPlayer)args.Caller.Player;
            int graveId = (int)args[0];

            if (!corpseService.TryRemoveGraveById(graveId))
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "No grave found for that ID.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            caller.SendMessage(GlobalConstants.InfoLogChatGroup, $"Removed grave ID {graveId}.", EnumChatType.CommandSuccess);
            return TextCommandResult.Success();
        }

        private static TextCommandResult DuplicateGrave(TextCommandCallingArgs args, ICoreServerAPI api, CorpseService corpseService)
        {
            IServerPlayer caller = (IServerPlayer)args.Caller.Player;
            int graveId = (int)args[0];
            string playerName = (string)args[1];

            IServerPlayer target = GetPlayerByName(api, playerName);
            if (target == null)
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "Target player is not online.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            if (!corpseService.TryDuplicateGraveItemsById(graveId, target))
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "No grave inventory found for that ID.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            caller.SendMessage(GlobalConstants.InfoLogChatGroup, $"Duplicated grave ID {graveId} items to {target.PlayerName}.", EnumChatType.CommandSuccess);
            return TextCommandResult.Success();
        }

        private static TextCommandResult GiveGrave(TextCommandCallingArgs args, ICoreServerAPI api, CorpseService corpseService)
        {
            IServerPlayer caller = (IServerPlayer)args.Caller.Player;
            int graveId = (int)args[0];
            string playerName = (string)args[1];

            IServerPlayer target = GetPlayerByName(api, playerName);
            if (target == null)
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "Target player is not online.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            if (!corpseService.TryGiveGraveItemsById(graveId, target))
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "No grave inventory found for that ID.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            caller.SendMessage(GlobalConstants.InfoLogChatGroup, $"Gave grave ID {graveId} items to {target.PlayerName} and removed grave.", EnumChatType.CommandSuccess);
            return TextCommandResult.Success();
        }

        private static IServerPlayer GetPlayerByName(ICoreServerAPI api, string name)
        {
            foreach (IServerPlayer player in api.World.AllOnlinePlayers)
            {
                if (player.PlayerName.ToLowerInvariant() == name.ToLowerInvariant())
                {
                    return player;
                }
            }

            return null;
        }
    }
}
