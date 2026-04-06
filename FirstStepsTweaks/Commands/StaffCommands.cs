using System;
using System.Linq;
using System.Text;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class StaffCommands
    {
        private const string PermissionDeniedMessage = "You need Vintage Story admin access or FirstStepsTweaks staff admin privileges to use /fststaff.";
        private readonly ICoreServerAPI api;
        private readonly IStaffAssignmentStore staffAssignmentStore;
        private readonly IStaffStatusReader staffStatusReader;
        private readonly StaffPrivilegeSyncService staffPrivilegeSyncService;
        private readonly StaffTargetResolver staffTargetResolver;
        private readonly IPlayerRoleCodeReader roleCodeReader;

        public StaffCommands(
            ICoreServerAPI api,
            IStaffAssignmentStore staffAssignmentStore,
            IStaffStatusReader staffStatusReader,
            StaffPrivilegeSyncService staffPrivilegeSyncService,
            IPlayerLookup playerLookup)
        {
            this.api = api;
            this.staffAssignmentStore = staffAssignmentStore;
            this.staffStatusReader = staffStatusReader;
            this.staffPrivilegeSyncService = staffPrivilegeSyncService;
            staffTargetResolver = new StaffTargetResolver(playerLookup);
            roleCodeReader = new PlayerRoleCodeReader();
        }

        public void Register()
        {
                api.ChatCommands
                .Create("fststaff")
                .WithDescription("Manage FirstStepsTweaks admin and moderator assignments")
                .RequiresPrivilege(Privilege.chat)
                .BeginSubCommand("list")
                    .WithDescription("List stored staff assignments")
                    .HandleWith(List)
                .EndSubCommand()
                .BeginSubCommand("status")
                    .WithDescription("Show stored staff assignment details for a player or UID")
                    .WithArgs(api.ChatCommands.Parsers.Word("playerOrUid"))
                    .HandleWith(Status)
                .EndSubCommand()
                .BeginSubCommand("set")
                    .WithDescription("Set a player's staff level to admin, moderator, or none")
                    .WithArgs(api.ChatCommands.Parsers.Word("playerOrUid"), api.ChatCommands.Parsers.Word("level"))
                    .HandleWith(Set)
                .EndSubCommand()
                .BeginSubCommand("sync")
                    .WithDescription("Re-apply managed staff privileges to an online player")
                    .WithArgs(api.ChatCommands.Parsers.Word("playerOrUid"))
                    .HandleWith(Sync);
        }

        private TextCommandResult List(TextCommandCallingArgs args)
        {
            if (!TryAuthorize(args, out IServerPlayer caller))
            {
                return TextCommandResult.Success();
            }

            StaffRoster roster = staffAssignmentStore.LoadRoster();

            var adminAssignments = roster.Assignments
                .Where(assignment => assignment.Level == StaffLevel.Admin)
                .OrderBy(assignment => GetDisplayName(assignment), StringComparer.OrdinalIgnoreCase)
                .ToList();
            var moderatorAssignments = roster.Assignments
                .Where(assignment => assignment.Level == StaffLevel.Moderator)
                .OrderBy(assignment => GetDisplayName(assignment), StringComparer.OrdinalIgnoreCase)
                .ToList();
            var legacyAssignments = roster.LegacyAssignments
                .OrderBy(assignment => assignment.PlayerName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (adminAssignments.Count == 0 && moderatorAssignments.Count == 0 && legacyAssignments.Count == 0)
            {
                Send(caller, "No staff assignments are stored.");
                return TextCommandResult.Success();
            }

            var builder = new StringBuilder();
            builder.AppendLine("Stored staff assignments:");

            AppendAssignments(builder, "Admins", adminAssignments);
            AppendAssignments(builder, "Moderators", moderatorAssignments);

            if (legacyAssignments.Count > 0)
            {
                builder.AppendLine("Pending legacy name-only admins:");
                foreach (LegacyStaffAssignment assignment in legacyAssignments)
                {
                    builder.AppendLine($"- {assignment.PlayerName} ({FormatLevel(assignment.Level)})");
                }
            }

            Send(caller, builder.ToString().TrimEnd());
            return TextCommandResult.Success();
        }

        private TextCommandResult Status(TextCommandCallingArgs args)
        {
            if (!TryAuthorize(args, out IServerPlayer caller))
            {
                return TextCommandResult.Success();
            }

            string token = (string)args[0];
            StaffCommandTarget target = staffTargetResolver.ResolvePersistentTarget(token);
            if (target == null)
            {
                Send(caller, "Player or UID is required.");
                return TextCommandResult.Success();
            }

            StaffLevel level = staffStatusReader.GetLevel(target.PlayerUid, target.OnlinePlayer?.PlayerName);
            var builder = new StringBuilder();
            builder.AppendLine($"Target: {target.DisplayName}");
            builder.AppendLine($"UID: {target.PlayerUid}");
            builder.AppendLine($"Stored level: {FormatLevel(level)}");

            if (target.OnlinePlayer != null)
            {
                bool privilegesAligned = staffPrivilegeSyncService.HasExpectedPrivileges(target.OnlinePlayer, level);
                builder.AppendLine($"Online: yes");
                builder.AppendLine($"Managed privileges aligned: {(privilegesAligned ? "yes" : "no")}");
            }
            else
            {
                builder.AppendLine("Online: no");
            }

            Send(caller, builder.ToString().TrimEnd());
            return TextCommandResult.Success();
        }

        private TextCommandResult Set(TextCommandCallingArgs args)
        {
            if (!TryAuthorize(args, out IServerPlayer caller))
            {
                return TextCommandResult.Success();
            }

            string token = (string)args[0];
            if (!TryParseLevel((string)args[1], out StaffLevel level))
            {
                Send(caller, "Staff level must be admin, moderator, or none.");
                return TextCommandResult.Success();
            }

            StaffCommandTarget target = staffTargetResolver.ResolvePersistentTarget(token);
            if (target == null)
            {
                Send(caller, "Player or UID is required.");
                return TextCommandResult.Success();
            }

            StaffRoster roster = staffAssignmentStore.LoadRoster();
            if (level == StaffLevel.None)
            {
                roster.Assignments.RemoveAll(assignment =>
                    string.Equals(assignment.PlayerUid, target.PlayerUid, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                StaffAssignment assignment = roster.Assignments.FirstOrDefault(candidate =>
                    string.Equals(candidate.PlayerUid, target.PlayerUid, StringComparison.OrdinalIgnoreCase));

                if (assignment == null)
                {
                    roster.Assignments.Add(new StaffAssignment
                    {
                        PlayerUid = target.PlayerUid,
                        LastKnownPlayerName = target.OnlinePlayer?.PlayerName ?? string.Empty,
                        Level = level
                    });
                }
                else
                {
                    assignment.Level = level;
                    assignment.LastKnownPlayerName = target.OnlinePlayer?.PlayerName ?? assignment.LastKnownPlayerName ?? string.Empty;
                }
            }

            if (target.OnlinePlayer != null)
            {
                roster.LegacyAssignments.RemoveAll(assignment =>
                    string.Equals(assignment.PlayerName, target.OnlinePlayer.PlayerName, StringComparison.OrdinalIgnoreCase));
            }

            staffAssignmentStore.SaveRoster(roster);

            if (target.OnlinePlayer != null)
            {
                if (level == StaffLevel.None)
                {
                    staffPrivilegeSyncService.RemoveManagedPrivileges(target.OnlinePlayer);
                }
                else
                {
                    staffPrivilegeSyncService.ApplyAssignmentToOnlinePlayer(target.OnlinePlayer, level);
                }
            }

            Send(caller, level == StaffLevel.None
                ? $"Removed staff assignment for {target.DisplayName}."
                : $"Set {target.DisplayName} to {FormatLevel(level)}.");
            return TextCommandResult.Success();
        }

        private TextCommandResult Sync(TextCommandCallingArgs args)
        {
            if (!TryAuthorize(args, out IServerPlayer caller))
            {
                return TextCommandResult.Success();
            }

            string token = (string)args[0];
            StaffCommandTarget target = staffTargetResolver.ResolveOnlineTarget(token);
            if (target?.OnlinePlayer == null)
            {
                Send(caller, "Target player must be online to sync staff privileges.");
                return TextCommandResult.Success();
            }

            staffPrivilegeSyncService.SyncOnlinePlayer(target.OnlinePlayer);
            StaffLevel level = staffStatusReader.GetLevel(target.OnlinePlayer);
            bool aligned = staffPrivilegeSyncService.HasExpectedPrivileges(target.OnlinePlayer, level);
            Send(caller, $"Synced {target.DisplayName}. Managed privileges aligned: {(aligned ? "yes" : "no")}.");
            return TextCommandResult.Success();
        }

        private void AppendAssignments(StringBuilder builder, string header, System.Collections.Generic.IReadOnlyCollection<StaffAssignment> assignments)
        {
            if (assignments.Count == 0)
            {
                return;
            }

            builder.AppendLine($"{header}:");
            foreach (StaffAssignment assignment in assignments)
            {
                builder.AppendLine($"- {GetDisplayName(assignment)} [{assignment.PlayerUid}]");
            }
        }

        private static string GetDisplayName(StaffAssignment assignment)
        {
            return string.IsNullOrWhiteSpace(assignment?.LastKnownPlayerName)
                ? assignment?.PlayerUid ?? string.Empty
                : assignment.LastKnownPlayerName;
        }

        private static string FormatLevel(StaffLevel level)
        {
            return level switch
            {
                StaffLevel.Admin => "admin",
                StaffLevel.Moderator => "moderator",
                _ => "none"
            };
        }

        private bool TryParseLevel(string value, out StaffLevel level)
        {
            if (string.Equals(value, "admin", StringComparison.OrdinalIgnoreCase))
            {
                level = StaffLevel.Admin;
                return true;
            }

            if (string.Equals(value, "moderator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "mod", StringComparison.OrdinalIgnoreCase))
            {
                level = StaffLevel.Moderator;
                return true;
            }

            if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
            {
                level = StaffLevel.None;
                return true;
            }

            level = StaffLevel.None;
            return false;
        }

        private bool TryAuthorize(TextCommandCallingArgs args, out IServerPlayer caller)
        {
            caller = args.Caller.Player as IServerPlayer;
            if (caller == null)
            {
                return true;
            }

            if (caller.HasPrivilege(Privilege.controlserver)
                || caller.HasPrivilege(StaffPrivilegeCatalog.AdminPrivilege)
                || HasBootstrapAdminRole(caller))
            {
                return true;
            }

            Send(caller, PermissionDeniedMessage);
            return false;
        }

        private bool HasBootstrapAdminRole(IServerPlayer player)
        {
            string roleCode = roleCodeReader.Read(player);
            if (string.IsNullOrWhiteSpace(roleCode))
            {
                return false;
            }

            return string.Equals(roleCode, "admin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(roleCode, "su", StringComparison.OrdinalIgnoreCase)
                || roleCode.IndexOf("admin", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Send(IServerPlayer player, string message)
        {
            if (player == null)
            {
                api.Logger.Notification($"[FirstStepsTweaks] {message}");
                return;
            }

            player.SendMessage(GlobalConstants.InfoLogChatGroup, message, EnumChatType.CommandSuccess);
            player.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
        }
    }
}
