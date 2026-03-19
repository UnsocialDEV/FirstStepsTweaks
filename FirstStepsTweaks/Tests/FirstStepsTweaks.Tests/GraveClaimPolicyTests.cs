using FirstStepsTweaks.Gravestones;
using FirstStepsTweaks.Services;
using Xunit;

namespace FirstStepsTweaks.Tests
{
    public sealed class GraveClaimPolicyTests
    {
        [Fact]
        public void IsPubliclyClaimable_ReturnsFalse_WhenProtectionHasNotExpired()
        {
            var policy = new GraveClaimPolicy();
            var grave = new GraveData
            {
                ProtectionEndsUnixMs = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds()
            };

            Assert.False(policy.IsPubliclyClaimable(grave));
        }

        [Fact]
        public void IsPubliclyClaimable_ReturnsTrue_WhenProtectionHasExpired()
        {
            var policy = new GraveClaimPolicy();
            var grave = new GraveData
            {
                ProtectionEndsUnixMs = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds()
            };

            Assert.True(policy.IsPubliclyClaimable(grave));
        }
    }
}
