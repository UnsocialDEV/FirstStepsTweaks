namespace FirstStepsTweaks.Config
{
    public class ChiselTransferConfig
    {
        public int MaxBlocksPerExport { get; set; } = 32768;
        public bool AllowOverwriteOnImportDefault { get; set; } = false;
        public string ExportFolderName { get; set; } = "Exported";
    }
}
