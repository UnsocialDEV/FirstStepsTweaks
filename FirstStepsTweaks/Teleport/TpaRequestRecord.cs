namespace FirstStepsTweaks.Teleport
{
    public sealed class TpaRequestRecord
    {
        public string RequesterUid { get; set; } = string.Empty;
        public string RequesterName { get; set; } = string.Empty;
        public string TargetUid { get; set; } = string.Empty;
        public string TargetName { get; set; } = string.Empty;
        public TpaRequestDirection Direction { get; set; } = TpaRequestDirection.RequesterToTarget;
        public long CreatedSequence { get; set; }
        public long ExpireListenerId { get; set; }
    }
}
