using System;
using System.Linq;
using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class LegacyDonatorPrivilegeCleaner
    {
        private readonly IPlayerPrivilegeReader privilegeReader;
        private readonly IPlayerPrivilegeMutator privilegeMutator;
        private readonly DonatorTierCatalog tierCatalog;

        public LegacyDonatorPrivilegeCleaner(
            IPlayerPrivilegeReader privilegeReader,
            IPlayerPrivilegeMutator privilegeMutator,
            DonatorTierCatalog tierCatalog)
        {
            this.privilegeReader = privilegeReader;
            this.privilegeMutator = privilegeMutator;
            this.tierCatalog = tierCatalog;
        }

        public bool ClearManagedPrivileges(IServerPlayer player)
        {
            if (player == null)
            {
                return false;
            }

            string[] currentPrivileges = tierCatalog.GetAllLegacyPrivileges()
                .Where(privilege => privilegeReader.HasPrivilege(player, privilege))
                .ToArray();

            foreach (string privilege in currentPrivileges)
            {
                privilegeMutator.Revoke(player, privilege);
            }

            return currentPrivileges.Length > 0;
        }
    }
}
