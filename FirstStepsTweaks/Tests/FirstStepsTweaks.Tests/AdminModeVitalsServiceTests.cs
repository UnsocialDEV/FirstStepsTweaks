using FirstStepsTweaks.Services;
using Vintagestory.API.Datastructures;
using Xunit;

namespace FirstStepsTweaks.Tests
{
    public class AdminModeVitalsServiceTests
    {
        [Fact]
        public void CaptureAndFill_StoresCurrentVitals_AndSetsThemToFull()
        {
            var service = new AdminModeVitalsService();
            var state = new AdminModeState();
            var attributes = new TreeAttribute();
            var health = new TreeAttribute();
            health.SetFloat("currenthealth", 7f);
            health.SetFloat("maxhealth", 20f);
            attributes["health"] = health;
            var hunger = new TreeAttribute();
            hunger.SetFloat("currentsaturation", 300f);
            hunger.SetFloat("maxsaturation", 1200f);
            attributes["hunger"] = hunger;

            AdminModeVitalsServiceHarness.CaptureAndFill(service, attributes, state);

            Assert.Equal(7f, state.PriorCurrentHealth);
            Assert.Equal(300f, state.PriorCurrentSaturation);
            Assert.Equal(20f, ((ITreeAttribute)attributes["health"]).GetFloat("currenthealth"));
            Assert.Equal(1200f, ((ITreeAttribute)attributes["hunger"]).GetFloat("currentsaturation"));
        }

        [Fact]
        public void RestoreOrFull_UsesFullValues_WhenStoredVitalsAreMissing()
        {
            var service = new AdminModeVitalsService();
            var state = new AdminModeState();
            var attributes = new TreeAttribute();
            var health = new TreeAttribute();
            health.SetFloat("currenthealth", 3f);
            health.SetFloat("basemaxhealth", 18f);
            attributes["health"] = health;
            var hunger = new TreeAttribute();
            hunger.SetFloat("currentsaturation", 10f);
            hunger.SetFloat("maxsaturation", 900f);
            attributes["hunger"] = hunger;

            AdminModeVitalsServiceHarness.RestoreOrFull(service, attributes, state);

            Assert.Equal(18f, ((ITreeAttribute)attributes["health"]).GetFloat("currenthealth"));
            Assert.Equal(900f, ((ITreeAttribute)attributes["hunger"]).GetFloat("currentsaturation"));
        }
    }

    internal static class AdminModeVitalsServiceHarness
    {
        public static void CaptureAndFill(AdminModeVitalsService service, TreeAttribute attributes, AdminModeState state)
        {
            InvokePrivate(service, "CaptureAndFillFromAttributes", attributes, state);
        }

        public static void RestoreOrFull(AdminModeVitalsService service, TreeAttribute attributes, AdminModeState state)
        {
            InvokePrivate(service, "RestoreOrFullFromAttributes", attributes, state);
        }

        private static void InvokePrivate(AdminModeVitalsService service, string methodName, TreeAttribute attributes, AdminModeState state)
        {
            typeof(AdminModeVitalsService)
                .GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(service, new object[] { attributes, state });
        }
    }
}
