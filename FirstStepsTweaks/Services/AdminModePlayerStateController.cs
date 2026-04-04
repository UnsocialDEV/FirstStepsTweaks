using System;
using FirstStepsTweaks.Infrastructure.Players;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Services
{
    public sealed class AdminModePlayerStateController : IAdminModePlayerStateController
    {
        private const string GameModePrivilege = "gamemode";
        private const string FreeMovePrivilege = "freemove";

        private readonly IPlayerRoleCodeReader roleCodeReader;
        private readonly IPlayerRoleAssigner roleAssigner;
        private readonly IPlayerPrivilegeReader privilegeReader;
        private readonly IPlayerPrivilegeMutator privilegeMutator;

        public AdminModePlayerStateController(
            IPlayerRoleCodeReader roleCodeReader,
            IPlayerRoleAssigner roleAssigner,
            IPlayerPrivilegeReader privilegeReader,
            IPlayerPrivilegeMutator privilegeMutator)
        {
            this.roleCodeReader = roleCodeReader;
            this.roleAssigner = roleAssigner;
            this.privilegeReader = privilegeReader;
            this.privilegeMutator = privilegeMutator;
        }

        public AdminModeState Capture(IServerPlayer player)
        {
            return new AdminModeState
            {
                IsActive = true,
                PriorRoleCode = roleCodeReader.Read(player) ?? string.Empty,
                PriorGameMode = player?.WorldData?.CurrentGameMode ?? EnumGameMode.Survival,
                PriorFreeMove = player?.WorldData?.FreeMove == true,
                PriorNoClip = player?.WorldData?.NoClip == true,
                PriorMoveSpeedMultiplier = player?.WorldData?.MoveSpeedMultiplier ?? 1f
            };
        }

        public void Enable(IServerPlayer player, AdminModeState state)
        {
            if (player == null || state == null)
            {
                return;
            }

            if (!privilegeReader.HasPrivilege(player, GameModePrivilege))
            {
                privilegeMutator.Grant(player, GameModePrivilege);
                state.GrantedGameModePrivilege = true;
            }

            if (!privilegeReader.HasPrivilege(player, FreeMovePrivilege))
            {
                privilegeMutator.Grant(player, FreeMovePrivilege);
                state.GrantedFreeMovePrivilege = true;
            }

            ApplyMode(player, EnumGameMode.Creative, freeMove: true, noClip: false, player.WorldData?.MoveSpeedMultiplier ?? 1f);
        }

        public void Reapply(IServerPlayer player, AdminModeState state)
        {
            if (player == null || state == null || !state.IsActive)
            {
                return;
            }

            if (state.GrantedGameModePrivilege && !privilegeReader.HasPrivilege(player, GameModePrivilege))
            {
                privilegeMutator.Grant(player, GameModePrivilege);
            }

            if (state.GrantedFreeMovePrivilege && !privilegeReader.HasPrivilege(player, FreeMovePrivilege))
            {
                privilegeMutator.Grant(player, FreeMovePrivilege);
            }

            ApplyMode(player, EnumGameMode.Creative, freeMove: true, noClip: false, player.WorldData?.MoveSpeedMultiplier ?? 1f);
        }

        public void Restore(IServerPlayer player, AdminModeState state)
        {
            if (player == null || state == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(state.PriorRoleCode))
            {
                roleAssigner.Assign(player, state.PriorRoleCode);
            }

            ApplyMode(player, state.PriorGameMode, state.PriorFreeMove, state.PriorNoClip, state.PriorMoveSpeedMultiplier);

            if (state.GrantedGameModePrivilege)
            {
                privilegeMutator.Revoke(player, GameModePrivilege);
            }

            if (state.GrantedFreeMovePrivilege)
            {
                privilegeMutator.Revoke(player, FreeMovePrivilege);
            }
        }

        private static void ApplyMode(IServerPlayer player, EnumGameMode gameMode, bool freeMove, bool noClip, float moveSpeedMultiplier)
        {
            if (player?.WorldData == null)
            {
                return;
            }

            player.WorldData.CurrentGameMode = gameMode;
            player.WorldData.FreeMove = freeMove;
            player.WorldData.NoClip = noClip;
            player.WorldData.MoveSpeedMultiplier = Math.Max(0f, moveSpeedMultiplier);
            player.BroadcastPlayerData();
        }
    }
}
