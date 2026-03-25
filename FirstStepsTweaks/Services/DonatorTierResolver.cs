using System;
using Vintagestory.API.Common;

namespace FirstStepsTweaks.Services
{
    public sealed class DonatorTierResolver
    {
        private readonly DonatorPrivilegeCatalog privilegeCatalog = new DonatorPrivilegeCatalog();

        public string ResolveLabel(IPlayer player)
        {
            if (player == null)
            {
                return null;
            }

            return ResolveLabel(player.HasPrivilege);
        }

        public string ResolveLabel(System.Func<string, bool> hasPrivilege)
        {
            foreach (DonatorPrivilegeDefinition definition in privilegeCatalog.GetAll())
            {
                if (hasPrivilege(definition.Privilege))
                {
                    return definition.Label;
                }
            }

            return null;
        }
    }
}
