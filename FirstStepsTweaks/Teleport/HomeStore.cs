using System;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Teleport
{
    public sealed class HomeStore
    {
        private const string HomeKey = "fst_homepos";

        public bool HasHome(IServerPlayer player)
        {
            return player?.GetModdata(HomeKey) != null;
        }

        public void SetHome(IServerPlayer player)
        {
            if (player?.Entity?.Pos == null)
            {
                return;
            }

            byte[] data = new byte[24];
            BitConverter.GetBytes(player.Entity.Pos.X).CopyTo(data, 0);
            BitConverter.GetBytes(player.Entity.Pos.Y).CopyTo(data, 8);
            BitConverter.GetBytes(player.Entity.Pos.Z).CopyTo(data, 16);
            player.SetModdata(HomeKey, data);
        }

        public bool TryGetHome(IServerPlayer player, out Vec3d position)
        {
            position = null;
            byte[] data = player?.GetModdata(HomeKey);
            if (data == null || data.Length != 24)
            {
                return false;
            }

            position = new Vec3d(
                BitConverter.ToDouble(data, 0),
                BitConverter.ToDouble(data, 8),
                BitConverter.ToDouble(data, 16)
            );

            return true;
        }

        public void ClearHome(IServerPlayer player)
        {
            player?.SetModdata(HomeKey, null);
        }
    }
}
