using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class PlayerDebugCommands
    {
        private readonly IPlayerLookup playerLookup;
        private readonly IPlayerMessenger messenger;
        private readonly PlayerDebugDataInspector inspector;
        private readonly JoinHistoryStore joinHistoryStore;
        private readonly KitClaimStore kitClaimStore;
        private readonly PlayerPlaytimeStore playtimeStore;
        private readonly HomeStore homeStore;
        private readonly TpaPreferenceStore tpaPreferenceStore;

        public PlayerDebugCommands(
            IPlayerLookup playerLookup,
            IPlayerMessenger messenger,
            PlayerDebugDataInspector inspector,
            JoinHistoryStore joinHistoryStore,
            KitClaimStore kitClaimStore,
            PlayerPlaytimeStore playtimeStore,
            HomeStore homeStore,
            TpaPreferenceStore tpaPreferenceStore)
        {
            this.playerLookup = playerLookup;
            this.messenger = messenger;
            this.inspector = inspector;
            this.joinHistoryStore = joinHistoryStore;
            this.kitClaimStore = kitClaimStore;
            this.playtimeStore = playtimeStore;
            this.homeStore = homeStore;
            this.tpaPreferenceStore = tpaPreferenceStore;
        }

        public TextCommandResult Inspect(TextCommandCallingArgs args)
        {
            IServerPlayer target = ResolveOnlinePlayer(args[0] as string, args.Caller.Player);
            if (target == null)
            {
                return TextCommandResult.Success();
            }

            SendInfo(args.Caller.Player, inspector.Format(inspector.Capture(target)));
            return TextCommandResult.Success();
        }

        public TextCommandResult SetFirstJoin(TextCommandCallingArgs args)
        {
            IServerPlayer target = ResolveOnlinePlayer(args[0] as string, args.Caller.Player);
            if (target == null)
            {
                return TextCommandResult.Success();
            }

            if (!DebugCommandSupport.TryParseBoolean(args[1] as string, out bool value))
            {
                SendDual(args.Caller.Player, "Value must be true or false.");
                return TextCommandResult.Success();
            }

            joinHistoryStore.SetFirstJoinRecorded(target, value);
            SendDual(args.Caller.Player, $"Set first join recorded for {target.PlayerName} to {value}.");
            return TextCommandResult.Success();
        }

        public TextCommandResult SetLastSeen(TextCommandCallingArgs args)
        {
            IServerPlayer target = ResolveOnlinePlayer(args[0] as string, args.Caller.Player);
            if (target == null)
            {
                return TextCommandResult.Success();
            }

            if (!DebugCommandSupport.TryParseDouble(args[1] as string, out double totalDays))
            {
                SendDual(args.Caller.Player, "totalDays must be a valid number.");
                return TextCommandResult.Success();
            }

            joinHistoryStore.SetLastSeenDay(target, totalDays);
            SendDual(args.Caller.Player, $"Set last seen total days for {target.PlayerName} to {totalDays:0.###}.");
            return TextCommandResult.Success();
        }

        public TextCommandResult SetKit(TextCommandCallingArgs args)
        {
            IServerPlayer target = ResolveOnlinePlayer(args[0] as string, args.Caller.Player);
            if (target == null)
            {
                return TextCommandResult.Success();
            }

            string kitName = (args[1] as string ?? string.Empty).Trim().ToLowerInvariant();
            string state = (args[2] as string ?? string.Empty).Trim().ToLowerInvariant();
            bool claimed;

            switch (state)
            {
                case "claimed":
                    claimed = true;
                    break;
                case "unclaimed":
                    claimed = false;
                    break;
                default:
                    SendDual(args.Caller.Player, "State must be claimed or unclaimed.");
                    return TextCommandResult.Success();
            }

            switch (kitName)
            {
                case "starter":
                    kitClaimStore.SetStarterClaimed(target, claimed);
                    break;
                case "winter":
                    kitClaimStore.SetWinterClaimed(target, claimed);
                    break;
                default:
                    SendDual(args.Caller.Player, "Kit must be starter or winter.");
                    return TextCommandResult.Success();
            }

            SendDual(args.Caller.Player, $"Set {kitName} kit for {target.PlayerName} to {state}.");
            return TextCommandResult.Success();
        }

        public TextCommandResult SetPlaytime(TextCommandCallingArgs args)
        {
            IServerPlayer target = ResolveOnlinePlayer(args[0] as string, args.Caller.Player);
            if (target == null)
            {
                return TextCommandResult.Success();
            }

            if (!DebugCommandSupport.TryParseLong(args[1] as string, out long seconds))
            {
                SendDual(args.Caller.Player, "seconds must be a whole number.");
                return TextCommandResult.Success();
            }

            playtimeStore.SetTotalPlayedSeconds(target, seconds);
            SendDual(args.Caller.Player, $"Set playtime for {target.PlayerName} to {System.Math.Max(0, seconds)} seconds.");
            return TextCommandResult.Success();
        }

        public TextCommandResult ResetPlaytime(TextCommandCallingArgs args)
        {
            IServerPlayer target = ResolveOnlinePlayer(args[0] as string, args.Caller.Player);
            if (target == null)
            {
                return TextCommandResult.Success();
            }

            playtimeStore.ResetTotalPlayedSeconds(target);
            SendDual(args.Caller.Player, $"Reset playtime for {target.PlayerName}.");
            return TextCommandResult.Success();
        }

        public TextCommandResult ListHomes(TextCommandCallingArgs args)
        {
            return Inspect(args);
        }

        public TextCommandResult SetHome(TextCommandCallingArgs args)
        {
            IServerPlayer target = ResolveOnlinePlayer(args[0] as string, args.Caller.Player);
            if (target == null)
            {
                return TextCommandResult.Success();
            }

            string homeName = args[1] as string;
            if (string.IsNullOrWhiteSpace(homeName))
            {
                SendDual(args.Caller.Player, "Home name cannot be empty.");
                return TextCommandResult.Success();
            }

            homeStore.Set(target, homeName);
            SendDual(args.Caller.Player, $"Set home '{homeStore.NormalizeHomeName(homeName)}' for {target.PlayerName}.");
            return TextCommandResult.Success();
        }

        public TextCommandResult RemoveHome(TextCommandCallingArgs args)
        {
            IServerPlayer target = ResolveOnlinePlayer(args[0] as string, args.Caller.Player);
            if (target == null)
            {
                return TextCommandResult.Success();
            }

            string homeName = args[1] as string;
            if (!homeStore.Remove(target, homeName))
            {
                SendDual(args.Caller.Player, $"Home '{homeName}' does not exist for {target.PlayerName}.");
                return TextCommandResult.Success();
            }

            SendDual(args.Caller.Player, $"Removed home '{homeStore.NormalizeHomeName(homeName)}' for {target.PlayerName}.");
            return TextCommandResult.Success();
        }

        public TextCommandResult ClearHomes(TextCommandCallingArgs args)
        {
            IServerPlayer target = ResolveOnlinePlayer(args[0] as string, args.Caller.Player);
            if (target == null)
            {
                return TextCommandResult.Success();
            }

            homeStore.Clear(target);
            SendDual(args.Caller.Player, $"Cleared all homes for {target.PlayerName}.");
            return TextCommandResult.Success();
        }

        public TextCommandResult SetTpa(TextCommandCallingArgs args)
        {
            IServerPlayer target = ResolveOnlinePlayer(args[0] as string, args.Caller.Player);
            if (target == null)
            {
                return TextCommandResult.Success();
            }

            string state = (args[1] as string ?? string.Empty).Trim().ToLowerInvariant();
            if (state == "enabled")
            {
                tpaPreferenceStore.SetDisabled(target, false);
                SendDual(args.Caller.Player, $"Enabled TPA requests for {target.PlayerName}.");
                return TextCommandResult.Success();
            }

            if (state == "disabled")
            {
                tpaPreferenceStore.SetDisabled(target, true);
                SendDual(args.Caller.Player, $"Disabled TPA requests for {target.PlayerName}.");
                return TextCommandResult.Success();
            }

            SendDual(args.Caller.Player, "State must be enabled or disabled.");
            return TextCommandResult.Success();
        }

        private IServerPlayer ResolveOnlinePlayer(string query, IPlayer caller)
        {
            IServerPlayer player = playerLookup.FindOnlinePlayerByName(query) ?? playerLookup.FindOnlinePlayerByUid(query);
            if (player != null)
            {
                return player;
            }

            SendDual(caller, "Target player is not online.");
            return null;
        }

        private void SendDual(IPlayer caller, string message)
        {
            if (caller is IServerPlayer serverPlayer)
            {
                messenger.SendDual(serverPlayer, message, (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
            }
        }

        private void SendInfo(IPlayer caller, string message)
        {
            if (caller is IServerPlayer serverPlayer)
            {
                messenger.SendInfo(serverPlayer, message, GlobalConstants.InfoLogChatGroup, (int)EnumChatType.Notification);
            }
        }
    }
}
