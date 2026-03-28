using System;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Services;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class TpaCommands
    {
        private readonly ICoreServerAPI api;
        private readonly TeleportConfig teleportConfig;
        private readonly IPlayerMessenger messenger;
        private readonly IPlayerLookup playerLookup;
        private readonly ITeleportWarmupService teleportWarmupService;
        private readonly IBackLocationStore backLocationStore;
        private readonly PlayerTeleportWarmupResolver warmupResolver;
        private readonly TpaPreferenceStore preferenceStore;
        private readonly TpaRequestStore requestStore;

        public TpaCommands(
            ICoreServerAPI api,
            FirstStepsTweaksConfig config,
            IPlayerMessenger messenger,
            IPlayerLookup playerLookup,
            ITeleportWarmupService teleportWarmupService,
            IBackLocationStore backLocationStore,
            PlayerTeleportWarmupResolver warmupResolver,
            TpaPreferenceStore preferenceStore,
            TpaRequestStore requestStore)
        {
            this.api = api;
            teleportConfig = config?.Teleport ?? new TeleportConfig();
            this.messenger = messenger;
            this.playerLookup = playerLookup;
            this.teleportWarmupService = teleportWarmupService;
            this.backLocationStore = backLocationStore;
            this.warmupResolver = warmupResolver;
            this.preferenceStore = preferenceStore;
            this.requestStore = requestStore;
        }

        public void Register()
        {
            api.ChatCommands.Create("tpa")
                .WithArgs(api.ChatCommands.Parsers.Word("player"))
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(Tpa);

            api.ChatCommands.Create("tpaccept")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(TpAccept);

            api.ChatCommands.Create("tpadeny")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(TpDeny);

            api.ChatCommands.Create("tpacancel")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(TpCancel);

            api.ChatCommands.Create("tpatoggle")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(TpaToggle);
        }

        private bool IsTpaDisabled(string uid)
        {
            return preferenceStore.IsDisabled(playerLookup.FindOnlinePlayerByUid(uid));
        }

        private void SetTpaDisabled(string uid, bool value)
        {
            preferenceStore.SetDisabled(playerLookup.FindOnlinePlayerByUid(uid), value);
        }

        private TextCommandResult Tpa(TextCommandCallingArgs args)
        {
            var caller = (IServerPlayer)args.Caller.Player;
            string targetName = (string)args[0];

            var target = playerLookup.FindOnlinePlayerByName(targetName);

            if (target == null)
            {
                messenger.SendDual(caller, "Player not found.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            if (target.PlayerUID == caller.PlayerUID)
            {
                messenger.SendDual(caller, "You cannot teleport to yourself.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            if (IsTpaDisabled(target.PlayerUID))
            {
                messenger.SendInfo(caller, $"{target.PlayerName} is not accepting teleport requests.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
                messenger.SendGeneral(caller, $"{target.PlayerName} is not accepting teleport requests.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            var request = new TpaRequestRecord
            {
                RequesterUid = caller.PlayerUID,
                TargetUid = target.PlayerUID
            };

            requestStore.Add(request);

            request.ExpireListenerId = api.Event.RegisterCallback(dt =>
            {
                requestStore.Remove(target.PlayerUID, request);

                var requester = playerLookup.FindOnlinePlayerByUid(request.RequesterUid);
                messenger.SendInfo(requester, "Your teleport request expired.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
                messenger.SendGeneral(requester, "Your teleport request expired.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);

            }, teleportConfig.TpaExpireMs);

            messenger.SendInfo(caller, $"Teleport request sent to {target.PlayerName}.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
            messenger.SendGeneral(caller, $"Teleport request sent to {target.PlayerName}.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);

            messenger.SendInfo(target, $"{caller.PlayerName} wants to teleport to you. Use /tpaccept to accept.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
            messenger.SendGeneral(target, $"{caller.PlayerName} wants to teleport to you. Use /tpaccept to accept.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);

            messenger.SendInfo(target, $"You recieved a teleport request from {caller.PlayerName}.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
            messenger.SendGeneral(target, $"You recieved a teleport request from {caller.PlayerName}.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);

            return TextCommandResult.Success();
        }

        private TextCommandResult TpAccept(TextCommandCallingArgs args)
        {
            var target = (IServerPlayer)args.Caller.Player;

            if (!requestStore.TryTakeFirst(target.PlayerUID, out TpaRequestRecord request))
            {
                messenger.SendInfo(target, "No pending teleport requests.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
                messenger.SendGeneral(target, "No pending teleport requests.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            api.Event.UnregisterCallback(request.ExpireListenerId);

            var requester = playerLookup.FindOnlinePlayerByUid(request.RequesterUid);
            if (requester == null)
            {
                messenger.SendInfo(target, "Requester is no longer online.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
                messenger.SendGeneral(target, "Requester is no longer online.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            StartTeleportWarmup(requester, target);

            return TextCommandResult.Success();
        }

        private void StartTeleportWarmup(IServerPlayer requester, IServerPlayer target)
        {
            int effectiveWarmupSeconds = warmupResolver.Resolve(requester, teleportConfig);
            teleportWarmupService.Begin(new TeleportWarmupRequest
            {
                Player = requester,
                WarmupMessage = $"Teleporting in {effectiveWarmupSeconds} seconds. Do not move.",
                CountdownTemplate = "Teleporting in {0}...",
                CancelMessage = "Teleport cancelled because you moved.",
                SuccessIngameMessage = "Teleported.",
                BypassContext = "/tpa warmup",
                WarmupSeconds = effectiveWarmupSeconds,
                TickIntervalMs = teleportConfig.TickIntervalMs,
                CancelMoveThreshold = teleportConfig.CancelMoveThreshold,
                WarmupInfoChatType = (int)EnumChatType.CommandSuccess,
                WarmupGeneralGroupId = GlobalConstants.GeneralChatGroup,
                WarmupGeneralChatType = (int)EnumChatType.Notification,
                CancelInfoChatType = (int)EnumChatType.CommandSuccess,
                CancelGeneralChatType = (int)EnumChatType.Notification,
                ExecuteTeleport = () =>
                {
                    backLocationStore.RecordCurrentLocation(requester);
                    requester.Entity.TeleportToDouble(
                        target.Entity.Pos.X,
                        target.Entity.Pos.Y,
                        target.Entity.Pos.Z
                    );
                }
            });
        }

        private TextCommandResult TpDeny(TextCommandCallingArgs args)
        {
            var target = (IServerPlayer)args.Caller.Player;

            if (!requestStore.TryTakeFirst(target.PlayerUID, out TpaRequestRecord request))
            {
                messenger.SendInfo(target, "No pending teleport requests.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
                messenger.SendGeneral(target, "No pending teleport requests.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            api.Event.UnregisterCallback(request.ExpireListenerId);

            var requester = playerLookup.FindOnlinePlayerByUid(request.RequesterUid);
            messenger.SendInfo(requester, "Your teleport request was denied.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
            messenger.SendGeneral(requester, "Your teleport request was denied.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);

            return TextCommandResult.Success();
        }

        private TextCommandResult TpCancel(TextCommandCallingArgs args)
        {
            var caller = (IServerPlayer)args.Caller.Player;

            if (requestStore.TryCancelByRequester(caller.PlayerUID, out TpaRequestRecord request))
            {
                api.Event.UnregisterCallback(request.ExpireListenerId);
                messenger.SendInfo(caller, "Teleport request cancelled.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
                messenger.SendGeneral(caller, "Teleport request cancelled.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            messenger.SendInfo(caller, "You have no pending requests.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.Notification);
            messenger.SendGeneral(caller, "You have no pending requests.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);

            return TextCommandResult.Success();
        }

        private TextCommandResult TpaToggle(TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;
            string uid = player.PlayerUID;

            bool currentlyDisabled = IsTpaDisabled(uid);
            bool newState = !currentlyDisabled;

            SetTpaDisabled(uid, newState);

            if (newState)
            {
                var requests = requestStore.Clear(uid);
                foreach (TpaRequestRecord request in requests)
                {
                    api.Event.UnregisterCallback(request.ExpireListenerId);
                    var requester = playerLookup.FindOnlinePlayerByUid(request.RequesterUid);
                    messenger.SendInfo(requester, "Your teleport request was automatically denied.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
                    messenger.SendGeneral(requester, "Your teleport request was automatically denied.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
                }

                messenger.SendInfo(player, "TPA requests disabled.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
                messenger.SendGeneral(player, "TPA requests disabled.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
            }
            else
            {
                messenger.SendInfo(player, "TPA requests enabled.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
                messenger.SendGeneral(player, "TPA requests enabled.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
            }

            return TextCommandResult.Success();
        }
    }
}
