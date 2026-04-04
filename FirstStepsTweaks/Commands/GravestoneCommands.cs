using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Infrastructure.Teleport;
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
    public sealed class GravestoneCommands
    {
        private const string AdminPrivilege = "firststepstweaks.graveadmin";
        private const string CurrentLocationSelector = "currentloc";

        private readonly ICoreServerAPI api;
        private readonly GravestoneService gravestoneService;
        private readonly IPlayerMessenger messenger;
        private readonly IPlayerLookup playerLookup;
        private readonly IBackLocationStore backLocationStore;
        private readonly IWorldCoordinateReader coordinateReader;
        private readonly IWorldCoordinateDisplayFormatter coordinateDisplayFormatter;

        public GravestoneCommands(
            ICoreServerAPI api,
            GravestoneService gravestoneService,
            IPlayerMessenger messenger,
            IPlayerLookup playerLookup,
            IBackLocationStore backLocationStore,
            IWorldCoordinateReader coordinateReader,
            IWorldCoordinateDisplayFormatter coordinateDisplayFormatter)
        {
            this.api = api;
            this.gravestoneService = gravestoneService;
            this.messenger = messenger;
            this.playerLookup = playerLookup;
            this.backLocationStore = backLocationStore;
            this.coordinateReader = coordinateReader ?? new WorldCoordinateReader();
            this.coordinateDisplayFormatter = coordinateDisplayFormatter ?? new WorldCoordinateDisplayFormatter(api);
        }

        public void Register()
        {
            api.ChatCommands
                .Create("graveadmin")
                .WithDescription("Admin tools for gravestone management")
                .RequiresPlayer()
                .RequiresPrivilege(AdminPrivilege)
                .BeginSubCommand("list")
                    .WithDescription("List active gravestones")
                    .HandleWith(List)
                .EndSubCommand()
                .BeginSubCommand("giveblock")
                    .WithDescription("Give gravestone block item(s) to a player")
                    .WithArgs(
                        api.ChatCommands.Parsers.Word("player"),
                        api.ChatCommands.Parsers.OptionalWord("quantity")
                    )
                    .HandleWith(GiveBlock)
                .EndSubCommand()
                .BeginSubCommand("dupeitems")
                    .WithDescription("Duplicate stored gravestone items to a player without removing the gravestone. Use currentloc <player> while looking at a gravestone, or <graveId> <player>.")
                    .WithArgs(
                        api.ChatCommands.Parsers.Word("graveId"),
                        api.ChatCommands.Parsers.Word("player")
                    )
                    .HandleWith(DuplicateItems)
                .EndSubCommand()
                .BeginSubCommand("restore")
                    .WithDescription("Restore gravestone items to a player and remove the gravestone. Use currentloc <player> while looking at a gravestone, or <graveId> <player>.")
                    .WithArgs(
                        api.ChatCommands.Parsers.Word("graveId"),
                        api.ChatCommands.Parsers.Word("player")
                    )
                    .HandleWith(Restore)
                .EndSubCommand()
                .BeginSubCommand("remove")
                    .WithDescription("Remove a gravestone without restoring items. Use currentloc while looking at a gravestone, or provide <graveId>.")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("graveId"))
                    .HandleWith(Remove)
                .EndSubCommand()
                .BeginSubCommand("teleport")
                    .WithDescription("Teleport directly to a gravestone. Use currentloc while looking at a gravestone, or provide <graveId>.")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("graveId"))
                    .HandleWith(Teleport)
                .EndSubCommand();
        }

        private TextCommandResult List(TextCommandCallingArgs args)
        {
            IServerPlayer caller = args.Caller.Player as IServerPlayer;
            if (caller == null)
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
                string displayPosition = coordinateDisplayFormatter.FormatBlockPosition(grave.Dimension, grave.X, grave.Y, grave.Z);
                sb.AppendLine($"- {grave.GraveId} | owner={grave.OwnerName} | pos={displayPosition} | age={ageMinutes}m | {claimState}");
            }

            messenger.SendInfo(caller, sb.ToString().TrimEnd(), GlobalConstants.InfoLogChatGroup, (int)EnumChatType.Notification);
            return TextCommandResult.Success();
        }

        private TextCommandResult GiveBlock(TextCommandCallingArgs args)
        {
            IServerPlayer caller = args.Caller.Player as IServerPlayer;
            string targetName = args[0] as string;
            string quantityRaw = args[1] as string;

            IServerPlayer target = ResolveOnlinePlayer(targetName);
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

        private TextCommandResult DuplicateItems(TextCommandCallingArgs args)
        {
            IServerPlayer caller = args.Caller.Player as IServerPlayer;
            string graveSelector = args[0] as string;
            string targetName = args[1] as string;

            if (!TryResolveGraveSelector(caller, graveSelector, out string graveId, out string resolveMessage))
            {
                SendBoth(caller, resolveMessage);
                return TextCommandResult.Success();
            }

            IServerPlayer target = ResolveOnlinePlayer(targetName);
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

        private TextCommandResult Restore(TextCommandCallingArgs args)
        {
            IServerPlayer caller = args.Caller.Player as IServerPlayer;
            string graveSelector = args[0] as string;
            string targetName = args[1] as string;

            if (!TryResolveGraveSelector(caller, graveSelector, out string graveId, out string resolveMessage))
            {
                SendBoth(caller, resolveMessage);
                return TextCommandResult.Success();
            }

            IServerPlayer target = ResolveOnlinePlayer(targetName);
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

        private TextCommandResult Remove(TextCommandCallingArgs args)
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

            gravestoneService.TryRemoveGrave(graveId, out string message);
            SendBoth(caller, message);
            return TextCommandResult.Success();
        }

        private TextCommandResult Teleport(TextCommandCallingArgs args)
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

            backLocationStore.RecordCurrentLocation(caller);

            int? currentDimension = coordinateReader.GetDimension(caller);
            if (currentDimension.HasValue && currentDimension.Value != grave.Dimension)
            {
                entityPlayer.ChangeDimension(grave.Dimension);
            }

            entityPlayer.TeleportToDouble(
                target.X,
                target.Y,
                target.Z,
                () => SendBoth(caller, $"Teleported to gravestone '{grave.GraveId}' at {coordinateDisplayFormatter.FormatBlockPosition(grave.Dimension, grave.X, grave.Y, grave.Z)}."));

            return TextCommandResult.Success();
        }

        private bool TryResolveGraveSelector(IServerPlayer caller, string selector, out string graveId, out string message)
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

        private IServerPlayer ResolveOnlinePlayer(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            return playerLookup.FindOnlinePlayerByName(query) ?? playerLookup.FindOnlinePlayerByUid(query);
        }

        private void SendBoth(IServerPlayer player, string message)
        {
            messenger.SendDual(player, message, (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
        }
    }
}
