using System.Text.Json;

namespace FirstStepsTweaks.ChiselTransfer
{
    public class ChiselSerializationService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        public string Serialize(ChiselExportFile file)
        {
            return JsonSerializer.Serialize(file, JsonOptions);
        }

        public ChiselExportFile? Deserialize(string json)
        {
            return JsonSerializer.Deserialize<ChiselExportFile>(json, JsonOptions);
        }
    }
}
