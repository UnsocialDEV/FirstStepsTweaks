namespace FirstStepsTweaks.Teleport
{
    public sealed class TpaRequestRecord
    {
        public string RequesterUid { get; set; } = string.Empty;
        public string TargetUid { get; set; } = string.Empty;
        public long ExpireListenerId { get; set; }
    }
}
