using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Players
{
    public sealed class PlayerRoleCodeReader : IPlayerRoleCodeReader
    {
        public string Read(IPlayer player)
        {
            if (player == null)
            {
                return null;
            }

            return ReadRoleCode(player);
        }

        public string Read(IServerPlayer player)
        {
            if (player == null)
            {
                return null;
            }

            return ReadRoleCode(player);
        }

        private static string ReadRoleCode(object player)
        {
            PropertyInfo roleCodeProperty = player.GetType().GetProperty("RoleCode", BindingFlags.Public | BindingFlags.Instance);
            if (roleCodeProperty?.GetValue(player) is string roleCode && !string.IsNullOrWhiteSpace(roleCode))
            {
                return roleCode;
            }

            PropertyInfo roleProperty = player.GetType().GetProperty("Role", BindingFlags.Public | BindingFlags.Instance);
            object role = roleProperty?.GetValue(player);
            if (role == null)
            {
                return null;
            }

            PropertyInfo nestedRoleCodeProperty = role.GetType().GetProperty("Code", BindingFlags.Public | BindingFlags.Instance)
                ?? role.GetType().GetProperty("RoleCode", BindingFlags.Public | BindingFlags.Instance);

            return nestedRoleCodeProperty?.GetValue(role) as string;
        }
    }
}
