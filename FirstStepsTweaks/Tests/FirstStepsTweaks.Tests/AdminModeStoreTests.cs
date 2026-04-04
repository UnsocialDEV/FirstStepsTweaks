using System.Reflection;
using System.Text.Json;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests
{
    public class AdminModeStoreTests
    {
        [Fact]
        public void SaveAndLoad_RoundTripsState()
        {
            IServerPlayer player = CreatePlayer("uid-admin", "Admin");
            var store = new AdminModeStore();
            var state = new AdminModeState
            {
                IsActive = true,
                PriorRoleCode = "admin",
                PriorGameMode = EnumGameMode.Survival,
                PriorFreeMove = false,
                PriorNoClip = true,
                PriorMoveSpeedMultiplier = 1.25f,
                GrantedGameModePrivilege = true,
                GrantedFreeMovePrivilege = false,
                PriorCurrentHealth = 13.5f,
                PriorCurrentSaturation = 642f,
                SurvivalInventories =
                {
                    new PlayerInventorySnapshot
                    {
                        InventoryClassName = "hotbar",
                        InventoryId = "player-hotbar",
                        Slots =
                        {
                            new PlayerInventorySlotSnapshot
                            {
                                SlotId = 2,
                                StackBytes = new byte[] { 1, 2, 3 }
                            }
                        }
                    }
                },
                AdminInventories =
                {
                    new PlayerInventorySnapshot
                    {
                        InventoryClassName = "character",
                        InventoryId = "player-character",
                        Slots =
                        {
                            new PlayerInventorySlotSnapshot
                            {
                                SlotId = 5,
                                StackBytes = new byte[] { 7, 8, 9 }
                            }
                        }
                    }
                }
            };

            store.Save(player, state);

            Assert.True(store.TryLoad(player, out AdminModeState loaded, out string error));
            Assert.Equal(string.Empty, error);
            Assert.True(loaded.IsActive);
            Assert.Equal("admin", loaded.PriorRoleCode);
            Assert.Equal(EnumGameMode.Survival, loaded.PriorGameMode);
            Assert.False(loaded.PriorFreeMove);
            Assert.True(loaded.PriorNoClip);
            Assert.Equal(1.25f, loaded.PriorMoveSpeedMultiplier);
            Assert.True(loaded.GrantedGameModePrivilege);
            Assert.False(loaded.GrantedFreeMovePrivilege);
            Assert.Equal(13.5f, loaded.PriorCurrentHealth);
            Assert.Equal(642f, loaded.PriorCurrentSaturation);
            Assert.Single(loaded.SurvivalInventories);
            Assert.Equal("hotbar", loaded.SurvivalInventories[0].InventoryClassName);
            Assert.Single(loaded.SurvivalInventories[0].Slots);
            Assert.Equal(2, loaded.SurvivalInventories[0].Slots[0].SlotId);
            Assert.Equal(new byte[] { 1, 2, 3 }, loaded.SurvivalInventories[0].Slots[0].StackBytes);
            Assert.Single(loaded.AdminInventories);
            Assert.Equal("character", loaded.AdminInventories[0].InventoryClassName);
            Assert.Single(loaded.AdminInventories[0].Slots);
            Assert.Equal(5, loaded.AdminInventories[0].Slots[0].SlotId);
            Assert.Equal(new byte[] { 7, 8, 9 }, loaded.AdminInventories[0].Slots[0].StackBytes);
            Assert.Null(loaded.Inventories);
        }

        [Fact]
        public void TryLoad_MigratesLegacyInventories_ToSurvivalInventories()
        {
            IServerPlayer player = CreatePlayer("uid-legacy", "Admin");
            var store = new AdminModeStore();
            byte[] legacyState = JsonSerializer.SerializeToUtf8Bytes(new
            {
                IsActive = true,
                Inventories = new[]
                {
                    new
                    {
                        InventoryClassName = "hotbar",
                        InventoryId = "player-hotbar",
                        Slots = new[]
                        {
                            new
                            {
                                SlotId = 1,
                                StackBytes = new byte[] { 4, 5, 6 }
                            }
                        }
                    }
                }
            });

            player.SetModdata("fst_adminmode", legacyState);

            Assert.True(store.TryLoad(player, out AdminModeState loaded, out string error));
            Assert.Equal(string.Empty, error);
            Assert.Single(loaded.SurvivalInventories);
            Assert.Equal("hotbar", loaded.SurvivalInventories[0].InventoryClassName);
            Assert.Empty(loaded.AdminInventories);
            Assert.Null(loaded.Inventories);
        }

        private static IServerPlayer CreatePlayer(string playerUid, string playerName)
        {
            IServerPlayer proxy = DispatchProxy.Create<IServerPlayer, TestServerPlayerProxy>();
            var state = (TestServerPlayerProxy)(object)proxy;
            state.PlayerUid = playerUid;
            state.PlayerName = playerName;
            return proxy;
        }

        private class TestServerPlayerProxy : DispatchProxy
        {
            private readonly Dictionary<string, byte[]?> modData = new(StringComparer.Ordinal);

            public string PlayerUid { get; set; } = string.Empty;

            public string PlayerName { get; set; } = string.Empty;

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                if (targetMethod == null)
                {
                    return null;
                }

                switch (targetMethod.Name)
                {
                    case "get_PlayerUID":
                        return PlayerUid;
                    case "get_PlayerName":
                        return PlayerName;
                    case "GetModdata":
                        return modData.TryGetValue((string)args![0]!, out byte[]? value) ? value : null;
                    case "SetModdata":
                        modData[(string)args![0]!] = (byte[]?)args[1];
                        return null;
                    default:
                        return targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
                }
            }
        }
    }
}
