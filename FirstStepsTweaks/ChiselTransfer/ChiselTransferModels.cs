using System;
using System.Collections.Generic;

namespace FirstStepsTweaks.ChiselTransfer
{
    public class ChiselExportFile
    {
        public int Version { get; set; } = 1;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public List<ChiselBlockRecord> Blocks { get; set; } = new List<ChiselBlockRecord>();
    }

    public class ChiselBlockRecord
    {
        public int RelX { get; set; }
        public int RelY { get; set; }
        public int RelZ { get; set; }
        public string BaseBlockCode { get; set; } = string.Empty;
        public Dictionary<string, string> ShapeData { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public class ChiselSelection
    {
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int Z1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
        public int Z2 { get; set; }

        public int MinX => Math.Min(X1, X2);
        public int MinY => Math.Min(Y1, Y2);
        public int MinZ => Math.Min(Z1, Z2);
        public int MaxX => Math.Max(X1, X2);
        public int MaxY => Math.Max(Y1, Y2);
        public int MaxZ => Math.Max(Z1, Z2);

        public int BlockCount => (MaxX - MinX + 1) * (MaxY - MinY + 1) * (MaxZ - MinZ + 1);
    }
}
