using System;
using System.Collections.Generic;

namespace FirstStepsTweaks.Services
{
    public sealed class DonatorFeaturePrivilegeResolver
    {
        private static readonly IReadOnlyCollection<string> ManagedPrivileges = new[]
        {
            "firststepstweaks.back"
        };

        public IReadOnlyCollection<string> GetManagedPrivileges()
        {
            return ManagedPrivileges;
        }

        public IReadOnlyCollection<string> ResolveGrantedPrivileges(string donatorPrivilege)
        {
            if (string.Equals(donatorPrivilege, "firststepstweaks.patron", StringComparison.OrdinalIgnoreCase)
                || string.Equals(donatorPrivilege, "firststepstweaks.founder", StringComparison.OrdinalIgnoreCase))
            {
                return ManagedPrivileges;
            }

            return Array.Empty<string>();
        }
    }
}
