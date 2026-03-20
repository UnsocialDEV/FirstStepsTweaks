using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.LandClaims
{
    public sealed class ReflectionLandClaimAccessor : ILandClaimAccessor
    {
        private readonly ICoreServerAPI api;

        public ReflectionLandClaimAccessor(ICoreServerAPI api)
        {
            this.api = api;
        }

        public LandClaimInfo GetClaimAt(BlockPos pos)
        {
            if (pos == null)
            {
                return LandClaimInfo.None;
            }

            object claimsApi = api.World?.GetType().GetProperty("Claims", BindingFlags.Instance | BindingFlags.Public)?.GetValue(api.World);
            if (claimsApi == null)
            {
                return LandClaimInfo.None;
            }

            object claim = ResolveClaim(claimsApi, pos);
            return claim == null ? LandClaimInfo.None : FromClaim(claim);
        }

        private static object ResolveClaim(object claimsApi, BlockPos pos)
        {
            MethodInfo[] methods = claimsApi.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);

            foreach (string methodName in new[] { "Get", "GetClaimsAt", "GetAt", "GetCurrentClaims" })
            {
                MethodInfo method = methods.FirstOrDefault(m =>
                    m.Name == methodName
                    && m.GetParameters().Length == 1
                    && IsPositionParameter(m.GetParameters()[0].ParameterType));

                if (method == null)
                {
                    continue;
                }

                object result = method.Invoke(claimsApi, new object[] { pos });
                object claim = PickSingleClaim(result);
                if (claim != null)
                {
                    return claim;
                }
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
            if (result == null || result is string)
            {
                return null;
            }

            if (result is IEnumerable enumerable)
            {
                foreach (object entry in enumerable)
                {
                    if (entry != null)
                    {
                        return entry;
                    }
                }

                return null;
            }

            return result;
        }

        private static LandClaimInfo FromClaim(object claim)
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
            Cuboidi[] areas = ReadClaimAreas(claim);
            return new LandClaimInfo(key, claimName, ownerUid, ownerName, areas);
        }

        private static string ReadAreaFingerprint(object claim)
        {
            object[] areas = ReadObjectArray(claim, "Areas");
            if (areas == null || areas.Length == 0)
            {
                return null;
            }

            return string.Join("|", areas
                .Where(area => area != null)
                .Select(area =>
                {
                    string minX = ReadStringOrNull(area, "MinX", "X1");
                    string minY = ReadStringOrNull(area, "MinY", "Y1");
                    string minZ = ReadStringOrNull(area, "MinZ", "Z1");
                    string maxX = ReadStringOrNull(area, "MaxX", "X2");
                    string maxY = ReadStringOrNull(area, "MaxY", "Y2");
                    string maxZ = ReadStringOrNull(area, "MaxZ", "Z2");
                    return $"{minX},{minY},{minZ}-{maxX},{maxY},{maxZ}";
                }));
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
            if (obj == null || names == null || names.Length == 0)
            {
                return null;
            }

            Type type = obj.GetType();
            foreach (string name in names)
            {
                PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (property != null)
                {
                    return property.GetValue(obj);
                }

                FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    return field.GetValue(obj);
                }
            }

            return null;
        }

        private static Cuboidi[] ReadClaimAreas(object claim)
        {
            if (!(ReadObjectOrNull(claim, "Areas") is IEnumerable enumerable))
            {
                return Array.Empty<Cuboidi>();
            }

            var areas = new List<Cuboidi>();
            foreach (object entry in enumerable)
            {
                if (TryReadArea(entry, out Cuboidi area))
                {
                    areas.Add(area);
                }
            }

            return areas.Count == 0 ? Array.Empty<Cuboidi>() : areas.ToArray();
        }

        private static bool TryReadArea(object entry, out Cuboidi area)
        {
            area = null;
            if (entry == null)
            {
                return false;
            }

            if (entry is Cuboidi cuboid)
            {
                area = cuboid.Clone();
                return true;
            }

            int? minX = ReadIntOrNull(entry, "MinX", "X1");
            int? minY = ReadIntOrNull(entry, "MinY", "Y1");
            int? minZ = ReadIntOrNull(entry, "MinZ", "Z1");
            int? maxX = ReadIntOrNull(entry, "MaxX", "X2");
            int? maxY = ReadIntOrNull(entry, "MaxY", "Y2");
            int? maxZ = ReadIntOrNull(entry, "MaxZ", "Z2");

            if (!minX.HasValue || !minY.HasValue || !minZ.HasValue || !maxX.HasValue || !maxY.HasValue || !maxZ.HasValue)
            {
                return false;
            }

            area = new Cuboidi
            {
                X1 = minX.Value,
                Y1 = minY.Value,
                Z1 = minZ.Value,
                X2 = maxX.Value,
                Y2 = maxY.Value,
                Z2 = maxZ.Value
            };

            return true;
        }

        private static int? ReadIntOrNull(object obj, params string[] names)
        {
            object value = ReadObjectOrNull(obj, names);
            if (value == null)
            {
                return null;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (value is IConvertible convertible)
            {
                try
                {
                    return convertible.ToInt32(CultureInfo.InvariantCulture);
                }
                catch
                {
                }
            }

            return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : (int?)null;
        }
    }
}
