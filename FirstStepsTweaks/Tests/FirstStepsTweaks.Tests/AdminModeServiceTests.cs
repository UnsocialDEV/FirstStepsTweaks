using System.Reflection;
using FirstStepsTweaks.Infrastructure.Coordinates;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace FirstStepsTweaks.Tests
{
    public class AdminModeServiceTests
    {
        [Fact]
        public void Toggle_EnablesAdminMode_WhenNoStoredStateExists()
        {
            IServerPlayer player = CreatePlayer("uid-1", "Admin");
            var store = new FakeAdminModeStore();
            var controller = new FakeAdminModePlayerStateController();
            var loadoutService = new FakeAdminModeLoadoutService
            {
                SnapshotsToReturn =
                {
                    new PlayerInventorySnapshot { InventoryClassName = "hotbar" }
                },
                InitialAdminSnapshotsToReturn =
                {
                    new PlayerInventorySnapshot { InventoryClassName = "character" }
                }
            };
            var vitals = new FakeAdminModeVitalsService();
            var messenger = new FakePlayerMessenger();
            var service = CreateService(store, controller, loadoutService, vitals, messenger);

            service.Toggle(player);

            Assert.True(store.IsActive(player));
            Assert.Equal(1, loadoutService.SnapshotCallCount);
            Assert.Equal(1, loadoutService.InitialSnapshotCallCount);
            Assert.True(loadoutService.TryEquipCalled);
            Assert.True(controller.EnableCalled);
            Assert.True(vitals.CaptureAndFillCalled);
            Assert.Equal("Admin mode enabled. Your admin loadout has been equipped.", messenger.LastMessage);
            Assert.Equal("Admin Inventory loaded", messenger.LastIngameErrorMessage);
            Assert.Single(store.LastSavedState!.SurvivalInventories);
            Assert.Single(store.LastSavedState.AdminInventories);
            Assert.Equal("character", store.LastSavedState.AdminInventories[0].InventoryClassName);
            Assert.Single(loadoutService.LastTargetLoadout!);
            Assert.Equal("character", loadoutService.LastTargetLoadout![0].InventoryClassName);
            Assert.Equal(9f, store.LastSavedState.PriorCurrentHealth);
            Assert.Equal(555f, store.LastSavedState.PriorCurrentSaturation);
        }

        [Fact]
        public void Toggle_EnablesAdminMode_WhenStoredStateIsInactive()
        {
            IServerPlayer player = CreatePlayer("uid-2", "Admin");
            var store = new FakeAdminModeStore();
            store.Save(player, new AdminModeState
            {
                IsActive = false,
                AdminInventories =
                {
                    new PlayerInventorySnapshot { InventoryClassName = "character" }
                }
            });
            var controller = new FakeAdminModePlayerStateController();
            var loadoutService = new FakeAdminModeLoadoutService
            {
                SnapshotsToReturn =
                {
                    new PlayerInventorySnapshot { InventoryClassName = "hotbar" }
                }
            };
            var vitals = new FakeAdminModeVitalsService();
            var messenger = new FakePlayerMessenger();
            var service = CreateService(store, controller, loadoutService, vitals, messenger);

            service.Toggle(player);

            Assert.True(store.IsActive(player));
            Assert.True(loadoutService.TryEquipCalled);
            Assert.Equal(0, loadoutService.InitialSnapshotCallCount);
            Assert.Single(loadoutService.LastTargetLoadout!);
            Assert.Equal("character", loadoutService.LastTargetLoadout![0].InventoryClassName);
            Assert.True(controller.EnableCalled);
            Assert.True(vitals.CaptureAndFillCalled);
            Assert.Equal("Admin Inventory loaded", messenger.LastIngameErrorMessage);
        }

        [Fact]
        public void Toggle_DisablesAdminMode_WhenStoredStateCanBeRestored()
        {
            IServerPlayer player = CreatePlayer("uid-3", "Admin");
            var store = new FakeAdminModeStore();
            store.Save(player, new AdminModeState
            {
                IsActive = true,
                PriorCurrentHealth = 4f,
                PriorCurrentSaturation = 200f,
                SurvivalInventories =
                {
                    new PlayerInventorySnapshot { InventoryClassName = "hotbar" }
                }
            });
            var controller = new FakeAdminModePlayerStateController();
            var loadoutService = new FakeAdminModeLoadoutService
            {
                SnapshotsToReturn =
                {
                    new PlayerInventorySnapshot { InventoryClassName = "character" }
                }
            };
            var vitals = new FakeAdminModeVitalsService();
            var messenger = new FakePlayerMessenger();
            var service = CreateService(store, controller, loadoutService, vitals, messenger);

            service.Toggle(player);

            Assert.False(store.IsActive(player));
            Assert.True(loadoutService.TryEquipCalled);
            Assert.True(controller.RestoreCalled);
            Assert.True(vitals.RestoreOrFullCalled);
            Assert.Equal(4f, vitals.LastRestoredState!.PriorCurrentHealth);
            Assert.Equal(200f, vitals.LastRestoredState.PriorCurrentSaturation);
            Assert.Single(store.LastSavedState!.AdminInventories);
            Assert.Equal("character", store.LastSavedState.AdminInventories[0].InventoryClassName);
            Assert.Single(store.LastSavedState.SurvivalInventories);
            Assert.Equal("Survival Inventory loaded", messenger.LastIngameErrorMessage);
            Assert.Equal("Admin mode disabled. Your survival loadout has been restored.", messenger.LastMessage);
        }

        [Fact]
        public void OnPlayerNowPlaying_ReappliesAdminMode_WhenStoredStateIsActive()
        {
            IServerPlayer player = CreatePlayer("uid-4", "Admin");
            var store = new FakeAdminModeStore();
            store.Save(player, new AdminModeState { IsActive = true, PriorCurrentHealth = 6f, PriorCurrentSaturation = 77f });
            var controller = new FakeAdminModePlayerStateController();
            var loadoutService = new FakeAdminModeLoadoutService();
            var vitals = new FakeAdminModeVitalsService();
            var messenger = new FakePlayerMessenger();
            var service = CreateService(store, controller, loadoutService, vitals, messenger);

            service.OnPlayerNowPlaying(player);

            Assert.True(controller.ReapplyCalled);
            Assert.True(vitals.EnsureFullCalled);
            Assert.False(vitals.CaptureAndFillCalled);
            Assert.False(loadoutService.TryEquipCalled);
            Assert.Equal(0, loadoutService.SnapshotCallCount);
            Assert.Equal("Admin Inventory loaded", messenger.LastIngameErrorMessage);
        }

        [Fact]
        public void Toggle_RefusesWhenStoredStateIsCorrupted()
        {
            IServerPlayer player = CreatePlayer("uid-5", "Admin");
            var store = new FakeAdminModeStore { LoadError = "bad json" };
            var controller = new FakeAdminModePlayerStateController();
            var loadoutService = new FakeAdminModeLoadoutService();
            var vitals = new FakeAdminModeVitalsService();
            var messenger = new FakePlayerMessenger();
            var service = CreateService(store, controller, loadoutService, vitals, messenger);

            service.Toggle(player);

            Assert.False(loadoutService.TryEquipCalled);
            Assert.False(controller.EnableCalled);
            Assert.False(vitals.CaptureAndFillCalled);
            Assert.Equal("Your stored admin mode data is corrupted. Admin mode cannot be toggled off safely.", messenger.LastMessage);
        }

        [Fact]
        public void Toggle_LeavesAdminModeActive_WhenSurvivalLoadoutCannotBeRestored()
        {
            IServerPlayer player = CreatePlayer("uid-6", "Admin");
            var store = new FakeAdminModeStore();
            store.Save(player, new AdminModeState
            {
                IsActive = true,
                SurvivalInventories =
                {
                    new PlayerInventorySnapshot { InventoryClassName = "hotbar" }
                }
            });
            var controller = new FakeAdminModePlayerStateController();
            var loadoutService = new FakeAdminModeLoadoutService
            {
                TryEquipShouldSucceed = false,
                SnapshotsToReturn =
                {
                    new PlayerInventorySnapshot { InventoryClassName = "character" }
                }
            };
            var vitals = new FakeAdminModeVitalsService();
            var messenger = new FakePlayerMessenger();
            var service = CreateService(store, controller, loadoutService, vitals, messenger);

            service.Toggle(player);

            Assert.True(store.IsActive(player));
            Assert.False(controller.RestoreCalled);
            Assert.False(vitals.RestoreOrFullCalled);
            Assert.Equal("Failed to restore your stored survival loadout. Admin mode remains active for safety.", messenger.LastMessage);
        }

        private static AdminModeService CreateService(
            IAdminModeStore store,
            IAdminModePlayerStateController controller,
            IAdminModeLoadoutService loadoutService,
            IAdminModeVitalsService vitalsService,
            IPlayerMessenger messenger)
        {
            ILogger logger = DispatchProxy.Create<ILogger, LoggerProxy>();
            ((LoggerProxy)(object)logger).LastError = null;
            ICoreServerAPI api = DispatchProxy.Create<ICoreServerAPI, CoreServerApiProxy>();
            ((CoreServerApiProxy)(object)api).Logger = logger;

            return new AdminModeService(
                api,
                store,
                controller,
                loadoutService,
                vitalsService,
                messenger);
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
            public string PlayerUid { get; set; } = string.Empty;

            public string PlayerName { get; set; } = string.Empty;

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                if (targetMethod == null)
                {
                    return null;
                }

                return targetMethod.Name switch
                {
                    "get_PlayerUID" => PlayerUid,
                    "get_PlayerName" => PlayerName,
                    _ => targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null
                };
            }
        }

        private sealed class FakeAdminModeStore : IAdminModeStore
        {
            private readonly Dictionary<string, AdminModeState> stateByPlayerUid = new(StringComparer.OrdinalIgnoreCase);

            public string LoadError { get; set; } = string.Empty;

            public AdminModeState? LastSavedState { get; private set; }

            public bool IsActive(IServerPlayer player)
            {
                return player != null
                    && stateByPlayerUid.TryGetValue(player.PlayerUID, out AdminModeState? state)
                    && state?.IsActive == true;
            }

            public bool TryLoad(IServerPlayer player, out AdminModeState state, out string errorMessage)
            {
                state = null!;
                errorMessage = LoadError;

                if (!string.IsNullOrWhiteSpace(LoadError))
                {
                    return false;
                }

                return player != null && stateByPlayerUid.TryGetValue(player.PlayerUID, out state!);
            }

            public void Save(IServerPlayer player, AdminModeState state)
            {
                stateByPlayerUid[player.PlayerUID] = state;
                LastSavedState = state;
            }

            public void Clear(IServerPlayer player)
            {
                stateByPlayerUid.Remove(player.PlayerUID);
            }
        }

        private sealed class FakeAdminModePlayerStateController : IAdminModePlayerStateController
        {
            public bool EnableCalled { get; private set; }

            public bool ReapplyCalled { get; private set; }

            public bool RestoreCalled { get; private set; }

            public AdminModeState Capture(IServerPlayer player)
            {
                return new AdminModeState
                {
                    IsActive = true,
                    PriorRoleCode = "admin"
                };
            }

            public void Enable(IServerPlayer player, AdminModeState state)
            {
                EnableCalled = true;
            }

            public void Reapply(IServerPlayer player, AdminModeState state)
            {
                ReapplyCalled = true;
            }

            public void Restore(IServerPlayer player, AdminModeState state)
            {
                RestoreCalled = true;
            }
        }

        private sealed class FakeAdminModeLoadoutService : IAdminModeLoadoutService
        {
            public bool TryEquipShouldSucceed { get; set; } = true;

            public bool TryEquipCalled { get; private set; }

            public int SnapshotCallCount { get; private set; }

            public int InitialSnapshotCallCount { get; private set; }

            public List<PlayerInventorySnapshot>? LastCurrentLoadout { get; private set; }

            public List<PlayerInventorySnapshot>? LastTargetLoadout { get; private set; }

            public List<PlayerInventorySnapshot> SnapshotsToReturn { get; } = new();

            public List<PlayerInventorySnapshot> InitialAdminSnapshotsToReturn { get; } = new();

            public List<PlayerInventorySnapshot> SnapshotLiveLoadout(IServerPlayer player)
            {
                SnapshotCallCount++;
                return new List<PlayerInventorySnapshot>(SnapshotsToReturn);
            }

            public List<PlayerInventorySnapshot> SnapshotInitialAdminLoadout(IServerPlayer player)
            {
                InitialSnapshotCallCount++;
                return new List<PlayerInventorySnapshot>(InitialAdminSnapshotsToReturn);
            }

            public bool TryEquipLoadout(
                IServerPlayer player,
                IReadOnlyCollection<PlayerInventorySnapshot> currentLoadout,
                IReadOnlyCollection<PlayerInventorySnapshot> targetLoadout)
            {
                TryEquipCalled = true;
                LastCurrentLoadout = currentLoadout == null ? new List<PlayerInventorySnapshot>() : new List<PlayerInventorySnapshot>(currentLoadout);
                LastTargetLoadout = targetLoadout == null ? new List<PlayerInventorySnapshot>() : new List<PlayerInventorySnapshot>(targetLoadout);
                return TryEquipShouldSucceed;
            }
        }

        private sealed class FakePlayerMessenger : IPlayerMessenger
        {
            public string? LastMessage { get; private set; }

            public string? LastIngameErrorMessage { get; private set; }

            public void SendInfo(IServerPlayer player, string message, int groupId, int chatType)
            {
                LastMessage = message;
            }

            public void SendGeneral(IServerPlayer player, string message, int groupId, int chatType)
            {
                LastMessage = message;
            }

            public void SendDual(IServerPlayer player, string message, int infoChatType, int generalChatType)
            {
                LastMessage = message;
            }

            public void SendDual(IServerPlayer player, string message, int infoGroupId, int infoChatType, int generalGroupId, int generalChatType)
            {
                LastMessage = message;
            }

            public void SendIngameError(IServerPlayer player, string code, string message)
            {
                LastIngameErrorMessage = message;
            }
        }

        private sealed class FakeAdminModeVitalsService : IAdminModeVitalsService
        {
            public bool CaptureAndFillCalled { get; private set; }

            public bool EnsureFullCalled { get; private set; }

            public bool RestoreOrFullCalled { get; private set; }

            public AdminModeState? LastRestoredState { get; private set; }

            public void CaptureAndFill(IServerPlayer player, AdminModeState state)
            {
                CaptureAndFillCalled = true;
                state.PriorCurrentHealth = 9f;
                state.PriorCurrentSaturation = 555f;
            }

            public void EnsureFull(IServerPlayer player)
            {
                EnsureFullCalled = true;
            }

            public void RestoreOrFull(IServerPlayer player, AdminModeState state)
            {
                RestoreOrFullCalled = true;
                LastRestoredState = state;
            }
        }
        private class CoreServerApiProxy : DispatchProxy
        {
            public ILogger? Logger { get; set; }

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                if (targetMethod == null)
                {
                    return null;
                }

                if (targetMethod.Name == "get_Logger")
                {
                    return Logger;
                }

                return targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
            }
        }

        private class LoggerProxy : DispatchProxy
        {
            public string? LastError { get; set; }

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                if (targetMethod == null)
                {
                    return null;
                }

                if (targetMethod.Name.StartsWith("Error", StringComparison.Ordinal))
                {
                    if (args?.Length > 0 && args[0] is string message)
                    {
                        LastError = message;
                    }
                    else if (args?.Length > 0)
                    {
                        LastError = args[0]?.ToString();
                    }
                }

                if (targetMethod.ReturnType == typeof(void))
                {
                    return null;
                }

                return targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
            }
        }
    }
}
