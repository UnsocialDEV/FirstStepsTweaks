using System;
using System.Collections.Generic;
using System.Text.Json;

namespace FirstStepsTweaks.Teleport
{
    public sealed class HomeDataSerializer
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public byte[] Serialize(Dictionary<string, HomeLocation> homes)
        {
            return JsonSerializer.SerializeToUtf8Bytes(
                homes ?? new Dictionary<string, HomeLocation>(StringComparer.OrdinalIgnoreCase),
                SerializerOptions);
        }

        public Dictionary<string, HomeLocation> Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return new Dictionary<string, HomeLocation>(StringComparer.OrdinalIgnoreCase);
            }

            Dictionary<string, HomeLocation> homes = JsonSerializer.Deserialize<Dictionary<string, HomeLocation>>(data, SerializerOptions);
            if (homes == null)
            {
                return new Dictionary<string, HomeLocation>(StringComparer.OrdinalIgnoreCase);
            }

            return new Dictionary<string, HomeLocation>(homes, StringComparer.OrdinalIgnoreCase);
        }

        public bool TryDeserializeLegacy(byte[] data, out HomeLocation home)
        {
            home = null;
            if (data == null || data.Length != 24)
            {
                return false;
            }

            home = new HomeLocation(
                BitConverter.ToDouble(data, 0),
                BitConverter.ToDouble(data, 8),
                BitConverter.ToDouble(data, 16));

            return true;
        }
    }
}
