using System;
using System.Reflection;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Players
{
    public sealed class PlayerDefaultRoleResetter : IPlayerDefaultRoleResetter
    {
        private const string FallbackDefaultRoleCode = "suplayer";
        private readonly ICoreServerAPI api;
        private readonly IPlayerRoleAssigner roleAssigner;

        public PlayerDefaultRoleResetter(ICoreServerAPI api, IPlayerRoleAssigner roleAssigner)
        {
            this.api = api;
            this.roleAssigner = roleAssigner;
        }

        public void Reset(IServerPlayer player)
        {
            string defaultRoleCode = GetDefaultRoleCode();
            if (string.IsNullOrWhiteSpace(defaultRoleCode))
            {
                return;
            }

            roleAssigner.Assign(player, defaultRoleCode);
        }

        public string GetDefaultRoleCode()
        {
            object permissionManager = api.Permissions;
            PropertyInfo defaultRoleCodeProperty = permissionManager
                .GetType()
                .GetProperty("DefaultRoleCode", BindingFlags.Public | BindingFlags.Instance);

            string defaultRoleCode = defaultRoleCodeProperty?.GetValue(permissionManager) as string;
            return string.IsNullOrWhiteSpace(defaultRoleCode)
                ? FallbackDefaultRoleCode
                : defaultRoleCode;
        }
    }
}
