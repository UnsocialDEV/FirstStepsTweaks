using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstStepsTweaks.Services
{
    public sealed class DonatorPrivilegeCatalog
    {
        private static readonly IReadOnlyList<DonatorPrivilegeDefinition> Definitions =
            new[]
            {
                new DonatorPrivilegeDefinition(DonatorTier.Founder, "Founder", "founder", "firststepstweaks.founder", "founder"),
                new DonatorPrivilegeDefinition(DonatorTier.Patron, "Patron", "patron", "firststepstweaks.patron", "patron"),
                new DonatorPrivilegeDefinition(DonatorTier.Sponsor, "Sponsor", "sponsor", "firststepstweaks.sponsor", "sponsor"),
                new DonatorPrivilegeDefinition(DonatorTier.Contributor, "Contributor", "contributor", "firststepstweaks.contributor", "contributor"),
                new DonatorPrivilegeDefinition(DonatorTier.Supporter, "Supporter", "supporter", "firststepstweaks.supporter", "supporter")
            };

        public IReadOnlyList<DonatorPrivilegeDefinition> GetAll()
        {
            return Definitions;
        }

        public IReadOnlyCollection<string> GetAllPrivileges()
        {
            return Definitions.Select(definition => definition.Privilege).ToArray();
        }

        public IReadOnlyCollection<string> GetAllRoleCodes()
        {
            return Definitions.Select(definition => definition.InGameRoleCode).ToArray();
        }

        public DonatorPrivilegeDefinition FindByPrivilege(string privilege)
        {
            return Definitions.FirstOrDefault(definition => string.Equals(definition.Privilege, privilege, StringComparison.OrdinalIgnoreCase));
        }
    }
}
