using System;
using System.Collections;
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
            return new LandClaimInfo(key, claimName, ownerUid, ownerName);
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
    }
}
