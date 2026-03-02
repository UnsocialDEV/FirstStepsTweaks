using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.ChiselTransfer
{
    public class ChiselDataExtractor
    {
        private readonly ICoreServerAPI api;

        public ChiselDataExtractor(ICoreServerAPI api)
        {
            this.api = api;
        }

        public bool IsLikelyChiseledBlock(Block block, object? blockEntity)
        {
            if (block?.Code?.Path?.IndexOf("chisel", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (blockEntity == null)
            {
                return false;
            }

            string entityType = blockEntity.GetType().Name;
            if (entityType.IndexOf("chisel", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return TryCollectShapeData(blockEntity).Count > 0;
        }

        public Dictionary<string, string> TryCollectShapeData(object? blockEntity)
        {
            var collected = new Dictionary<string, string>(StringComparer.Ordinal);
            if (blockEntity == null)
            {
                return collected;
            }

            Type type = blockEntity.GetType();

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead) continue;
                if (!LooksLikeShapeProperty(property.Name)) continue;
                object value;
                try
                {
                    value = property.GetValue(blockEntity);
                }
                catch
                {
                    continue;
                }

                string encoded = EncodeValue(value);
                if (!string.IsNullOrEmpty(encoded))
                {
                    collected[$"prop:{property.Name}"] = encoded;
                }
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!LooksLikeShapeProperty(field.Name)) continue;
                object value;
                try
                {
                    value = field.GetValue(blockEntity);
                }
                catch
                {
                    continue;
                }

                string encoded = EncodeValue(value);
                if (!string.IsNullOrEmpty(encoded))
                {
                    collected[$"field:{field.Name}"] = encoded;
                }
            }

            return collected;
        }

        public void TryApplyShapeData(object? blockEntity, Dictionary<string, string>? shapeData)
        {
            if (blockEntity == null || shapeData == null || shapeData.Count == 0)
            {
                return;
            }

            Type type = blockEntity.GetType();

            foreach (var pair in shapeData)
            {
                string[] split = pair.Key.Split(':', 2);
                if (split.Length != 2) continue;

                object decoded;
                try
                {
                    decoded = DecodeValue(pair.Value);
                }
                catch
                {
                    continue;
                }

                if (split[0] == "prop")
                {
                    PropertyInfo? prop = type.GetProperty(split[1], BindingFlags.Instance | BindingFlags.Public);
                    if (prop != null && prop.CanWrite)
                    {
                        TryAssign(prop.PropertyType, decoded, v => prop.SetValue(blockEntity, v));
                    }
                }
                else if (split[0] == "field")
                {
                    FieldInfo? field = type.GetField(split[1], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        TryAssign(field.FieldType, decoded, v => field.SetValue(blockEntity, v));
                    }
                }
            }

            InvokeOptional(blockEntity, "MarkDirty");
            InvokeOptional(blockEntity, "RedrawAfterReceivingTreeAttributes");
        }

        private static bool LooksLikeShapeProperty(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return name.IndexOf("voxel", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("material", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("chisel", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("cuboid", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string EncodeValue(object value)
        {
            if (value == null) return string.Empty;

            if (value is byte[] bytes)
            {
                return "b64:" + Convert.ToBase64String(bytes);
            }

            if (value is Array arr)
            {
                var flat = new List<string>();
                foreach (object item in arr)
                {
                    flat.Add(item?.ToString() ?? string.Empty);
                }
                return "arr:" + string.Join(",", flat);
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                var flat = new List<string>();
                foreach (object item in enumerable)
                {
                    flat.Add(item?.ToString() ?? string.Empty);
                }
                return "enum:" + string.Join(",", flat);
            }

            return "str:" + value;
        }

        private static object DecodeValue(string encoded)
        {
            if (encoded.StartsWith("b64:", StringComparison.Ordinal))
            {
                return Convert.FromBase64String(encoded.Substring(4));
            }

            if (encoded.StartsWith("arr:", StringComparison.Ordinal) || encoded.StartsWith("enum:", StringComparison.Ordinal))
            {
                string payload = encoded.Substring(4);
                return payload.Split(',', StringSplitOptions.None);
            }

            if (encoded.StartsWith("str:", StringComparison.Ordinal))
            {
                return encoded.Substring(4);
            }

            return encoded;
        }

        private static void TryAssign(Type targetType, object decoded, Action<object> assign)
        {
            try
            {
                if (targetType == typeof(byte[]) && decoded is byte[] bytes)
                {
                    assign(bytes);
                    return;
                }

                if (targetType == typeof(string) && decoded is string str)
                {
                    assign(str);
                    return;
                }
            }
            catch
            {
            }
        }

        private static void InvokeOptional(object instance, string methodName)
        {
            try
            {
                MethodInfo? method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                method?.Invoke(instance, null);
            }
            catch
            {
            }
        }

        public object? GetBlockEntity(IBlockAccessor blockAccessor, BlockPos pos)
        {
            try
            {
                return blockAccessor.GetBlockEntity(pos);
            }
            catch (Exception ex)
            {
                api.Logger.Warning("[FirstStepsTweaks] Failed reading block entity at {0}: {1}", pos, ex.Message);
                return null;
            }
        }
    }
}
