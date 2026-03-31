using System;
using System.Globalization;

namespace FirstStepsTweaks.Commands
{
    internal static class DebugCommandSupport
    {
        public static bool TryParseBoolean(string raw, out bool value)
        {
            if (bool.TryParse(raw, out value))
            {
                return true;
            }

            switch ((raw ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "1":
                case "yes":
                case "on":
                    value = true;
                    return true;
                case "0":
                case "no":
                case "off":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        public static bool TryParseLong(string raw, out long value)
        {
            return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseDouble(string raw, out double value)
        {
            return double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
        }

        public static string FormatVec3(double x, double y, double z)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{x:0.##}, {y:0.##}, {z:0.##}");
        }
    }
}
