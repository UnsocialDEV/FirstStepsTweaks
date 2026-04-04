using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Players
{
    public interface IPlayerLoadoutManager
    {
        List<PlayerInventorySnapshot> Snapshot(IServerPlayer player, List<string> debugEntries = null);

        List<PlayerInventorySnapshot> Snapshot(IServerPlayer player, PlayerLoadoutScope scope, List<string> debugEntries = null);

        void Clear(IServerPlayer player, IReadOnlyCollection<PlayerInventorySnapshot> snapshots);

        bool HasAnyItems(IServerPlayer player);

        int DuplicateToPlayer(IReadOnlyCollection<PlayerInventorySnapshot> snapshots, IServerPlayer targetPlayer, BlockPos fallbackPos);

        bool TryRestore(IReadOnlyCollection<PlayerInventorySnapshot> snapshots, IServerPlayer targetPlayer, BlockPos fallbackPos, out int transferredStacks, out int failedStacks);
    }
}
