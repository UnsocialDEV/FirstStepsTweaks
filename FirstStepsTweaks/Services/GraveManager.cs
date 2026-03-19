using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public class GraveManager : IGraveRepository
    {
        private const string SaveKey = "fst_gravestones.v2";
        private const string LegacySaveKey = "fst_gravestones.v1";

        private readonly ICoreServerAPI api;
        private readonly object syncRoot = new object();
        private readonly Dictionary<string, GraveData> gravesById =
            new Dictionary<string, GraveData>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> graveIdByPosition =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public GraveManager(ICoreServerAPI api)
        {
            this.api = api;
            Load();
        }

        public static string BuildPositionKey(BlockPos pos)
        {
            return BuildPositionKey(pos.dimension, pos.X, pos.Y, pos.Z);
        }

        public static string BuildPositionKey(int dimension, int x, int y, int z)
        {
            return $"{dimension}:{x}:{y}:{z}";
        }

        public List<GraveData> GetAll()
        {
            lock (syncRoot)
            {
                return gravesById.Values
                    .Select(Clone)
                    .OrderBy(grave => grave.CreatedUnixMs)
                    .ToList();
            }
        }

        public bool TryGetById(string graveId, out GraveData grave)
        {
            grave = null;
            if (string.IsNullOrWhiteSpace(graveId))
            {
                return false;
            }

            lock (syncRoot)
            {
                if (!gravesById.TryGetValue(graveId, out GraveData found) || found == null)
                {
                    return false;
                }

                grave = Clone(found);
                return true;
            }
        }

        public bool TryGetByPosition(BlockPos pos, out GraveData grave)
        {
            grave = null;
            if (pos == null)
            {
                return false;
            }

            lock (syncRoot)
            {
                string posKey = BuildPositionKey(pos);
                if (!graveIdByPosition.TryGetValue(posKey, out string graveId))
                {
                    return false;
                }

                if (!gravesById.TryGetValue(graveId, out GraveData found) || found == null)
                {
                    graveIdByPosition.Remove(posKey);
                    return false;
                }

                grave = Clone(found);
                return true;
            }
        }

        public bool Upsert(GraveData grave)
        {
            if (grave == null || string.IsNullOrWhiteSpace(grave.GraveId))
            {
                return false;
            }

            lock (syncRoot)
            {
                if (gravesById.TryGetValue(grave.GraveId, out GraveData existing) && existing != null)
                {
                    graveIdByPosition.Remove(BuildPositionKey(existing.Dimension, existing.X, existing.Y, existing.Z));
                }

                GraveData stored = Clone(grave);
                gravesById[stored.GraveId] = stored;
                graveIdByPosition[BuildPositionKey(stored.Dimension, stored.X, stored.Y, stored.Z)] = stored.GraveId;

                SaveLocked();
                return true;
            }
        }

        public bool Remove(string graveId, out GraveData removed)
        {
            removed = null;
            if (string.IsNullOrWhiteSpace(graveId))
            {
                return false;
            }

            lock (syncRoot)
            {
                if (!gravesById.TryGetValue(graveId, out GraveData existing) || existing == null)
                {
                    return false;
                }

                gravesById.Remove(graveId);
                graveIdByPosition.Remove(BuildPositionKey(existing.Dimension, existing.X, existing.Y, existing.Z));
                removed = Clone(existing);

                SaveLocked();
                return true;
            }
        }

        public void Save()
        {
            lock (syncRoot)
            {
                SaveLocked();
            }
        }
        private void Load()
        {
            lock (syncRoot)
            {
                gravesById.Clear();
                graveIdByPosition.Clear();

                if (TryLoadStoreFromKey(SaveKey, logFailures: true, out GraveStore currentStore))
                {
                    PopulateFromStore(currentStore);
                    return;
                }

                if (TryLoadStoreFromKey(LegacySaveKey, logFailures: false, out GraveStore legacyStore))
                {
                    PopulateFromStore(legacyStore);
                    SaveLocked();
                    api.Logger.Notification($"[FirstStepsTweaks] Migrated gravestone data from '{LegacySaveKey}' to '{SaveKey}'.");
                }
            }
        }
        private bool TryLoadStoreFromKey(string key, bool logFailures, out GraveStore store)
        {
            store = null;

            if (TryLoadRawBytesStore(key, out store))
            {
                return true;
            }

            if (TryLoadProtoStore(key, out store))
            {
                return true;
            }

            if (logFailures)
            {
                api.Logger.Warning($"[FirstStepsTweaks] Gravestone save key '{key}' contains invalid or unsupported data. Ignoring entry.");

                if (string.Equals(key, SaveKey, StringComparison.OrdinalIgnoreCase))
                {
                    api.WorldManager.SaveGame.StoreData(SaveKey, Array.Empty<byte>());
                }
            }

            return false;
        }

        private bool TryLoadRawBytesStore(string key, out GraveStore store)
        {
            store = null;

            if (!TryReadRawBytes(key, out byte[] raw))
            {
                return false;
            }

            if (raw == null || raw.Length == 0)
            {
                return false;
            }

            return TryDeserializeJsonStore(raw, out store);
        }

        private bool TryReadRawBytes(string key, out byte[] raw)
        {
            raw = null;

            try
            {
                raw = api.WorldManager.SaveGame.GetData(key);
                if (raw != null && raw.Length > 0)
                {
                    return true;
                }
            }
            catch
            {
                // ignored, try alternate access pattern below
            }

            try
            {
                raw = api.WorldManager.SaveGame.GetData<byte[]>(key);
                return raw != null && raw.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryDeserializeJsonStore(byte[] raw, out GraveStore store)
        {
            store = null;

            try
            {
                store = JsonSerializer.Deserialize<GraveStore>(raw);
                return store != null;
            }
            catch
            {
                return false;
            }
        }

        private bool TryLoadProtoStore(string key, out GraveStore store)
        {
            store = null;

            try
            {
                store = api.WorldManager.SaveGame.GetData<GraveStore>(key);
                return store != null;
            }
            catch
            {
                return false;
            }
        }

        private void PopulateFromStore(GraveStore store)
        {
            if (store?.Graves == null)
            {
                return;
            }

            foreach (GraveData grave in store.Graves)
            {
                if (grave == null || string.IsNullOrWhiteSpace(grave.GraveId))
                {
                    continue;
                }

                GraveData stored = Clone(grave);
                gravesById[stored.GraveId] = stored;
                graveIdByPosition[BuildPositionKey(stored.Dimension, stored.X, stored.Y, stored.Z)] = stored.GraveId;
            }
        }

        private void SaveLocked()
        {
            var store = new GraveStore
            {
                Graves = gravesById.Values.Select(Clone).ToList()
            };

            byte[] raw = JsonSerializer.SerializeToUtf8Bytes(store);
            api.WorldManager.SaveGame.StoreData(SaveKey, raw);
        }

        private static GraveData Clone(GraveData source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new GraveData
            {
                GraveId = source.GraveId,
                OwnerUid = source.OwnerUid,
                OwnerName = source.OwnerName,
                X = source.X,
                Y = source.Y,
                Z = source.Z,
                Dimension = source.Dimension,
                CreatedUnixMs = source.CreatedUnixMs,
                ProtectionEndsUnixMs = source.ProtectionEndsUnixMs,
                CreatedTotalDays = source.CreatedTotalDays,
                Inventories = new List<GraveInventorySnapshot>()
            };

            if (source.Inventories == null)
            {
                return clone;
            }

            foreach (GraveInventorySnapshot inventory in source.Inventories)
            {
                if (inventory == null)
                {
                    continue;
                }

                var inventoryClone = new GraveInventorySnapshot
                {
                    InventoryClassName = inventory.InventoryClassName,
                    InventoryId = inventory.InventoryId,
                    Slots = new List<GraveSlotSnapshot>()
                };

                if (inventory.Slots != null)
                {
                    foreach (GraveSlotSnapshot slot in inventory.Slots)
                    {
                        if (slot == null)
                        {
                            continue;
                        }

                        inventoryClone.Slots.Add(new GraveSlotSnapshot
                        {
                            SlotId = slot.SlotId,
                            StackBytes = slot.StackBytes == null
                                ? Array.Empty<byte>()
                                : (byte[])slot.StackBytes.Clone()
                        });
                    }
                }

                clone.Inventories.Add(inventoryClone);
            }

            return clone;
        }
    }
}




