using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public static class TpaCommands
    {
        private class TpaRequest
        {
            public string RequesterUid;
            public string TargetUid;
            public long ExpireListenerId;
        }

        // targetUID -> list of requests
        private static Dictionary<string, List<TpaRequest>> pendingRequests =
            new Dictionary<string, List<TpaRequest>>();

        private const string TpaDisabledKey = "fst_tpa_disabled";

        public static void Register(ICoreServerAPI api)
        {
            api.ChatCommands.Create("tpa")
                .WithArgs(api.ChatCommands.Parsers.Word("player"))
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => Tpa(api, args));

            api.ChatCommands.Create("tpaccept")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => TpAccept(api, args));

            api.ChatCommands.Create("tpadeny")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => TpDeny(api, args));

            api.ChatCommands.Create("tpacancel")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => TpCancel(api, args));

            api.ChatCommands.Create("tpatoggle")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => TpaToggle(api, args));
        }

        private static bool IsTpaDisabled(ICoreServerAPI api, string uid)
        {
            var player = GetPlayer(api, uid);
            if (player == null) return false;

            return player.GetModData<bool>(TpaDisabledKey);
        }

        private static void SetTpaDisabled(ICoreServerAPI api, string uid, bool value)
        {
            var player = GetPlayer(api, uid);
            if (player == null) return;

            player.SetModData(TpaDisabledKey, value);
        }

        private static IServerPlayer GetPlayer(ICoreServerAPI api, string uid)
        {
            foreach (IServerPlayer plr in api.World.AllOnlinePlayers)
            {
                if (plr.PlayerUID == uid) return plr;
            }
            return null;
        }

        private static IServerPlayer GetPlayerByName(ICoreServerAPI api, string name)
        {
            foreach (IServerPlayer plr in api.World.AllOnlinePlayers)
            {
                if (plr.PlayerName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return plr;
            }
            return null;
        }

        private static TextCommandResult Tpa(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            var caller = (IServerPlayer)args.Caller.Player;
            string targetName = (string)args[0];

            var target = GetPlayerByName(api, targetName);

            if (target == null)
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "Player not found.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            if (target.PlayerUID == caller.PlayerUID)
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup, "You cannot teleport to yourself.", EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            if (IsTpaDisabled(api, target.PlayerUID))
            {
                caller.SendMessage(GlobalConstants.InfoLogChatGroup,
                    $"{target.PlayerName} is not accepting teleport requests.",
                    EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            if (!pendingRequests.ContainsKey(target.PlayerUID))
                pendingRequests[target.PlayerUID] = new List<TpaRequest>();

            var request = new TpaRequest
            {
                RequesterUid = caller.PlayerUID,
                TargetUid = target.PlayerUID
            };

            pendingRequests[target.PlayerUID].Add(request);

            request.ExpireListenerId = api.Event.RegisterCallback(dt =>
            {
                if (pendingRequests.TryGetValue(target.PlayerUID, out var list))
                {
                    list.Remove(request);
                }

                var requester = GetPlayer(api, request.RequesterUid);
                requester?.SendMessage(GlobalConstants.InfoLogChatGroup,
                    "Your teleport request expired.",
                    EnumChatType.CommandError);

            }, 180000);

            caller.SendMessage(GlobalConstants.InfoLogChatGroup,
                $"Teleport request sent to {target.PlayerName}.",
                EnumChatType.Notification);

            target.SendMessage(GlobalConstants.InfoLogChatGroup,
                $"{caller.PlayerName} wants to teleport to you. Use /tpaccept to accept.",
                EnumChatType.Notification);

            return TextCommandResult.Success();
        }

        private static TextCommandResult TpAccept(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            var target = (IServerPlayer)args.Caller.Player;

            if (!pendingRequests.TryGetValue(target.PlayerUID, out var list) || list.Count == 0)
            {
                target.SendMessage(GlobalConstants.InfoLogChatGroup,
                    "No pending teleport requests.",
                    EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            var request = list[0];
            list.RemoveAt(0);

            api.Event.UnregisterCallback(request.ExpireListenerId);

            var requester = GetPlayer(api, request.RequesterUid);
            if (requester == null)
            {
                target.SendMessage(GlobalConstants.InfoLogChatGroup,
                    "Requester is no longer online.",
                    EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            StartTeleportWarmup(api, requester, target);

            return TextCommandResult.Success();
        }

        private static void StartTeleportWarmup(ICoreServerAPI api, IServerPlayer requester, IServerPlayer target)
        {
            double startX = requester.Entity.Pos.X;
            double startY = requester.Entity.Pos.Y;
            double startZ = requester.Entity.Pos.Z;

            int seconds = 10;
            long listenerId = 0;

            requester.SendMessage(GlobalConstants.InfoLogChatGroup,
                "Teleporting in 10 seconds. Do not move.",
                EnumChatType.Notification);

            listenerId = api.Event.RegisterGameTickListener(dt =>
            {
                if (requester?.Entity == null)
                {
                    api.Event.UnregisterGameTickListener(listenerId);
                    return;
                }

                double dx = Math.Abs(requester.Entity.Pos.X - startX);
                double dy = Math.Abs(requester.Entity.Pos.Y - startY);
                double dz = Math.Abs(requester.Entity.Pos.Z - startZ);

                if (dx > 0.1 || dy > 0.1 || dz > 0.1)
                {
                    requester.SendMessage(GlobalConstants.InfoLogChatGroup,
                        "Teleport cancelled because you moved.",
                        EnumChatType.CommandError);

                    api.Event.UnregisterGameTickListener(listenerId);
                    return;
                }

                if (seconds > 0)
                {
                    requester.SendMessage(GlobalConstants.InfoLogChatGroup,
                        $"Teleporting in {seconds}...",
                        EnumChatType.Notification);

                    seconds--;
                }
                else
                {
                    BackCommands.RecordCurrentLocation(requester);
                    requester.Entity.TeleportToDouble(
                        target.Entity.Pos.X,
                        target.Entity.Pos.Y,
                        target.Entity.Pos.Z
                    );

                    requester.SendMessage(GlobalConstants.InfoLogChatGroup,
                        "Teleported.",
                        EnumChatType.CommandSuccess);

                    api.Event.UnregisterGameTickListener(listenerId);
                }

            }, 1000);
        }

        private static TextCommandResult TpDeny(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            var target = (IServerPlayer)args.Caller.Player;

            if (!pendingRequests.TryGetValue(target.PlayerUID, out var list) || list.Count == 0)
            {
                target.SendMessage(GlobalConstants.InfoLogChatGroup,
                    "No pending teleport requests.",
                    EnumChatType.CommandError);
                return TextCommandResult.Success();
            }

            var request = list[0];
            list.RemoveAt(0);

            api.Event.UnregisterCallback(request.ExpireListenerId);

            var requester = GetPlayer(api, request.RequesterUid);
            requester?.SendMessage(GlobalConstants.InfoLogChatGroup,
                "Your teleport request was denied.",
                EnumChatType.CommandError);

            return TextCommandResult.Success();
        }

        private static TextCommandResult TpCancel(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            var caller = (IServerPlayer)args.Caller.Player;

            foreach (var kvp in pendingRequests)
            {
                var list = kvp.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].RequesterUid == caller.PlayerUID)
                    {
                        api.Event.UnregisterCallback(list[i].ExpireListenerId);
                        list.RemoveAt(i);

                        caller.SendMessage(GlobalConstants.InfoLogChatGroup,
                            "Teleport request cancelled.",
                            EnumChatType.Notification);

                        return TextCommandResult.Success();
                    }
                }
            }

            caller.SendMessage(GlobalConstants.InfoLogChatGroup,
                "You have no pending requests.",
                EnumChatType.CommandError);

            return TextCommandResult.Success();
        }

        private static TextCommandResult TpaToggle(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;
            string uid = player.PlayerUID;

            bool currentlyDisabled = IsTpaDisabled(api, uid);
            bool newState = !currentlyDisabled;

            SetTpaDisabled(api, uid, newState);

            if (newState)
            {
                if (pendingRequests.TryGetValue(uid, out var list))
                {
                    foreach (var request in list)
                    {
                        api.Event.UnregisterCallback(request.ExpireListenerId);

                        var requester = GetPlayer(api, request.RequesterUid);
                        requester?.SendMessage(GlobalConstants.InfoLogChatGroup,
                            "Your teleport request was automatically denied.",
                            EnumChatType.CommandError);
                    }

                    list.Clear();
                }

                player.SendMessage(GlobalConstants.InfoLogChatGroup,
                    "TPA requests DISABLED.",
                    EnumChatType.Notification);
            }
            else
            {
                player.SendMessage(GlobalConstants.InfoLogChatGroup,
                    "TPA requests ENABLED.",
                    EnumChatType.Notification);
            }

            return TextCommandResult.Success();
        }
    }
}