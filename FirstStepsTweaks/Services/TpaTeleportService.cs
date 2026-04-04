using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class TpaTeleportService
    {
        private readonly TeleportConfig teleportConfig;
        private readonly IBackLocationStore backLocationStore;
        private readonly ITeleportWarmupService teleportWarmupService;
        private readonly IPlayerTeleporter playerTeleporter;
        private readonly PlayerTeleportWarmupResolver warmupResolver;
        private readonly IWorldCoordinateReader coordinateReader;

        public TpaTeleportService(
            FirstStepsTweaksConfig config,
            IBackLocationStore backLocationStore,
            ITeleportWarmupService teleportWarmupService,
            IPlayerTeleporter playerTeleporter,
            PlayerTeleportWarmupResolver warmupResolver)
            : this(config, backLocationStore, teleportWarmupService, playerTeleporter, warmupResolver, new WorldCoordinateReader())
        {
        }

        public TpaTeleportService(
            FirstStepsTweaksConfig config,
            IBackLocationStore backLocationStore,
            ITeleportWarmupService teleportWarmupService,
            IPlayerTeleporter playerTeleporter,
            PlayerTeleportWarmupResolver warmupResolver,
            IWorldCoordinateReader coordinateReader)
        {
            teleportConfig = config?.Teleport ?? new TeleportConfig();
            this.backLocationStore = backLocationStore;
            this.teleportWarmupService = teleportWarmupService;
            this.playerTeleporter = playerTeleporter;
            this.warmupResolver = warmupResolver;
            this.coordinateReader = coordinateReader ?? new WorldCoordinateReader();
        }

        public void BeginTeleport(TpaRequestRecord request, IServerPlayer requester, IServerPlayer target)
        {
            IServerPlayer movedPlayer = request.Direction == TpaRequestDirection.RequesterToTarget ? requester : target;
            IServerPlayer destinationPlayer = request.Direction == TpaRequestDirection.RequesterToTarget ? target : requester;

            if (coordinateReader.GetExactPosition(movedPlayer) == null || coordinateReader.GetExactPosition(destinationPlayer) == null)
            {
                return;
            }

            int effectiveWarmupSeconds = warmupResolver.Resolve(movedPlayer, teleportConfig);
            string destinationName = destinationPlayer.PlayerName;
            string commandName = request.Direction == TpaRequestDirection.RequesterToTarget ? "/tpa" : "/tpahere";

            teleportWarmupService.Begin(new TeleportWarmupRequest
            {
                Player = movedPlayer,
                WarmupMessage = $"Teleporting to {destinationName} in {effectiveWarmupSeconds} seconds. Do not move.",
                CountdownTemplate = $"Teleporting to {destinationName} in {{0}}...",
                CancelMessage = "Teleport cancelled because you moved.",
                SuccessIngameMessage = $"Teleported to {destinationName}.",
                BypassContext = $"{commandName} warmup",
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
                    Vec3d destinationPosition = coordinateReader.GetExactPosition(destinationPlayer);
                    if (destinationPosition == null)
                    {
                        return;
                    }

                    backLocationStore.RecordCurrentLocation(movedPlayer);
                    playerTeleporter.Teleport(movedPlayer, destinationPosition);
                }
            });
        }
    }
}
