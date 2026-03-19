using FirstStepsTweaks.Config;
using FirstStepsTweaks.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Gravestones
{
    public sealed class GraveBlockSynchronizer : IGraveBlockSynchronizer
    {
        private readonly ICoreServerAPI api;
        private readonly CorpseConfig config;
        private Block cachedGraveBlock;

        public GraveBlockSynchronizer(ICoreServerAPI api, CorpseConfig config)
        {
            this.api = api;
            this.config = config;
        }

        public string GraveBlockCode => config.GraveBlockCode;

        public Block ResolveGraveBlock()
        {
            if (cachedGraveBlock != null)
            {
                return cachedGraveBlock;
            }

            AssetLocation graveCode = new AssetLocation(config.GraveBlockCode ?? "firststepstweaks:gravestone");
            cachedGraveBlock = api.World.GetBlock(graveCode);

            if (cachedGraveBlock == null)
            {
                api.Logger.Error($"[FirstStepsTweaks] Grave block not found: {graveCode}");
            }

            return cachedGraveBlock;
        }

        public void Ensure(GraveData grave)
        {
            if (grave == null)
            {
                return;
            }

            Block graveBlock = ResolveGraveBlock();
            if (graveBlock == null)
            {
                return;
            }

            var pos = grave.ToBlockPos();
            Block current = api.World.BlockAccessor.GetBlock(pos);
            if (current == null || current.Id != graveBlock.Id)
            {
                api.World.BlockAccessor.SetBlock(graveBlock.Id, pos);
            }
        }

        public void RemoveIfPresent(GraveData grave)
        {
            if (grave == null)
            {
                return;
            }

            Block graveBlock = ResolveGraveBlock();
            if (graveBlock == null)
            {
                return;
            }

            var pos = grave.ToBlockPos();
            Block current = api.World.BlockAccessor.GetBlock(pos);
            if (current != null && current.Id == graveBlock.Id)
            {
                api.World.BlockAccessor.SetBlock(0, pos);
            }
        }
    }
}
