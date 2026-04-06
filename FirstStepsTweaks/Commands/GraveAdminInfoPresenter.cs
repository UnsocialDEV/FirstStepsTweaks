using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Services;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class GraveAdminInfoPresenter
    {
        private const string HeaderSeparator = "========================================";

        private readonly IGraveAdminGraveResolver graveResolver;
        private readonly System.Func<GraveData, bool> isPubliclyClaimable;
        private readonly IPlayerMessenger messenger;
        private readonly IWorldCoordinateReader coordinateReader;
        private readonly GraveAdminEntryFormatter entryFormatter;

        public GraveAdminInfoPresenter(
            IGraveAdminGraveResolver graveResolver,
            System.Func<GraveData, bool> isPubliclyClaimable,
            IPlayerMessenger messenger,
            IWorldCoordinateReader coordinateReader,
            GraveAdminEntryFormatter entryFormatter)
        {
            this.graveResolver = graveResolver;
            this.isPubliclyClaimable = isPubliclyClaimable ?? (_ => false);
            this.messenger = messenger;
            this.coordinateReader = coordinateReader ?? new WorldCoordinateReader();
            this.entryFormatter = entryFormatter;
        }

        public void ShowLookedAtGraveInfo(IServerPlayer caller)
        {
            if (caller == null)
            {
                return;
            }

            if (!graveResolver.TryResolveTargetedGraveId(caller, out string graveId, out string resolveMessage))
            {
                messenger.SendDual(caller, resolveMessage, (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return;
            }

            if (!graveResolver.TryGetActiveGrave(graveId, out GraveData grave) || grave == null)
            {
                messenger.SendDual(caller, $"Gravestone '{graveId}' was not found.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return;
            }

            double? distanceBlocks = ResolveDistanceBlocks(caller, grave);
            string claimState = isPubliclyClaimable(grave) ? "public" : "owner-only";
            string body = entryFormatter.Format(grave, claimState, distanceBlocks: distanceBlocks).TrimEnd();
            string message = $"Looked-at gravestone:{Environment.NewLine}{HeaderSeparator}{Environment.NewLine}{body}";

            messenger.SendInfo(caller, message, GlobalConstants.InfoLogChatGroup, (int)EnumChatType.Notification);
            messenger.SendGeneral(caller, "Looked-at gravestone details were sent to your Info log channel.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);
        }

        private double? ResolveDistanceBlocks(IServerPlayer caller, GraveData grave)
        {
            Vec3d currentPosition = coordinateReader.GetExactPosition(caller);
            int? currentDimension = coordinateReader.GetDimension(caller);
            if (currentPosition == null || !currentDimension.HasValue || currentDimension.Value != grave.Dimension)
            {
                return null;
            }

            return currentPosition.DistanceTo(new Vec3d(grave.X, grave.Y, grave.Z));
        }
    }
}
