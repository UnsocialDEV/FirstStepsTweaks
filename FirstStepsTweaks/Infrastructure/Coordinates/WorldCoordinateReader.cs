using System;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Infrastructure.Coordinates
{
    public sealed class WorldCoordinateReader : IWorldCoordinateReader
    {
        public Vec3d GetExactPosition(IServerPlayer player)
        {
            return GetExactPosition(player?.Entity);
        }

        public Vec3d GetExactPosition(Entity entity)
        {
            EntityPos position = ResolvePosition(entity);
            if (position == null)
            {
                return null;
            }

            return new Vec3d(position.X, position.Y, position.Z);
        }

        public BlockPos GetBlockPosition(IServerPlayer player)
        {
            return GetBlockPosition(player?.Entity);
        }

        public BlockPos GetBlockPosition(Entity entity)
        {
            Vec3d exactPosition = GetExactPosition(entity);
            int? dimension = GetDimension(entity);
            if (exactPosition == null || !dimension.HasValue)
            {
                return null;
            }

            // Avoid AsBlockPos here. The mod standardizes on explicit world-space flooring.
            return new BlockPos(
                (int)Math.Floor(exactPosition.X),
                (int)Math.Floor(exactPosition.Y),
                (int)Math.Floor(exactPosition.Z),
                dimension.Value);
        }

        public int? GetDimension(IServerPlayer player)
        {
            return GetDimension(player?.Entity);
        }

        public int? GetDimension(Entity entity)
        {
            EntityPos position = ResolvePosition(entity);
            return position == null ? (int?)null : position.Dimension;
        }

        private static EntityPos ResolvePosition(Entity entity)
        {
            if (entity == null)
            {
                return null;
            }

            return entity.ServerPos ?? entity.Pos;
        }
    }
}
