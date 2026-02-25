using System.Collections.Generic;
using FirstStepsTweaks.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public static class BackCommands
    {
        private static readonly Dictionary<string, Vec3d> LastPositionsByPlayerUid =
            new Dictionary<string, Vec3d>();

        private static TeleportConfig teleportConfig = new TeleportConfig();

        public static void Register(ICoreServerAPI api, FirstStepsTweaksConfig config)
        {
            teleportConfig = config?.Teleport ?? new TeleportConfig();

            api.ChatCommands
                .Create("back")
                .WithDescription("Teleport back to your last location")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => Back(api, args));
        }

        public static void RecordCurrentLocation(IServerPlayer player)
        {
            if (player?.Entity?.Pos == null)
            {
                return;
            }

            LastPositionsByPlayerUid[player.PlayerUID] = new Vec3d(player.Entity.Pos.X, player.Entity.Pos.Y, player.Entity.Pos.Z);
        }

        public static void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (!(entity is EntityPlayer entityPlayer))
            {
                return;
            }

            IServerPlayer player = entityPlayer.Player as IServerPlayer;
            if (player?.Entity?.Pos == null)
            {
                return;
            }

            LastPositionsByPlayerUid[player.PlayerUID] =
                new Vec3d(player.Entity.Pos.X, player.Entity.Pos.Y, player.Entity.Pos.Z);
        }

        private static TextCommandResult Back(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;

            if (!LastPositionsByPlayerUid.TryGetValue(player.PlayerUID, out Vec3d lastLocation))
            {
                player.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    "No previous location recorded.",
                    EnumChatType.CommandError
                );
                return TextCommandResult.Success();
            }

            double startX = player.Entity.Pos.X;
            double startY = player.Entity.Pos.Y;
            double startZ = player.Entity.Pos.Z;

            int secondsRemaining = teleportConfig.WarmupSeconds;
            long listenerId = 0;

            listenerId = api.Event.RegisterGameTickListener((dt) =>
            {
                if (player?.Entity == null)
                {
                    api.Event.UnregisterGameTickListener(listenerId);
                    return;
                }

                double dx = System.Math.Abs(player.Entity.Pos.X - startX);
                double dy = System.Math.Abs(player.Entity.Pos.Y - startY);
                double dz = System.Math.Abs(player.Entity.Pos.Z - startZ);

                if (dx > teleportConfig.CancelMoveThreshold || dy > teleportConfig.CancelMoveThreshold || dz > teleportConfig.CancelMoveThreshold)
                {
                    player.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        "Teleport cancelled because you moved.",
                        EnumChatType.CommandError
                    );

                    api.Event.UnregisterGameTickListener(listenerId);
                    return;
                }

                if (secondsRemaining > 0)
                {
                    player.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        $"Teleporting to your last location in {secondsRemaining}...",
                        EnumChatType.Notification
                    );

                    secondsRemaining--;
                }
                else
                {
                    Vec3d currentLocation =
                        new Vec3d(player.Entity.Pos.X, player.Entity.Pos.Y, player.Entity.Pos.Z);

                    player.Entity.TeleportToDouble(lastLocation.X, lastLocation.Y, lastLocation.Z);
                    LastPositionsByPlayerUid[player.PlayerUID] = currentLocation;

                    player.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        "Teleported to your last location.",
                        EnumChatType.CommandSuccess
                    );

                    api.Event.UnregisterGameTickListener(listenerId);
                }
            }, teleportConfig.TickIntervalMs);

            return TextCommandResult.Success();
        }
    }
}
