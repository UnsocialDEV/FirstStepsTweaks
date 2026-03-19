namespace FirstStepsTweaks.Infrastructure.Teleport
{
    public interface ITeleportWarmupService
    {
        void Begin(TeleportWarmupRequest request);
    }
}
