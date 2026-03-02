using System;
using System.IO;
using FirstStepsTweaks.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.ChiselTransfer
{
    public class ChiselFileStore
    {
        private readonly ICoreServerAPI api;
        private readonly ChiselTransferConfig config;

        public ChiselFileStore(ICoreServerAPI api, ChiselTransferConfig config)
        {
            this.api = api;
            this.config = config;
        }

        public string GetExportFolderPath()
        {
            string basePath = AppContext.BaseDirectory;
            string folder = config.ExportFolderName;
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = "Exported";
            }

            string fullPath = Path.Combine(basePath, "Mods", "FirstStepsTweaks", folder);
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }

        public string BuildSafePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("Filename is required.", nameof(fileName));
            }

            string sanitized = fileName.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalid, '_');
            }

            if (!sanitized.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                sanitized += ".json";
            }

            return Path.Combine(GetExportFolderPath(), sanitized);
        }

        public void Save(string fileName, string content)
        {
            string path = BuildSafePath(fileName);
            File.WriteAllText(path, content);
            api.Logger.Notification("[FirstStepsTweaks] Saved chisel export to {0}", path);
        }

        public string Load(string fileName)
        {
            string path = BuildSafePath(fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Chisel export file was not found.", path);
            }

            return File.ReadAllText(path);
        }
    }
}
