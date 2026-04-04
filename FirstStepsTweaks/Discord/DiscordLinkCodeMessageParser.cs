using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FirstStepsTweaks.Discord
{
    public sealed class DiscordLinkCodeMessageParser
    {
        private static readonly Regex CandidateCodePattern = new Regex(@"\b[A-Z0-9]{6}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public bool TryParsePendingCode(string content, IReadOnlyCollection<string> pendingCodes, out string code)
        {
            code = null;
            if (string.IsNullOrWhiteSpace(content) || pendingCodes == null || pendingCodes.Count == 0)
            {
                return false;
            }

            var knownCodes = new HashSet<string>(pendingCodes, StringComparer.OrdinalIgnoreCase);
            string candidate = content.Trim().ToUpperInvariant();
            if (!CandidateCodePattern.IsMatch(candidate) || candidate.Length != 6 || !knownCodes.Contains(candidate))
            {
                return false;
            }

            code = candidate;
            return true;
        }

        public bool TryParseCandidateCode(string content, out string code)
        {
            code = null;
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            string candidate = content.Trim().ToUpperInvariant();
            Match match = CandidateCodePattern.Match(candidate);
            if (!match.Success || match.Value.Length != candidate.Length)
            {
                return false;
            }

            code = match.Value;
            return true;
        }
    }
}
