using FirstStepsTweaks.Services;
using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public static class GravestoneCommands
    {
        private const string AdminPrivilege = "firststepstweaks.graveadmin";
        private const string CurrentLocationSelector = "currentloc";

        private static GravestoneService gravestoneService;

        public static void Register(ICoreServerAPI api, GravestoneService service)
        {
            gravestoneService = service;

            api.ChatCommands
                .Create("graveadmin")
                .WithDescription("Admin tools for gravestone management")
                .RequiresPlayer()
                .RequiresPrivilege(AdminPrivilege)
                .BeginSubCommand("list")
                    .WithDescription("List active gravestones")
                    .HandleWith(args => List(api, args))
                .EndSubCommand()
                .BeginSubCommand("giveblock")
                    .WithDescription("Give gravestone block item(s) to a player")
                    .WithArgs(
                        api.ChatCommands.Parsers.Word("player"),
                        api.ChatCommands.Parsers.OptionalWord("quantity")
                    )
                    .HandleWith(args => GiveBlock(api, args))
                .EndSubCommand()
                .BeginSubCommand("dupeitems")
                    .WithDescription("Duplicate stored gravestone items to a player without removing the gravestone. Use currentloc <player> while looking at a gravestone, or <graveId> <player>.")
                    .WithArgs(
                        api.ChatCommands.Parsers.Word("graveId"),
                        api.ChatCommands.Parsers.Word("player")
                    )
                    .HandleWith(args => DuplicateItems(api, args))
                .EndSubCommand()
                .BeginSubCommand("restore")
                    .WithDescription("Restore gravestone items to a player and remove the gravestone. Use currentloc <player> while looking at a gravestone, or <graveId> <player>.")
                    .WithArgs(
                        api.ChatCommands.Parsers.Word("graveId"),
                        api.ChatCommands.Parsers.Word("player")
                    )
                    .HandleWith(args => Restore(api, args))
                .EndSubCommand()
                .BeginSubCommand("remove")
                    .WithDescription("Remove a gravestone without restoring items. Use currentloc while looking at a gravestone, or provide <graveId>.")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("graveId"))
                    .HandleWith(args => Remove(api, args))
                .EndSubCommand()
                .BeginSubCommand("teleport")
                    .WithDescription("Teleport directly to a gravestone. Use currentloc while looking at a gravestone, or provide <graveId>.")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("graveId"))
                    .HandleWith(args => Teleport(api, args))
                .EndSubCommand();
        }

        private static TextCommandResult List(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer caller = args.Caller.Player as IServerPlayer;
            if (caller == null || gravestoneService == null)
            {
                return TextCommandResult.Success();
            }

            var graves = gravestoneService.GetActiveGraves();
            if (graves.Count == 0)
            {
                SendBoth(caller, "No active gravestones found.");
                return TextCommandResult.Success();
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Active gravestones ({graves.Count}):");

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (GraveData grave in graves.OrderBy(grave => grave.CreatedUnixMs))
            {
                if (grave == null)
                {
                    continue;
                }

                long ageMinutes = Math.Max(0, (now - grave.CreatedUnixMs) / 60000L);
                string claimState = gravestoneService.IsPubliclyClaimable(grave) ? "public" : "owner-only";

                sb.AppendLine($"- {grave.GraveId} | owner={grave.OwnerName} | pos={grave.Dimension}:{grave.X},{grave.Y},{grave.Z} | age={ageMinutes}m | {claimState}");
            }

            caller.SendMessage(GlobalConstants.InfoLogChatGroup, sb.ToString().TrimEnd(), EnumChatType.Notification);
            return TextCommandResult.Success();
        }

        private static TextCommandResult GiveBlock(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer caller = args.Caller.Player as IServerPlayer;
            string targetName = args[0] as string;
            string quantityRaw = args[1] as string;

            IServerPlayer target = ResolveOnlinePlayer(api, targetName);
            if (target == null)
            {
                SendBoth(caller, "Target player is not online.");
                return TextCommandResult.Success();
            }

            int quantity = 1;
            if (!string.IsNullOrWhiteSpace(quantityRaw) && (!int.TryParse(quantityRaw, out quantity) || quantity <= 0))
            {
                SendBoth(caller, "Quantity must be a positive whole number.");
                return TextCommandResult.Success();
            }

            ItemService.GiveCollectible(api, target, gravestoneService.GraveBlockCode, quantity);
            SendBoth(caller, $"Gave {quantity} gravestone block item(s) to {target.PlayerName}.");

            if (!string.Equals(caller.PlayerUID, target.PlayerUID, StringComparison.OrdinalIgnoreCase))
            {
                SendBoth(target, $"{caller.PlayerName} gave you {quantity} gravestone block item(s).");
            }

            return TextCommandResult.Success();
        }

        private static TextCommandResult DuplicateItems(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer caller = args.Caller.Player as IServerPlayer;
            string graveSelector = args[0] as string;
            string targetName = args[1] as string;

            if (!TryResolveGraveSelector(caller, graveSelector, out string graveId, out string resolveMessage))
            {
                SendBoth(caller, resolveMessage);
                return TextCommandResult.Success();
            }

            IServerPlayer target = ResolveOnlinePlayer(api, targetName);
            if (target == null)
            {
                SendBoth(caller, "Target player is not online.");
                return TextCommandResult.Success();
            }

            bool success = gravestoneService.TryDuplicateGraveItemsToPlayer(graveId, target, out string message);
            SendBoth(caller, message);

            if (success && !string.Equals(caller.PlayerUID, target.PlayerUID, StringComparison.OrdinalIgnoreCase))
            {
                SendBoth(target, $"{caller.PlayerName} duplicated gravestone items to your inventory.");
            }

            return TextCommandResult.Success();
        }

        private static TextCommandResult Restore(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer caller = args.Caller.Player as IServerPlayer;
            string graveSelector = args[0] as string;
            string targetName = args[1] as string;

            if (!TryResolveGraveSelector(caller, graveSelector, out string graveId, out string resolveMessage))
            {
                SendBoth(caller, resolveMessage);
                return TextCommandResult.Success();
            }

            IServerPlayer target = ResolveOnlinePlayer(api, targetName);
            if (target == null)
            {
                SendBoth(caller, "Target player is not online.");
                return TextCommandResult.Success();
            }

            bool success = gravestoneService.TryAdminRestoreGraveToPlayer(graveId, target, out string message);
            SendBoth(caller, message);

            if (success && !string.Equals(caller.PlayerUID, target.PlayerUID, StringComparison.OrdinalIgnoreCase))
            {
                SendBoth(target, $"{caller.PlayerName} restored gravestone items to you.");
            }

            return TextCommandResult.Success();
        }

        private static TextCommandResult Remove(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer caller = args.Caller.Player as IServerPlayer;
            string graveSelector = args[0] as string;

            if (string.IsNullOrWhiteSpace(graveSelector))
            {
                SendBoth(caller, "Usage: /graveadmin remove currentloc while looking at a gravestone, or /graveadmin remove <graveId>.");
                return TextCommandResult.Success();
            }

            if (!TryResolveGraveSelector(caller, graveSelector, out string graveId, out string resolveMessage))
            {
                SendBoth(caller, resolveMessage);
                return TextCommandResult.Success();
            }

            bool success = gravestoneService.TryRemoveGrave(graveId, out string message);
            SendBoth(caller, message);

            return TextCommandResult.Success();
        }

        private static TextCommandResult Teleport(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer caller = args.Caller.Player as IServerPlayer;
            string graveSelector = args[0] as string;

            if (string.IsNullOrWhiteSpace(graveSelector))
            {
                SendBoth(caller, "Usage: /graveadmin teleport currentloc while looking at a gravestone, or /graveadmin teleport <graveId>.");
                return TextCommandResult.Success();
            }

            if (!TryResolveGraveSelector(caller, graveSelector, out string graveId, out string resolveMessage))
            {
                SendBoth(caller, resolveMessage);
                return TextCommandResult.Success();
            }

            if (!gravestoneService.TryGetTeleportTarget(graveId, out GraveData grave, out var target, out string message))
            {
                SendBoth(caller, message);
                return TextCommandResult.Success();
            }

            if (!(caller?.Entity is EntityPlayer entityPlayer))
            {
                SendBoth(caller, "Teleport is only available to in-game players.");
                return TextCommandResult.Success();
            }

            BackCommands.RecordCurrentLocation(caller);

            if (entityPlayer.Pos != null && entityPlayer.Pos.Dimension != grave.Dimension)
            {
                entityPlayer.ChangeDimension(grave.Dimension);
            }

            entityPlayer.TeleportToDouble(
                target.X,
                target.Y,
                target.Z,
                () => SendBoth(caller, $"Teleported to gravestone '{grave.GraveId}' at {grave.Dimension}:{grave.X},{grave.Y},{grave.Z}.")
            );

            return TextCommandResult.Success();
        }

        private static bool TryResolveGraveSelector(IServerPlayer caller, string selector, out string graveId, out string message)
        {
            graveId = string.Empty;
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(selector))
            {
                message = "Specify a grave ID or use currentloc while looking at a gravestone.";
                return false;
            }

            if (selector.Equals(CurrentLocationSelector, StringComparison.OrdinalIgnoreCase))
            {
                return gravestoneService.TryResolveTargetedGraveId(caller, out graveId, out message);
            }

            graveId = selector;
            return true;
        }

        private static IServerPlayer ResolveOnlinePlayer(ICoreServerAPI api, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            foreach (IServerPlayer player in api.World.AllOnlinePlayers)
            {
                if (player.PlayerName.Equals(query, StringComparison.OrdinalIgnoreCase)
                    || player.PlayerUID.Equals(query, StringComparison.OrdinalIgnoreCase))
                {
                    return player;
                }
            }

            return null;
        }

        private static void SendBoth(IServerPlayer player, string message)
        {
            if (player == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            player.SendMessage(GlobalConstants.InfoLogChatGroup, message, EnumChatType.CommandSuccess);
            player.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
        }
    }
}
