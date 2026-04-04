using System;
using System.Collections.Generic;

namespace FirstStepsTweaks.Infrastructure.Players
{
    [Serializable]
    public sealed class PlayerInventorySnapshot
    {
        public string InventoryClassName { get; set; } = string.Empty;

        public string InventoryId { get; set; } = string.Empty;

        public List<PlayerInventorySlotSnapshot> Slots { get; set; } = new List<PlayerInventorySlotSnapshot>();
    }

    [Serializable]
    public sealed class PlayerInventorySlotSnapshot
    {
        public int SlotId { get; set; }

        public byte[] StackBytes { get; set; } = Array.Empty<byte>();
    }
}
