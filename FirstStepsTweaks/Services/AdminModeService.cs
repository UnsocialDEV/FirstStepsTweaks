using System.Collections.Generic;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class AdminModeService
    {
        private const string LoadoutMessageCode = "adminmode-loadout";
        private const string AdminLoadoutMessage = "Admin Inventory loaded";
        private const string SurvivalLoadoutMessage = "Survival Inventory loaded";
        private readonly ICoreServerAPI api;
        private readonly IAdminModeStore store;
        private readonly IAdminModePlayerStateController playerStateController;
        private readonly IAdminModeLoadoutService loadoutService;
        private readonly IAdminModeVitalsService vitalsService;
        private readonly IPlayerMessenger messenger;

        public AdminModeService(
            ICoreServerAPI api,
            IAdminModeStore store,
            IAdminModePlayerStateController playerStateController,
            IAdminModeLoadoutService loadoutService,
            IAdminModeVitalsService vitalsService,
            IPlayerMessenger messenger)
        {
            this.api = api;
            this.store = store;
            this.playerStateController = playerStateController;
            this.loadoutService = loadoutService;
            this.vitalsService = vitalsService;
            this.messenger = messenger;
        }

        public void Toggle(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            if (store.TryLoad(player, out AdminModeState state, out string loadError))
            {
                if (state.IsActive)
                {
                    Disable(player, state);
                    return;
                }

                Enable(player, state);
                return;
            }

            if (!string.IsNullOrWhiteSpace(loadError))
            {
                api.Logger.Error($"[FirstStepsTweaks] Failed to read admin mode state for {player.PlayerName}: {loadError}");
                Send(player, "Your stored admin mode data is corrupted. Admin mode cannot be toggled off safely.");
                return;
            }

            Enable(player, null);
        }

        public void OnPlayerNowPlaying(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            if (!store.TryLoad(player, out AdminModeState state, out string errorMessage))
            {
                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    api.Logger.Error($"[FirstStepsTweaks] Failed to reapply admin mode for {player.PlayerName}: {errorMessage}");
                }

                return;
            }

            if (!state.IsActive)
            {
                return;
            }

            playerStateController.Reapply(player, state);
            vitalsService.EnsureFull(player);
            SendLoadoutMessage(player, AdminLoadoutMessage);
        }

        private void Enable(IServerPlayer player, AdminModeState storedState)
        {
            if (storedState?.IsActive == true || store.IsActive(player))
            {
                Send(player, "Admin mode is already active.");
                return;
            }

            AdminModeState state = playerStateController.Capture(player);
            state.SurvivalInventories = loadoutService.SnapshotLiveLoadout(player);
            state.AdminInventories = CloneSnapshots(storedState?.AdminInventories);
            if (storedState == null)
            {
                state.AdminInventories = loadoutService.SnapshotInitialAdminLoadout(player);
            }

            if (!loadoutService.TryEquipLoadout(player, state.SurvivalInventories, state.AdminInventories))
            {
                Send(player, "Failed to equip your admin loadout. Admin mode was not enabled.");
                return;
            }

            vitalsService.CaptureAndFill(player, state);
            playerStateController.Enable(player, state);
            store.Save(player, state);
            SendLoadoutMessage(player, AdminLoadoutMessage);

            Send(player, "Admin mode enabled. Your admin loadout has been equipped.");
        }

        private void Disable(IServerPlayer player, AdminModeState state)
        {
            if (state?.SurvivalInventories == null)
            {
                api.Logger.Error($"[FirstStepsTweaks] Admin mode state for {player.PlayerName} is missing inventory data.");
                Send(player, "Stored admin mode data is incomplete. Admin mode cannot be disabled safely.");
                return;
            }

            List<PlayerInventorySnapshot> adminInventories = loadoutService.SnapshotLiveLoadout(player);
            if (!loadoutService.TryEquipLoadout(player, adminInventories, state.SurvivalInventories))
            {
                Send(player, "Failed to restore your stored survival loadout. Admin mode remains active for safety.");
                return;
            }

            playerStateController.Restore(player, state);
            vitalsService.RestoreOrFull(player, state);
            store.Save(player, CreateInactiveState(state, adminInventories));
            SendLoadoutMessage(player, SurvivalLoadoutMessage);
            Send(player, "Admin mode disabled. Your survival loadout has been restored.");
        }

        private void Send(IServerPlayer player, string message)
        {
            messenger.SendDual(
                player,
                message,
                GlobalConstants.InfoLogChatGroup,
                (int)EnumChatType.CommandSuccess,
                GlobalConstants.GeneralChatGroup,
                (int)EnumChatType.Notification);
        }

        private void SendLoadoutMessage(IServerPlayer player, string message)
        {
            messenger.SendIngameError(player, LoadoutMessageCode, message);
        }

        private static AdminModeState CreateInactiveState(AdminModeState activeState, List<PlayerInventorySnapshot> adminInventories)
        {
            return new AdminModeState
            {
                IsActive = false,
                SurvivalInventories = CloneSnapshots(activeState?.SurvivalInventories),
                AdminInventories = CloneSnapshots(adminInventories)
            };
        }

        private static List<PlayerInventorySnapshot> CloneSnapshots(IReadOnlyCollection<PlayerInventorySnapshot> snapshots)
        {
            var clonedSnapshots = new List<PlayerInventorySnapshot>();
            if (snapshots == null)
            {
                return clonedSnapshots;
            }

            foreach (PlayerInventorySnapshot snapshot in snapshots)
            {
                if (snapshot == null)
                {
                    continue;
                }

                var clonedSnapshot = new PlayerInventorySnapshot
                {
                    InventoryClassName = snapshot.InventoryClassName,
                    InventoryId = snapshot.InventoryId
                };

                foreach (PlayerInventorySlotSnapshot slot in snapshot.Slots ?? new List<PlayerInventorySlotSnapshot>())
                {
                    if (slot == null)
                    {
                        continue;
                    }

                    clonedSnapshot.Slots.Add(new PlayerInventorySlotSnapshot
                    {
                        SlotId = slot.SlotId,
                        StackBytes = slot.StackBytes == null ? new byte[0] : (byte[])slot.StackBytes.Clone()
                    });
                }

                clonedSnapshots.Add(clonedSnapshot);
            }

            return clonedSnapshots;
        }
    }
}
