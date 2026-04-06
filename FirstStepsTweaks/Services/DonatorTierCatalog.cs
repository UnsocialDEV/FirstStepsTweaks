using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstStepsTweaks.Services
{
    public sealed class DonatorTierCatalog
    {
        private static readonly IReadOnlyList<DonatorTierDefinition> Definitions =
            new[]
            {
                new DonatorTierDefinition(DonatorTier.Founder, "Founder", "founder", "founder", "firststepstweaks.founder"),
                new DonatorTierDefinition(DonatorTier.Patron, "Patron", "patron", "patron", "firststepstweaks.patron"),
                new DonatorTierDefinition(DonatorTier.Sponsor, "Sponsor", "sponsor", "sponsor", "firststepstweaks.sponsor"),
                new DonatorTierDefinition(DonatorTier.Contributor, "Contributor", "contributor", "contributor", "firststepstweaks.contributor"),
                new DonatorTierDefinition(DonatorTier.Supporter, "Supporter", "supporter", "supporter", "firststepstweaks.supporter")
            };

        public IReadOnlyList<DonatorTierDefinition> GetAll()
        {
            return Definitions;
        }

        public IReadOnlyCollection<string> GetAllRoleCodes()
        {
            return Definitions.Select(definition => definition.RoleCode).ToArray();
        }

        public IReadOnlyCollection<string> GetAllLegacyPrivileges()
        {
            return Definitions.Select(definition => definition.LegacyPrivilege).ToArray();
        }

        public DonatorTierDefinition FindByRoleCode(string roleCode)
        {
            return Definitions.FirstOrDefault(definition =>
                string.Equals(definition.RoleCode, roleCode, StringComparison.OrdinalIgnoreCase));
        }

        public DonatorTierDefinition FindByLegacyPrivilege(string privilege)
        {
            return Definitions.FirstOrDefault(definition =>
                string.Equals(definition.LegacyPrivilege, privilege, StringComparison.OrdinalIgnoreCase));
        }
    }
}
