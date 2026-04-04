using System;
using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Services;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Teleport
{
    public sealed class TeleportWarmupService : ITeleportWarmupService
    {
        private readonly ICoreServerAPI api;
        private readonly IPlayerMessenger messenger;
        private readonly IWorldCoordinateReader coordinateReader;

        public TeleportWarmupService(ICoreServerAPI api, IPlayerMessenger messenger)
            : this(api, messenger, new WorldCoordinateReader())
        {
        }

        public TeleportWarmupService(ICoreServerAPI api, IPlayerMessenger messenger, IWorldCoordinateReader coordinateReader)
        {
            this.api = api;
            this.messenger = messenger;
            this.coordinateReader = coordinateReader ?? new WorldCoordinateReader();
        }

        public void Begin(TeleportWarmupRequest request)
        {
            if (request?.Player?.Entity == null || request.ExecuteTeleport == null)
            {
                return;
            }

            if (request.AllowBypass && request.WarmupSeconds > 0 && TeleportBypass.HasBypass(request.Player))
            {
                TeleportBypass.NotifyBypassingCooldown(request.Player, request.BypassContext);
                request.ExecuteTeleport();
                messenger.SendIngameError(request.Player, "no_permission", request.SuccessIngameMessage);
                return;
            }

            var startPosition = coordinateReader.GetExactPosition(request.Player);
            if (startPosition == null)
            {
                return;
            }

            int secondsRemaining = request.WarmupSeconds;
            long listenerId = 0L;

            messenger.SendInfo(request.Player, request.WarmupMessage, GlobalConstants.InfoLogChatGroup, request.WarmupInfoChatType);
            messenger.SendGeneral(request.Player, request.WarmupMessage, request.WarmupGeneralGroupId, request.WarmupGeneralChatType);

            listenerId = api.Event.RegisterGameTickListener(_ =>
            {
                if (request.Player?.Entity == null)
                {
                    api.Event.UnregisterGameTickListener(listenerId);
                    return;
                }

                var currentPosition = coordinateReader.GetExactPosition(request.Player);
                if (currentPosition == null)
                {
                    api.Event.UnregisterGameTickListener(listenerId);
                    return;
                }

                double dx = Math.Abs(currentPosition.X - startPosition.X);
                double dy = Math.Abs(currentPosition.Y - startPosition.Y);
                double dz = Math.Abs(currentPosition.Z - startPosition.Z);

                if (dx > request.CancelMoveThreshold || dy > request.CancelMoveThreshold || dz > request.CancelMoveThreshold)
                {
                    messenger.SendInfo(request.Player, request.CancelMessage, GlobalConstants.InfoLogChatGroup, request.CancelInfoChatType);
                    messenger.SendGeneral(request.Player, request.CancelMessage, request.WarmupGeneralGroupId, request.CancelGeneralChatType);
                    api.Event.UnregisterGameTickListener(listenerId);
                    return;
                }

                if (secondsRemaining > 0)
                {
                    messenger.SendIngameError(request.Player, "no_permission", string.Format(request.CountdownTemplate, secondsRemaining));
                    secondsRemaining--;
                    return;
                }

                request.ExecuteTeleport();
                messenger.SendIngameError(request.Player, "no_permission", request.SuccessIngameMessage);
                api.Event.UnregisterGameTickListener(listenerId);
            }, request.TickIntervalMs);
        }
    }
}
