using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Discord
{
    public sealed class PendingDiscordLinkCodeStore : IPendingDiscordLinkCodeStore
    {
        private const string PendingLinkCodeDataKey = "fst_discord_linkcodes";
        private readonly ICoreServerAPI api;

        public PendingDiscordLinkCodeStore(ICoreServerAPI api)
        {
            this.api = api;
        }

        public IReadOnlyCollection<string> GetPendingCodes(DateTime nowUtc)
        {
            Dictionary<string, PendingDiscordLinkCodeRecord> pendingCodes = LoadActiveCodes(nowUtc);
            return pendingCodes.Keys.ToArray();
        }

        public IReadOnlyDictionary<string, PendingDiscordLinkCodeRecord> GetPendingCodeRecords(DateTime nowUtc)
        {
            return LoadActiveCodes(nowUtc);
        }

        public bool TryGetCode(string code, DateTime nowUtc, out PendingDiscordLinkCodeRecord record)
        {
            record = null;
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            Dictionary<string, PendingDiscordLinkCodeRecord> pendingCodes = LoadActiveCodes(nowUtc);
            return pendingCodes.TryGetValue(code.Trim(), out record);
        }

        public void SaveCode(string code, PendingDiscordLinkCodeRecord record)
        {
            if (string.IsNullOrWhiteSpace(code) || record == null || string.IsNullOrWhiteSpace(record.PlayerUid))
            {
                return;
            }

            Dictionary<string, PendingDiscordLinkCodeRecord> pendingCodes = LoadPendingCodes();
            pendingCodes[code.Trim()] = record;
            SavePendingCodes(pendingCodes);
        }

        public void RemoveCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            Dictionary<string, PendingDiscordLinkCodeRecord> pendingCodes = LoadPendingCodes();
            if (!pendingCodes.Remove(code.Trim()))
            {
                return;
            }

            SavePendingCodes(pendingCodes);
        }

        public void RemoveCodesForPlayer(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return;
            }

            Dictionary<string, PendingDiscordLinkCodeRecord> pendingCodes = LoadPendingCodes();
            string[] matchingCodes = pendingCodes
                .Where(entry => string.Equals(entry.Value?.PlayerUid, playerUid, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Key)
                .ToArray();

            if (matchingCodes.Length == 0)
            {
                return;
            }

            foreach (string code in matchingCodes)
            {
                pendingCodes.Remove(code);
            }

            SavePendingCodes(pendingCodes);
        }

        private Dictionary<string, PendingDiscordLinkCodeRecord> LoadActiveCodes(DateTime nowUtc)
        {
            Dictionary<string, PendingDiscordLinkCodeRecord> pendingCodes = LoadPendingCodes();
            string[] expiredCodes = pendingCodes
                .Where(entry => IsExpired(entry.Value, nowUtc))
                .Select(entry => entry.Key)
                .ToArray();

            if (expiredCodes.Length == 0)
            {
                return pendingCodes;
            }

            foreach (string code in expiredCodes)
            {
                pendingCodes.Remove(code);
            }

            SavePendingCodes(pendingCodes);
            return pendingCodes;
        }

        private Dictionary<string, PendingDiscordLinkCodeRecord> LoadPendingCodes()
        {
            Dictionary<string, string[]> rawPendingCodes = api.WorldManager.SaveGame.GetData<Dictionary<string, string[]>>(PendingLinkCodeDataKey)
                ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            var pendingCodes = new Dictionary<string, PendingDiscordLinkCodeRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string[]> entry in rawPendingCodes)
            {
                if (!TryConvertRecord(entry.Value, out PendingDiscordLinkCodeRecord record))
                {
                    continue;
                }

                pendingCodes[entry.Key] = record;
            }

            return pendingCodes;
        }

        private void SavePendingCodes(Dictionary<string, PendingDiscordLinkCodeRecord> pendingCodes)
        {
            var rawPendingCodes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, PendingDiscordLinkCodeRecord> entry in pendingCodes)
            {
                if (entry.Value == null || string.IsNullOrWhiteSpace(entry.Value.PlayerUid))
                {
                    continue;
                }

                rawPendingCodes[entry.Key] = new[]
                {
                    entry.Value.PlayerUid,
                    entry.Value.ExpiresAtUtcTicks.ToString()
                };
            }

            api.WorldManager.SaveGame.StoreData(PendingLinkCodeDataKey, rawPendingCodes);
        }

        private static bool IsExpired(PendingDiscordLinkCodeRecord record, DateTime nowUtc)
        {
            if (record == null || record.ExpiresAtUtcTicks <= 0)
            {
                return true;
            }

            return new DateTime(record.ExpiresAtUtcTicks, DateTimeKind.Utc) <= nowUtc;
        }

        private static bool TryConvertRecord(string[] rawRecord, out PendingDiscordLinkCodeRecord record)
        {
            record = null;
            if (rawRecord == null || rawRecord.Length != 2 || string.IsNullOrWhiteSpace(rawRecord[0]))
            {
                return false;
            }

            if (!long.TryParse(rawRecord[1], out long expiresAtUtcTicks))
            {
                return false;
            }

            record = new PendingDiscordLinkCodeRecord(rawRecord[0], expiresAtUtcTicks);
            return true;
        }
    }
}
