using System.Reflection;
using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Common;
using Xunit;

namespace FirstStepsTweaks.Tests
{
    public class PlayerLoadoutManagerTests
    {
        private static readonly MethodInfo ShouldTrackSlotMethod = typeof(PlayerLoadoutManager).GetMethod(
            "ShouldTrackSlot",
            BindingFlags.Static | BindingFlags.NonPublic);

        [Fact]
        public void ShouldTrackSlot_ReturnsTrue_ForArmorCharacterSlots()
        {
            ItemSlot slot = new ItemSlotCharacter(EnumCharacterDressType.ArmorBody, new InventoryGeneric((ICoreAPI)null));

            bool result = InvokeShouldTrackSlot("character", slot, PlayerLoadoutScope.Gravestone);

            Assert.True(result);
        }

        [Fact]
        public void ShouldTrackSlot_ReturnsFalse_ForClothingCharacterSlots_InGravestoneScope()
        {
            ItemSlot slot = new ItemSlotCharacter(EnumCharacterDressType.UpperBody, new InventoryGeneric((ICoreAPI)null));

            bool result = InvokeShouldTrackSlot("character", slot, PlayerLoadoutScope.Gravestone);

            Assert.False(result);
        }

        [Fact]
        public void ShouldTrackSlot_ReturnsTrue_ForClothingCharacterSlots_InAdminModeScope()
        {
            ItemSlot slot = new ItemSlotCharacter(EnumCharacterDressType.UpperBody, new InventoryGeneric((ICoreAPI)null));

            bool result = InvokeShouldTrackSlot("character", slot, PlayerLoadoutScope.AdminMode);

            Assert.True(result);
        }

        [Fact]
        public void ShouldTrackSlot_ReturnsTrue_ForClothingCharacterSlots_InAdminModeInitialSeedScope()
        {
            ItemSlot slot = new ItemSlotCharacter(EnumCharacterDressType.UpperBody, new InventoryGeneric((ICoreAPI)null));

            bool result = InvokeShouldTrackSlot("character", slot, PlayerLoadoutScope.AdminModeInitialSeed);

            Assert.True(result);
        }

        [Fact]
        public void ShouldTrackSlot_ReturnsFalse_ForArmorCharacterSlots_InAdminModeInitialSeedScope()
        {
            ItemSlot slot = new ItemSlotCharacter(EnumCharacterDressType.ArmorBody, new InventoryGeneric((ICoreAPI)null));

            bool result = InvokeShouldTrackSlot("character", slot, PlayerLoadoutScope.AdminModeInitialSeed);

            Assert.False(result);
        }

        [Fact]
        public void ShouldTrackSlot_ReturnsFalse_ForHotbarSlots_InAdminModeInitialSeedScope()
        {
            ItemSlot slot = new ItemSlot(new InventoryGeneric((ICoreAPI)null));

            bool result = InvokeShouldTrackSlot("hotbar", slot, PlayerLoadoutScope.AdminModeInitialSeed);

            Assert.False(result);
        }

        [Fact]
        public void ShouldTrackSlot_ReturnsTrue_ForNonCharacterInventories()
        {
            ItemSlot slot = new ItemSlot(new InventoryGeneric((ICoreAPI)null));

            bool result = InvokeShouldTrackSlot("hotbar", slot, PlayerLoadoutScope.Gravestone);

            Assert.True(result);
        }

        private static bool InvokeShouldTrackSlot(string inventoryClassName, ItemSlot slot, PlayerLoadoutScope scope)
        {
            return (bool)ShouldTrackSlotMethod.Invoke(null, new object[] { inventoryClassName, slot, scope });
        }
    }
}
