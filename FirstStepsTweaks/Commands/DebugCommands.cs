using FirstStepsTweaks.Discord;
using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class DebugCommands
    {
        private readonly ICoreServerAPI api;
        private readonly PlayerDebugCommands playerCommands;
        private readonly SpawnDebugCommands spawnCommands;
        private readonly WarpDebugCommands warpCommands;
        private readonly GraveDebugCommands graveCommands;
        private readonly DiscordDebugCommands discordCommands;

        public DebugCommands(
            ICoreServerAPI api,
            IPlayerLookup playerLookup,
            IPlayerMessenger messenger,
            JoinHistoryStore joinHistoryStore,
            KitClaimStore kitClaimStore,
            PlayerPlaytimeStore playtimeStore,
            HomeStore homeStore,
            TpaPreferenceStore tpaPreferenceStore,
            SpawnStore spawnStore,
            WarpStore warpStore,
            GravestoneService gravestoneService,
            IWorldCoordinateDisplayFormatter coordinateDisplayFormatter,
            IDiscordLinkedAccountStore linkedAccountStore,
            IPendingDiscordLinkCodeStore pendingCodeStore,
            IDiscordLinkRewardStateStore rewardStateStore,
            IDiscordLastMessageStore relayCursorStore,
            IDiscordLinkLastMessageStore linkCursorStore,
            DiscordLinkPollerStatusTracker linkPollerStatusTracker)
        {
            this.api = api;

            var playerInspector = new PlayerDebugDataInspector(
                joinHistoryStore,
                kitClaimStore,
                playtimeStore,
                homeStore,
                tpaPreferenceStore);

            var discordReader = new DiscordDebugStateReader(
                linkedAccountStore,
                pendingCodeStore,
                rewardStateStore,
                relayCursorStore,
                linkCursorStore,
                linkPollerStatusTracker);

            playerCommands = new PlayerDebugCommands(
                playerLookup,
                messenger,
                playerInspector,
                joinHistoryStore,
                kitClaimStore,
                playtimeStore,
                homeStore,
                tpaPreferenceStore);

            spawnCommands = new SpawnDebugCommands(spawnStore, messenger);
            warpCommands = new WarpDebugCommands(warpStore, messenger);
            graveCommands = new GraveDebugCommands(gravestoneService, messenger, coordinateDisplayFormatter);
            discordCommands = new DiscordDebugCommands(
                discordReader,
                linkedAccountStore,
                pendingCodeStore,
                rewardStateStore,
                relayCursorStore,
                linkCursorStore,
                messenger);
        }

        public void Register()
        {
            api.ChatCommands
                .Create("fsdebug")
                .WithDescription("Debug command surface for FirstStepsTweaks data")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("chattypes")
                    .WithDescription("Sends a message for each chat type to test formatting")
                    .RequiresPlayer()
                    .HandleWith(FsDebugChatTypes)
                .EndSubCommand()
                .BeginSubCommand("player")
                    .BeginSubCommand("inspect")
                        .WithArgs(api.ChatCommands.Parsers.Word("player"))
                        .HandleWith(playerCommands.Inspect)
                    .EndSubCommand()
                    .BeginSubCommand("join")
                        .BeginSubCommand("setfirstjoin")
                            .WithArgs(api.ChatCommands.Parsers.Word("player"), api.ChatCommands.Parsers.Word("value"))
                            .HandleWith(playerCommands.SetFirstJoin)
                        .EndSubCommand()
                        .BeginSubCommand("setlastseen")
                            .WithArgs(api.ChatCommands.Parsers.Word("player"), api.ChatCommands.Parsers.Word("totalDays"))
                            .HandleWith(playerCommands.SetLastSeen)
                        .EndSubCommand()
                    .EndSubCommand()
                    .BeginSubCommand("kits")
                        .BeginSubCommand("set")
                            .WithArgs(api.ChatCommands.Parsers.Word("player"), api.ChatCommands.Parsers.Word("kit"), api.ChatCommands.Parsers.Word("state"))
                            .HandleWith(playerCommands.SetKit)
                        .EndSubCommand()
                    .EndSubCommand()
                    .BeginSubCommand("playtime")
                        .BeginSubCommand("set")
                            .WithArgs(api.ChatCommands.Parsers.Word("player"), api.ChatCommands.Parsers.Word("seconds"))
                            .HandleWith(playerCommands.SetPlaytime)
                        .EndSubCommand()
                        .BeginSubCommand("reset")
                            .WithArgs(api.ChatCommands.Parsers.Word("player"))
                            .HandleWith(playerCommands.ResetPlaytime)
                        .EndSubCommand()
                    .EndSubCommand()
                    .BeginSubCommand("homes")
                        .BeginSubCommand("list")
                            .WithArgs(api.ChatCommands.Parsers.Word("player"))
                            .HandleWith(playerCommands.ListHomes)
                        .EndSubCommand()
                        .BeginSubCommand("set")
                            .WithArgs(api.ChatCommands.Parsers.Word("player"), api.ChatCommands.Parsers.Word("name"))
                            .HandleWith(playerCommands.SetHome)
                        .EndSubCommand()
                        .BeginSubCommand("remove")
                            .WithArgs(api.ChatCommands.Parsers.Word("player"), api.ChatCommands.Parsers.Word("name"))
                            .HandleWith(playerCommands.RemoveHome)
                        .EndSubCommand()
                        .BeginSubCommand("clear")
                            .WithArgs(api.ChatCommands.Parsers.Word("player"))
                            .HandleWith(playerCommands.ClearHomes)
                        .EndSubCommand()
                    .EndSubCommand()
                    .BeginSubCommand("tpa")
                        .BeginSubCommand("set")
                            .WithArgs(api.ChatCommands.Parsers.Word("player"), api.ChatCommands.Parsers.Word("state"))
                            .HandleWith(playerCommands.SetTpa)
                        .EndSubCommand()
                    .EndSubCommand()
                .EndSubCommand()
                .BeginSubCommand("spawn")
                    .BeginSubCommand("inspect")
                        .HandleWith(spawnCommands.Inspect)
                    .EndSubCommand()
                    .BeginSubCommand("clear")
                        .HandleWith(spawnCommands.Clear)
                    .EndSubCommand()
                .EndSubCommand()
                .BeginSubCommand("warps")
                    .BeginSubCommand("inspect")
                        .HandleWith(warpCommands.Inspect)
                    .EndSubCommand()
                    .BeginSubCommand("clear")
                        .HandleWith(warpCommands.Clear)
                    .EndSubCommand()
                .EndSubCommand()
                .BeginSubCommand("graves")
                    .BeginSubCommand("inspect")
                        .HandleWith(graveCommands.Inspect)
                    .EndSubCommand()
                    .BeginSubCommand("clear")
                        .HandleWith(graveCommands.Clear)
                    .EndSubCommand()
                .EndSubCommand()
                .BeginSubCommand("discord")
                    .BeginSubCommand("inspect")
                        .HandleWith(discordCommands.Inspect)
                    .EndSubCommand()
                    .BeginSubCommand("links")
                        .BeginSubCommand("set")
                            .WithArgs(api.ChatCommands.Parsers.Word("playerUid"), api.ChatCommands.Parsers.Word("discordUserId"))
                            .HandleWith(discordCommands.SetLink)
                        .EndSubCommand()
                        .BeginSubCommand("clear")
                            .WithArgs(api.ChatCommands.Parsers.Word("playerUid"))
                            .HandleWith(discordCommands.ClearLink)
                        .EndSubCommand()
                    .EndSubCommand()
                    .BeginSubCommand("rewards")
                        .BeginSubCommand("setclaimed")
                            .WithArgs(api.ChatCommands.Parsers.Word("playerUid"), api.ChatCommands.Parsers.Word("value"))
                            .HandleWith(discordCommands.SetClaimed)
                        .EndSubCommand()
                        .BeginSubCommand("setpending")
                            .WithArgs(api.ChatCommands.Parsers.Word("playerUid"), api.ChatCommands.Parsers.Word("value"))
                            .HandleWith(discordCommands.SetPending)
                        .EndSubCommand()
                    .EndSubCommand()
                    .BeginSubCommand("codes")
                        .BeginSubCommand("list")
                            .HandleWith(discordCommands.ListCodes)
                        .EndSubCommand()
                        .BeginSubCommand("set")
                            .WithArgs(api.ChatCommands.Parsers.Word("code"), api.ChatCommands.Parsers.Word("playerUid"), api.ChatCommands.Parsers.Word("expiresUtcTicks"))
                            .HandleWith(discordCommands.SetCode)
                        .EndSubCommand()
                        .BeginSubCommand("remove")
                            .WithArgs(api.ChatCommands.Parsers.Word("code"))
                            .HandleWith(discordCommands.RemoveCode)
                        .EndSubCommand()
                        .BeginSubCommand("clearplayer")
                            .WithArgs(api.ChatCommands.Parsers.Word("playerUid"))
                            .HandleWith(discordCommands.ClearCodesForPlayer)
                        .EndSubCommand()
                    .EndSubCommand()
                    .BeginSubCommand("cursors")
                        .BeginSubCommand("set")
                            .WithArgs(api.ChatCommands.Parsers.Word("cursor"), api.ChatCommands.Parsers.Word("messageId"))
                            .HandleWith(discordCommands.SetCursor)
                        .EndSubCommand()
                        .BeginSubCommand("clear")
                            .WithArgs(api.ChatCommands.Parsers.Word("cursor"))
                            .HandleWith(discordCommands.ClearCursor)
                        .EndSubCommand()
                    .EndSubCommand()
                .EndSubCommand();
        }

        private TextCommandResult FsDebugChatTypes(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;

            player.SendMessage(GlobalConstants.GeneralChatGroup, "AllGroups", EnumChatType.AllGroups);
            player.SendMessage(GlobalConstants.GeneralChatGroup, "CommandError", EnumChatType.CommandError);
            player.SendMessage(GlobalConstants.GeneralChatGroup, "CommandSuccess", EnumChatType.CommandSuccess);
            player.SendMessage(GlobalConstants.GeneralChatGroup, "GroupInvite", EnumChatType.GroupInvite);
            player.SendMessage(GlobalConstants.GeneralChatGroup, "JoinLeave", EnumChatType.JoinLeave);
            player.SendMessage(GlobalConstants.GeneralChatGroup, "Macro", EnumChatType.Macro);
            player.SendMessage(GlobalConstants.GeneralChatGroup, "Notification", EnumChatType.Notification);
            player.SendMessage(GlobalConstants.GeneralChatGroup, "OthersMessage", EnumChatType.OthersMessage);
            player.SendMessage(GlobalConstants.GeneralChatGroup, "OwnMessage", EnumChatType.OwnMessage);

            return TextCommandResult.Success();
        }
    }
}
