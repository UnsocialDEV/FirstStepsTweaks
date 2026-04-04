using System;
using System.Collections.Generic;
using FirstStepsTweaks.Infrastructure.Players;
using System.Text.Json.Serialization;
using Vintagestory.API.Common;

namespace FirstStepsTweaks.Services
{
    [Serializable]
    public sealed class AdminModeState
    {
        public bool IsActive { get; set; }

        public string PriorRoleCode { get; set; } = string.Empty;

        public EnumGameMode PriorGameMode { get; set; } = EnumGameMode.Survival;

        public bool PriorFreeMove { get; set; }

        public bool PriorNoClip { get; set; }

        public float PriorMoveSpeedMultiplier { get; set; }

        public bool GrantedGameModePrivilege { get; set; }

        public bool GrantedFreeMovePrivilege { get; set; }

        public float? PriorCurrentHealth { get; set; }

        public float? PriorCurrentSaturation { get; set; }

        public List<PlayerInventorySnapshot> SurvivalInventories { get; set; } = new List<PlayerInventorySnapshot>();

        public List<PlayerInventorySnapshot> AdminInventories { get; set; } = new List<PlayerInventorySnapshot>();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<PlayerInventorySnapshot> Inventories { get; set; }
    }
}
