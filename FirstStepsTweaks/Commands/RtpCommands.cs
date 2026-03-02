using System;
using System.Collections.Generic;
using FirstStepsTweaks.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public static class RtpCommands
    {
        private static readonly Dictionary<string, long> LastRtpByPlayerUid = new Dictionary<string, long>();
        private static readonly Random Random = new Random();

        private static TeleportConfig teleportConfig = new TeleportConfig();
        private static RtpConfig rtpConfig = new RtpConfig();

        public static void Register(ICoreServerAPI api, FirstStepsTweaksConfig config)
        {
            teleportConfig = config?.Teleport ?? new TeleportConfig();
            rtpConfig = config?.Rtp ?? new RtpConfig();

            api.ChatCommands
                .Create("rtp")
                .WithDescription("Teleport to a random location")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => RandomTeleport(api, args));
        }

        private static TextCommandResult RandomTeleport(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (rtpConfig.CooldownSeconds > 0 && LastRtpByPlayerUid.TryGetValue(player.PlayerUID, out long lastRtpMs))
            {
                long remainingMs = (rtpConfig.CooldownSeconds * 1000L) - (nowMs - lastRtpMs);
                if (remainingMs > 0)
                {
                    int remainingSeconds = (int)Math.Ceiling(remainingMs / 1000d);
                    player.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        $"You must wait {remainingSeconds}s before using /rtp again.",
                        EnumChatType.CommandError
                    );
                    return TextCommandResult.Success();
                }
            }

            Vec3d destination = FindDestination(api, player);
            if (destination == null)
            {
                player.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    "Failed to find a safe random destination. Try again.",
                    EnumChatType.CommandError
                );
                return TextCommandResult.Success();
            }

            if (rtpConfig.UseWarmup && teleportConfig.WarmupSeconds > 0)
            {
                StartWarmupTeleport(api, player, destination);
            }
            else
            {
                BackCommands.RecordCurrentLocation(player);
                player.Entity.TeleportToDouble(destination.X, destination.Y, destination.Z);
                player.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    "Teleported to a random location.",
                    EnumChatType.CommandSuccess
                );
                LastRtpByPlayerUid[player.PlayerUID] = nowMs;
            }

            return TextCommandResult.Success();
        }

        private static Vec3d FindDestination(ICoreServerAPI api, IServerPlayer player)
        {
            int attempts = Math.Max(1, rtpConfig.MaxAttempts);
            int minRadius = Math.Max(0, rtpConfig.MinRadius);
            int maxRadius = Math.Max(minRadius, rtpConfig.MaxRadius);
            int horizontalChecksPerAttempt = 4;

            double centerX = rtpConfig.UsePlayerPositionAsCenter ? player.Entity.Pos.X : 0;
            double centerZ = rtpConfig.UsePlayerPositionAsCenter ? player.Entity.Pos.Z : 0;

            for (int i = 0; i < attempts; i++)
            {
                double angle = Random.NextDouble() * Math.PI * 2;
                double distance = minRadius + (Random.NextDouble() * (maxRadius - minRadius));

                int baseX = (int)Math.Round(centerX + Math.Cos(angle) * distance);
                int baseZ = (int)Math.Round(centerZ + Math.Sin(angle) * distance);

                for (int sample = 0; sample < horizontalChecksPerAttempt; sample++)
                {
                    // Try a tiny local spread around the sampled point so one tree/cave column does not waste the whole attempt.
                    int x = baseX + Random.Next(-4, 5);
                    int z = baseZ + Random.Next(-4, 5);
                    Vec3d safeDestination = FindSafeDestinationInColumn(api, x, z);
                    if (safeDestination != null)
                    {
                        return safeDestination;
                    }
                }
            }

            return null;
        }

        private static Vec3d FindSafeDestinationInColumn(ICoreServerAPI api, int x, int z)
        {
            int terrainHeight = api.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos(x, 0, z));
            if (terrainHeight <= 1)
            {
                return null;
            }

            // Terrain map height can hit treetops/leaves. Scan downward to find a true walkable surface.
            int scanStartY = terrainHeight + 6;
            int scanEndY = Math.Max(2, terrainHeight - 20);

            for (int y = scanStartY; y >= scanEndY; y--)
            {
                BlockPos feetPos = new BlockPos(x, y, z);
                BlockPos headPos = new BlockPos(x, y + 1, z);
                BlockPos groundPos = new BlockPos(x, y - 1, z);

                Block feetBlock = api.World.BlockAccessor.GetBlock(feetPos);
                Block headBlock = api.World.BlockAccessor.GetBlock(headPos);
                Block groundBlock = api.World.BlockAccessor.GetBlock(groundPos);

                if (feetBlock == null || headBlock == null || groundBlock == null) continue;
                if (!IsPassableTeleportSpace(feetBlock) || !IsPassableTeleportSpace(headBlock)) continue;
                if (!IsSafeTeleportGround(groundBlock)) continue;

                return new Vec3d(x + 0.5, y, z + 0.5);
            }

            return null;
        }

        private static bool IsPassableTeleportSpace(Block block)
        {
            // Allow air and highly-replaceable non-solid blocks (tallgrass, flowers, etc.) as valid player space.
            return block.BlockId == 0 || block.Replaceable >= 6000;
        }

        private static bool IsSafeTeleportGround(Block block)
        {
            // Ground must exist and be reasonably solid; exclude highly-replaceable blocks (flora/snow layers).
            return block.BlockId != 0 && block.Replaceable < 6000;
        }

        private static void StartWarmupTeleport(ICoreServerAPI api, IServerPlayer player, Vec3d destination)
        {
            double startX = player.Entity.Pos.X;
            double startY = player.Entity.Pos.Y;
            double startZ = player.Entity.Pos.Z;
            int secondsRemaining = teleportConfig.WarmupSeconds;
            long listenerId = 0;

            player.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                $"Teleporting to a random location in {teleportConfig.WarmupSeconds} seconds. Do not move.",
                EnumChatType.CommandSuccess
            );

            listenerId = api.Event.RegisterGameTickListener((dt) =>
            {
                if (player?.Entity == null)
                {
                    api.Event.UnregisterGameTickListener(listenerId);
                    return;
                }

                double dx = Math.Abs(player.Entity.Pos.X - startX);
                double dy = Math.Abs(player.Entity.Pos.Y - startY);
                double dz = Math.Abs(player.Entity.Pos.Z - startZ);

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
                    player.SendIngameError("no_permission", $"Teleporting in {secondsRemaining}...");
                    secondsRemaining--;
                    return;
                }

                BackCommands.RecordCurrentLocation(player);
                player.Entity.TeleportToDouble(destination.X, destination.Y, destination.Z);
                LastRtpByPlayerUid[player.PlayerUID] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                player.SendIngameError("no_permission", "Teleported to a random location.");
                api.Event.UnregisterGameTickListener(listenerId);
            }, teleportConfig.TickIntervalMs);
        }
    }
}
