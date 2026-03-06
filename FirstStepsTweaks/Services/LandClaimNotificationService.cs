using FirstStepsTweaks.Config;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public class LandClaimNotificationService
    {
        private readonly ICoreServerAPI api;
        private readonly LandClaimNotificationConfig config;
        private readonly Dictionary<string, ClaimSnapshot> playerClaimByUid = new Dictionary<string, ClaimSnapshot>();

        public LandClaimNotificationService(ICoreServerAPI api, FirstStepsTweaksConfig rootConfig)
        {
            this.api = api;
            config = rootConfig.LandClaims ?? new LandClaimNotificationConfig();

            api.Event.RegisterGameTickListener(OnTick, Math.Max(200, config.TickIntervalMs));
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
        }

        private void OnPlayerDisconnect(IServerPlayer player)
        {
            if (player == null) return;
            playerClaimByUid.Remove(player.PlayerUID);
        }

        private void OnTick(float dt)
        {
            foreach (var onlinePlayer in api.World.AllOnlinePlayers)
            {
                if (!(onlinePlayer is IServerPlayer player) || player?.Entity == null) continue;

                ClaimSnapshot previousClaim = playerClaimByUid.TryGetValue(player.PlayerUID, out var snapshot) ? snapshot : ClaimSnapshot.None;
                ClaimSnapshot currentClaim = GetClaimAtPlayerPosition(player);

                if (previousClaim.Key == currentClaim.Key) continue;
                if (IsTransitionBetweenPlayersOwnClaims(previousClaim, currentClaim, player))
                {
                    playerClaimByUid[player.PlayerUID] = currentClaim;
                    continue;
                }

                if (previousClaim.Exists)
                {
                    Send(player, BuildMessage(config.ExitMessage, previousClaim, player));
                }

                if (currentClaim.Exists)
                {
                    Send(player, BuildMessage(config.EnterMessage, currentClaim, player));
                }

                if (currentClaim.Exists)
                {
                    playerClaimByUid[player.PlayerUID] = currentClaim;
                }
                else
                {
                    playerClaimByUid.Remove(player.PlayerUID);
                }
            }
        }

        private static bool IsTransitionBetweenPlayersOwnClaims(ClaimSnapshot previousClaim, ClaimSnapshot currentClaim, IServerPlayer player)
        {
            if (!previousClaim.Exists || !currentClaim.Exists || player == null) return false;
            return IsOwnedByPlayer(previousClaim, player) && IsOwnedByPlayer(currentClaim, player);
        }

        private static bool IsOwnedByPlayer(ClaimSnapshot claim, IServerPlayer player)
        {
            return claim.Exists
                && player != null
                && !string.IsNullOrWhiteSpace(claim.OwnerUid)
                && string.Equals(claim.OwnerUid, player.PlayerUID, StringComparison.OrdinalIgnoreCase);
        }

        private ClaimSnapshot GetClaimAtPlayerPosition(IServerPlayer player)
        {
            object claimsApi = api.World?.GetType().GetProperty("Claims", BindingFlags.Instance | BindingFlags.Public)?.GetValue(api.World);
            if (claimsApi == null) return ClaimSnapshot.None;

            var blockPos = player.Entity.Pos.AsBlockPos;
            object claim = ResolveClaim(claimsApi, blockPos);
            return claim == null ? ClaimSnapshot.None : ClaimSnapshot.FromClaim(claim);
        }

        private static object ResolveClaim(object claimsApi, BlockPos pos)
        {
            var methods = claimsApi.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);

            foreach (string methodName in new[] { "Get", "GetClaimsAt", "GetAt", "GetCurrentClaims" })
            {
                var method = methods.FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == 1 && IsPositionParameter(m.GetParameters()[0].ParameterType));
                if (method == null) continue;

                object result = method.Invoke(claimsApi, new object[] { pos });
                object claim = PickSingleClaim(result);
                if (claim != null) return claim;
            }

            return null;
        }

        private static bool IsPositionParameter(Type paramType)
        {
            return typeof(BlockPos).IsAssignableFrom(paramType)
                || paramType.Name.Contains("BlockPos", StringComparison.OrdinalIgnoreCase);
        }

        private static object PickSingleClaim(object result)
        {
            if (result == null) return null;
            if (result is string) return null;
            if (result is IEnumerable enumerable)
            {
                foreach (var entry in enumerable)
                {
                    if (entry != null) return entry;
                }

                return null;
            }

            return result;
        }

        private static string BuildMessage(string template, ClaimSnapshot claim, IServerPlayer player)
        {
            string ownerName = NormalizeOwnerName(claim.OwnerName);
            if (string.IsNullOrWhiteSpace(ownerName) || ownerName.Equals(player.PlayerUID, StringComparison.OrdinalIgnoreCase))
            {
                ownerName = "Unknown";
            }

            if (string.Equals(claim.OwnerUid, player.PlayerUID, StringComparison.OrdinalIgnoreCase))
            {
                ownerName = "your";
            }

            string claimName = string.IsNullOrWhiteSpace(claim.ClaimName) ? "Unnamed claim" : claim.ClaimName;

            return (template ?? string.Empty)
                .Replace("{owner}", ownerName)
                .Replace("{claim}", claimName)
                .Replace("{player}", player.PlayerName);
        }

        private static string NormalizeOwnerName(string ownerName)
        {
            if (string.IsNullOrWhiteSpace(ownerName)) return ownerName;

            const string playerPrefix = "Player ";
            string normalized = ownerName.Trim();
            if (normalized.StartsWith(playerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Substring(playerPrefix.Length).TrimStart();
            }

            return normalized;
        }

        private static void Send(IServerPlayer player, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            player.SendIngameError("no_permission", message);
        }

        private readonly struct ClaimSnapshot
        {
            public static ClaimSnapshot None => new ClaimSnapshot(null, null, null);

            public readonly string Key;
            public readonly string ClaimName;
            public readonly string OwnerUid;
            public readonly string OwnerName;

            public bool Exists => !string.IsNullOrWhiteSpace(Key);

            private ClaimSnapshot(string key, string claimName, string ownerUid, string ownerName = null)
            {
                Key = key;
                ClaimName = claimName;
                OwnerUid = ownerUid;
                OwnerName = ownerName;
            }

            public static ClaimSnapshot FromClaim(object claim)
            {
                string key = ReadStringOrNull(claim, "ClaimId", "Id", "ProtectionId", "LandClaimId");
                if (string.IsNullOrWhiteSpace(key))
                {
                    string area = ReadAreaFingerprint(claim);
                    key = string.IsNullOrWhiteSpace(area) ? claim.GetHashCode().ToString(CultureInfo.InvariantCulture) : area;
                }

                string claimName = ReadStringOrNull(claim, "Name", "ClaimName", "Description", "Label");
                string ownerUid = ReadStringOrNull(claim, "OwnedByPlayerUid", "OwnerUid", "OwnerPlayerUid", "PlayerUid", "Uid");
                string ownerName = ReadStringOrNull(claim, "OwnedByPlayerName", "OwnerName", "OwnerPlayerName", "LastKnownOwnerName", "PlayerName");

                return new ClaimSnapshot(key, claimName, ownerUid, ownerName);
            }

            private static string ReadAreaFingerprint(object claim)
            {
                object[] areas = ReadObjectArray(claim, "Areas");
                if (areas == null || areas.Length == 0) return null;

                var parts = new List<string>();
                foreach (var area in areas)
                {
                    if (area == null) continue;
                    string minX = ReadStringOrNull(area, "MinX", "X1");
                    string minY = ReadStringOrNull(area, "MinY", "Y1");
                    string minZ = ReadStringOrNull(area, "MinZ", "Z1");
                    string maxX = ReadStringOrNull(area, "MaxX", "X2");
                    string maxY = ReadStringOrNull(area, "MaxY", "Y2");
                    string maxZ = ReadStringOrNull(area, "MaxZ", "Z2");

                    parts.Add($"{minX},{minY},{minZ}-{maxX},{maxY},{maxZ}");
                }

                return parts.Count == 0 ? null : string.Join("|", parts);
            }

            private static object[] ReadObjectArray(object obj, params string[] names)
            {
                object value = ReadObjectOrNull(obj, names);
                if (value is IEnumerable enumerable)
                {
                    return enumerable.Cast<object>().ToArray();
                }

                return null;
            }

            private static string ReadStringOrNull(object obj, params string[] names)
            {
                object value = ReadObjectOrNull(obj, names);
                return value?.ToString();
            }

            private static object ReadObjectOrNull(object obj, params string[] names)
            {
                if (obj == null || names == null || names.Length == 0) return null;

                var type = obj.GetType();
                foreach (string name in names)
                {
                    var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (property != null) return property.GetValue(obj);

                    var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                    if (field != null) return field.GetValue(obj);
                }

                return null;
            }
        }
    }
}
