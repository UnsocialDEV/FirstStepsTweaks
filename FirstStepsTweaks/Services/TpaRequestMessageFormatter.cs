using FirstStepsTweaks.Teleport;

namespace FirstStepsTweaks.Services
{
    public sealed class TpaRequestMessageFormatter
    {
        public string FormatSent(TpaRequestDirection direction, string targetName)
        {
            return direction == TpaRequestDirection.RequesterToTarget
                ? $"Teleport request sent to {targetName}. If accepted, you will teleport to them."
                : $"Teleport-here request sent to {targetName}. If accepted, they will teleport to you.";
        }

        public string FormatReceived(TpaRequestDirection direction, string requesterName)
        {
            return direction == TpaRequestDirection.RequesterToTarget
                ? $"{requesterName} wants to teleport to you. Use /tpaccept to accept or /tpadeny to deny."
                : $"{requesterName} wants you to teleport to them. Use /tpaccept to accept or /tpadeny to deny.";
        }

        public string FormatAcceptedForRequester(TpaRequestDirection direction, string targetName)
        {
            return $"{targetName} accepted your {GetCommandName(direction)} request.";
        }

        public string FormatAcceptedForTarget(TpaRequestDirection direction, string requesterName)
        {
            return $"You accepted {requesterName}'s {GetCommandName(direction)} request.";
        }

        public string FormatDeniedForRequester(TpaRequestDirection direction, string targetName)
        {
            return $"{targetName} denied your {GetCommandName(direction)} request.";
        }

        public string FormatDeniedForTarget(TpaRequestDirection direction, string requesterName)
        {
            return $"You denied {requesterName}'s {GetCommandName(direction)} request.";
        }

        public string FormatCancelledForRequester(TpaRequestDirection direction, string targetName)
        {
            return $"Cancelled your {GetCommandName(direction)} request to {targetName}.";
        }

        public string FormatCancelledForTarget(TpaRequestDirection direction, string requesterName)
        {
            return $"{requesterName} cancelled their {GetCommandName(direction)} request.";
        }

        public string FormatExpiredForRequester(TpaRequestDirection direction, string targetName)
        {
            return $"Your {GetCommandName(direction)} request to {targetName} expired.";
        }

        public string FormatExpiredForTarget(TpaRequestDirection direction, string requesterName)
        {
            return $"{requesterName}'s {GetCommandName(direction)} request expired.";
        }

        public string FormatAutoDeniedForRequester(TpaRequestDirection direction, string targetName)
        {
            return $"{targetName} is no longer accepting teleport requests. Your {GetCommandName(direction)} request was denied.";
        }

        private string GetCommandName(TpaRequestDirection direction)
        {
            return direction == TpaRequestDirection.RequesterToTarget ? "/tpa" : "/tpahere";
        }
    }
}
