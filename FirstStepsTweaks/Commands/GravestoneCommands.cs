using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Services;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class GravestoneCommands
    {
        private const string AdminPrivilege = "firststepstweaks.graveadmin";
        private const int DefaultListRadius = 100;
        private const int DefaultListPage = 1;

        private readonly ICoreServerAPI api;
        private readonly GravestoneService gravestoneService;
        private readonly IPlayerMessenger messenger;
        private readonly IPlayerLookup playerLookup;
        private readonly IBackLocationStore backLocationStore;
        private readonly IWorldCoordinateReader coordinateReader;
        private readonly IWorldCoordinateDisplayFormatter coordinateDisplayFormatter;
        private readonly GraveAdminNearbyQuery nearbyQuery;
        private readonly GraveAdminListSnapshotStore snapshotStore;
        private readonly GraveAdminSelectorResolver selectorResolver;
        private readonly GraveAdminPageFormatter pageFormatter;
        private readonly GraveAdminRestoreTargetResolver restoreTargetResolver;
        private readonly GraveAdminInfoPresenter infoPresenter;

        public GravestoneCommands(
            ICoreServerAPI api,
            GravestoneService gravestoneService,
            IPlayerMessenger messenger,
            IPlayerLookup playerLookup,
            IBackLocationStore backLocationStore,
            IWorldCoordinateReader coordinateReader,
            IWorldCoordinateDisplayFormatter coordinateDisplayFormatter,
            GraveAdminNearbyQuery nearbyQuery,
            GraveAdminListSnapshotStore snapshotStore,
            GraveAdminSelectorResolver selectorResolver,
            GraveAdminPageFormatter pageFormatter,
            GraveAdminRestoreTargetResolver restoreTargetResolver,
            GraveAdminInfoPresenter infoPresenter)
        {
            this.api = api;
            this.gravestoneService = gravestoneService;
            this.messenger = messenger;
            this.playerLookup = playerLookup;
            this.backLocationStore = backLocationStore;
            this.coordinateReader = coordinateReader ?? new WorldCoordinateReader();
            this.coordinateDisplayFormatter = coordinateDisplayFormatter ?? new WorldCoordinateDisplayFormatter(api);
            this.nearbyQuery = nearbyQuery;
            this.snapshotStore = snapshotStore;
            this.selectorResolver = selectorResolver;
            this.pageFormatter = pageFormatter;
            this.restoreTargetResolver = restoreTargetResolver;
            this.infoPresenter = infoPresenter;
        }

        public void Register()
        {
            api.ChatCommands
                .Create("graveadmin")
                .WithDescription("Admin tools for gravestone management")
                .RequiresPlayer()
                .RequiresPrivilege(AdminPrivilege)
                .BeginSubCommand("list")
                    .WithDescription("List nearby active gravestones")
                    .WithArgs(
                        api.ChatCommands.Parsers.OptionalInt("radius"),
                        api.ChatCommands.Parsers.OptionalInt("page")
                    )
                    .HandleWith(List)
                .EndSubCommand()
                .BeginSubCommand("info")
                    .WithDescription("Show information for the gravestone you are currently looking at")
                    .HandleWith(Info)
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
                    .WithDescription("Duplicate stored gravestone items to a player without removing the gravestone. Use currentloc <player>, <graveNumber> <player>, or <graveId> <player>.")
                    .WithArgs(
                        api.ChatCommands.Parsers.Word("graveId"),
                        api.ChatCommands.Parsers.Word("player")
                    )
                    .HandleWith(DuplicateItems)
                .EndSubCommand()
                .BeginSubCommand("restore")
                    .WithDescription("Restore gravestone items to a player and remove the gravestone. Use currentloc [player], <graveNumber> [player], or <graveId> [player].")
                    .WithArgs(
                        api.ChatCommands.Parsers.Word("graveId"),
                        api.ChatCommands.Parsers.OptionalWord("player")
                    )
                    .HandleWith(Restore)
                .EndSubCommand()
                .BeginSubCommand("remove")
                    .WithDescription("Remove a gravestone without restoring items. Use currentloc while looking at a gravestone, or provide <graveNumber|graveId>.")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("graveId"))
                    .HandleWith(Remove)
                .EndSubCommand()
                .BeginSubCommand("teleport")
                    .WithDescription("Teleport directly to a gravestone. Use currentloc while looking at a gravestone, or provide <graveNumber|graveId>.")
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

            if (!TryParsePositiveInt(args[0], DefaultListRadius, "Radius", out int radius, out string parseMessage)
                || !TryParsePositiveInt(args[1], DefaultListPage, "Page", out int page, out parseMessage))
            {
                SendBoth(caller, parseMessage);
                return TextCommandResult.Success();
            }

            if (!nearbyQuery.TryQuery(caller, gravestoneService.GetActiveGraves(), gravestoneService.IsPubliclyClaimable, radius, out var entries, out string queryMessage))
            {
                SendBoth(caller, queryMessage);
                return TextCommandResult.Success();
            }

            snapshotStore.Save(caller, radius, entries);

            if (!pageFormatter.TryFormat(entries, radius, page, out string pageMessage))
            {
                SendBoth(caller, pageMessage);
                return TextCommandResult.Success();
            }

            if (entries.Count == 0)
            {
                SendBoth(caller, pageMessage);
                return TextCommandResult.Success();
            }

            messenger.SendInfo(caller, pageMessage, GlobalConstants.InfoLogChatGroup, (int)EnumChatType.Notification);
            messenger.SendGeneral(caller, "Nearby gravestone details were sent to your Info log channel.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
            return TextCommandResult.Success();
        }

        private TextCommandResult Info(TextCommandCallingArgs args)
        {
            IServerPlayer caller = args.Caller.Player as IServerPlayer;
            infoPresenter.ShowLookedAtGraveInfo(caller);
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

            if (!selectorResolver.TryResolve(caller, graveSelector, out string graveId, out string resolveMessage))
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

            if (!selectorResolver.TryResolve(caller, graveSelector, out string graveId, out string resolveMessage))
            {
                SendBoth(caller, resolveMessage);
                return TextCommandResult.Success();
            }

            if (!gravestoneService.TryGetActiveGrave(graveId, out GraveData grave) || grave == null)
            {
                SendBoth(caller, $"Gravestone '{graveId}' was not found.");
                return TextCommandResult.Success();
            }

            if (!restoreTargetResolver.TryResolve(targetName, grave, out IServerPlayer target, out string targetMessage))
            {
                SendBoth(caller, targetMessage);
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
                SendBoth(caller, "Usage: /graveadmin remove currentloc while looking at a gravestone, or /graveadmin remove <graveNumber|graveId>.");
                return TextCommandResult.Success();
            }

            if (!selectorResolver.TryResolve(caller, graveSelector, out string graveId, out string resolveMessage))
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
                SendBoth(caller, "Usage: /graveadmin teleport currentloc while looking at a gravestone, or /graveadmin teleport <graveNumber|graveId>.");
                return TextCommandResult.Success();
            }

            if (!selectorResolver.TryResolve(caller, graveSelector, out string graveId, out string resolveMessage))
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

        private static bool TryParsePositiveInt(object rawValue, int defaultValue, string label, out int parsedValue, out string message)
        {
            parsedValue = defaultValue;
            message = string.Empty;

            if (rawValue == null)
            {
                return true;
            }

            if (rawValue is int directValue)
            {
                parsedValue = directValue;
            }
            else if (rawValue is string rawText)
            {
                if (string.IsNullOrWhiteSpace(rawText))
                {
                    return true;
                }

                if (!int.TryParse(rawText, out parsedValue))
                {
                    message = $"{label} must be a positive whole number.";
                    return false;
                }
            }
            else if (!int.TryParse(rawValue.ToString(), out parsedValue))
            {
                message = $"{label} must be a positive whole number.";
                return false;
            }

            if (parsedValue <= 0)
            {
                message = $"{label} must be a positive whole number.";
                return false;
            }

            return true;
        }
    }
}
