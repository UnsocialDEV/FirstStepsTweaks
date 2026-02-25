using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public static class HomeCommands
    {
        private const string HomeKey = "fst_homepos";

        public static void Register(ICoreServerAPI api)
        {
            api.ChatCommands
                .Create("sethome")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => SetHome(api, args));

            api.ChatCommands
                .Create("home")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => Home(api, args));

            api.ChatCommands
                .Create("delhome")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(args => DelHome(api, args));
        }

        private static TextCommandResult SetHome(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;

            if (player.GetModdata(HomeKey) != null)
            {
                player.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    "You already have a home set. Use /delhome first.",
                    EnumChatType.CommandError
                );
                return TextCommandResult.Success();
            }

            var pos = player.Entity.Pos;

            byte[] data = new byte[24];
            BitConverter.GetBytes(pos.X).CopyTo(data, 0);
            BitConverter.GetBytes(pos.Y).CopyTo(data, 8);
            BitConverter.GetBytes(pos.Z).CopyTo(data, 16);

            player.SetModdata(HomeKey, data);

            player.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "Home set.",
                EnumChatType.CommandSuccess
            );

            return TextCommandResult.Success();
        }
        private static TextCommandResult Home(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;

            byte[] data = player.GetModdata(HomeKey);

            if (data == null || data.Length != 24)
            {
                player.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    "You do not have a valid home set.",
                    EnumChatType.CommandError
                );
                return TextCommandResult.Success();
            }

            double targetX = BitConverter.ToDouble(data, 0);
            double targetY = BitConverter.ToDouble(data, 8);
            double targetZ = BitConverter.ToDouble(data, 16);

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
                        $"Teleporting home in {secondsRemaining}...",
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
                        "Teleported home.",
                        EnumChatType.CommandSuccess
                    );

                    api.Event.UnregisterGameTickListener(listenerId);
                }

            }, 1000); // 1 second

            return TextCommandResult.Success();
        }

        private static TextCommandResult DelHome(ICoreServerAPI api, TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;

            if (player.GetModdata(HomeKey) == null)
            {
                player.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    "You do not have a home set.",
                    EnumChatType.CommandError
                );
                return TextCommandResult.Success();
            }

            player.SetModdata(HomeKey, null);

            player.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "Home deleted.",
                EnumChatType.Notification
            );

            return TextCommandResult.Success();
        }
    }
}