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
            MatchCollection matches = CandidateCodePattern.Matches(content.ToUpperInvariant());

            foreach (Match match in matches)
            {
                if (!match.Success || !knownCodes.Contains(match.Value))
                {
                    continue;
                }

                code = match.Value;
                return true;
            }

            return false;
        }

        public bool TryParseCandidateCode(string content, out string code)
        {
            code = null;
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            Match match = CandidateCodePattern.Match(content.ToUpperInvariant());
            if (!match.Success)
            {
                return false;
            }

            code = match.Value;
            return true;
        }
    }
}
