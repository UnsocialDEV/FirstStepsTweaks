using System;
using System.Collections.Generic;
using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.MathTools;

namespace FirstStepsTweaks.Services
{
    [Serializable]
    public class GraveData
    {
        public string GraveId { get; set; } = string.Empty;
        public string OwnerUid { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int Dimension { get; set; }
        public long CreatedUnixMs { get; set; }
        public long ProtectionEndsUnixMs { get; set; }
        public double CreatedTotalDays { get; set; }
        public List<PlayerInventorySnapshot> Inventories { get; set; } = new List<PlayerInventorySnapshot>();

        public BlockPos ToBlockPos()
        {
            return new BlockPos(X, Y, Z, Dimension);
        }
    }

    [Serializable]
    public class GraveStore
    {
        public List<GraveData> Graves { get; set; } = new List<GraveData>();
    }
}
