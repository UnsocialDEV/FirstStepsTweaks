using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public static class SpawnCommands
    {
        private const string SpawnKey = "fst_spawnpos";

        public static void Register(ICoreServerAPI api)
        {
            api.ChatCommands
                .Create("setspawn")
                .WithDescription("Sets server spawn to your current location")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(args => SetSpawn(api, args));

            api.ChatCommands
                .Create("spawn")
                .WithDescription("Teleport to server spawn")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => Spawn(api, args));
        }

        private static TextCommandResult SetSpawn(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;

            var pos = player.Entity.Pos;

            double[] spawnData =
            {
                pos.X,
                pos.Y,
                pos.Z
            };

            api.WorldManager.SaveGame.StoreData(SpawnKey, spawnData);

            player.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "Spawn position set.",
                EnumChatType.CommandSuccess
            );

            return TextCommandResult.Success();
        }

        private static TextCommandResult Spawn(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;

            double[] spawnData = api.WorldManager.SaveGame.GetData<double[]>(SpawnKey);

            if (spawnData == null || spawnData.Length != 3)
            {
                player.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    "Spawn has not been set yet.",
                    EnumChatType.CommandError
                );
                return TextCommandResult.Success();
            }

            double targetX = spawnData[0];
            double targetY = spawnData[1];
            double targetZ = spawnData[2];

            double startX = player.Entity.Pos.X;
            double startY = player.Entity.Pos.Y;
            double startZ = player.Entity.Pos.Z;

            int secondsRemaining = 10;
            long listenerId = 0;

            listenerId = api.Event.RegisterGameTickListener((dt) =>
            {
                if (player?.Entity == null)
                {
                    api.Event.UnregisterGameTickListener(listenerId);
                    return;
                }

                // Cancel if moved
                double dx = Math.Abs(player.Entity.Pos.X - startX);
                double dy = Math.Abs(player.Entity.Pos.Y - startY);
                double dz = Math.Abs(player.Entity.Pos.Z - startZ);

                if (dx > 0.1 || dy > 0.1 || dz > 0.1)
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
                        $"Teleporting to spawn in {secondsRemaining}...",
                        EnumChatType.Notification
                    );
                    secondsRemaining--;
                }
                else
                {
                    BackCommands.RecordCurrentLocation(player);
                    player.Entity.TeleportToDouble(targetX, targetY, targetZ);
                    player.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        "Teleported to spawn.",
                        EnumChatType.CommandSuccess
                    );
                    api.Event.UnregisterGameTickListener(listenerId);
                }

            }, 1000); // 1 second interval

            return TextCommandResult.Success();
        }
    }
}