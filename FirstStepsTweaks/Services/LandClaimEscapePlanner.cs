using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace FirstStepsTweaks.Services
{
    public sealed class LandClaimEscapePlanner
    {
        public IReadOnlyList<BlockPos> PlanColumns(IReadOnlyList<Cuboidi> claimAreas, double originX, double originZ, int originY, int dimension, int maxShells)
        {
            if (claimAreas == null || claimAreas.Count == 0 || maxShells <= 0)
            {
                return Array.Empty<BlockPos>();
            }

            var candidates = new List<CandidateColumn>();
            var seenColumns = new HashSet<long>();

            foreach (Cuboidi area in claimAreas)
            {
                if (area == null)
                {
                    continue;
                }

                for (int shell = 1; shell <= maxShells; shell++)
                {
                    AddPerimeterCandidates(area, claimAreas, originX, originZ, originY, shell, seenColumns, candidates);
                }
            }

            return BuildOrderedColumns(candidates, dimension);
        }

        public IReadOnlyList<BlockPos> PlanFallbackColumns(double originX, double originZ, int dimension, int maxShells)
        {
            if (maxShells <= 0)
            {
                return Array.Empty<BlockPos>();
            }

            int originBlockX = (int)Math.Floor(originX);
            int originBlockZ = (int)Math.Floor(originZ);
            var candidates = new List<CandidateColumn>();
            var seenColumns = new HashSet<long>();

            for (int shell = 1; shell <= maxShells; shell++)
            {
                AddPerimeterCandidates(
                    originBlockX - shell,
                    originBlockX + shell,
                    originBlockZ - shell,
                    originBlockZ + shell,
                    originX,
                    originZ,
                    shell,
                    seenColumns,
                    candidates);
            }

            return BuildOrderedColumns(candidates, dimension);
        }

        private static IReadOnlyList<BlockPos> BuildOrderedColumns(List<CandidateColumn> candidates, int dimension)
        {
            if (candidates.Count == 0)
            {
                return Array.Empty<BlockPos>();
            }

            candidates.Sort((left, right) =>
            {
                int shellCompare = left.Shell.CompareTo(right.Shell);
                if (shellCompare != 0)
                {
                    return shellCompare;
                }

                int distanceCompare = left.DistanceSquared.CompareTo(right.DistanceSquared);
                if (distanceCompare != 0)
                {
                    return distanceCompare;
                }

                int xCompare = left.X.CompareTo(right.X);
                return xCompare != 0 ? xCompare : left.Z.CompareTo(right.Z);
            });

            var orderedColumns = new BlockPos[candidates.Count];
            for (int index = 0; index < candidates.Count; index++)
            {
                CandidateColumn candidate = candidates[index];
                orderedColumns[index] = new BlockPos(candidate.X, 0, candidate.Z, dimension);
            }

            return orderedColumns;
        }

        private static void AddPerimeterCandidates(
            Cuboidi area,
            IReadOnlyList<Cuboidi> allAreas,
            double originX,
            double originZ,
            int originY,
            int shell,
            HashSet<long> seenColumns,
            List<CandidateColumn> candidates)
        {
            int minX = GetOccupiedMin(area.X1, area.X2) - shell;
            int maxX = GetOccupiedMax(area.X1, area.X2) + shell;
            int minZ = GetOccupiedMin(area.Z1, area.Z2) - shell;
            int maxZ = GetOccupiedMax(area.Z1, area.Z2) + shell;

            AddPerimeterCandidates(minX, maxX, minZ, maxZ, originX, originZ, shell, seenColumns, candidates, (x, z) =>
                IsInsideAnyArea(allAreas, x, originY, z));
        }

        private static void AddPerimeterCandidates(
            int minX,
            int maxX,
            int minZ,
            int maxZ,
            double originX,
            double originZ,
            int shell,
            HashSet<long> seenColumns,
            List<CandidateColumn> candidates,
            Func<int, int, bool> shouldSkip = null)
        {
            for (int x = minX; x <= maxX; x++)
            {
                AddCandidate(x, minZ, originX, originZ, shell, seenColumns, candidates, shouldSkip);
                if (maxZ != minZ)
                {
                    AddCandidate(x, maxZ, originX, originZ, shell, seenColumns, candidates, shouldSkip);
                }
            }

            for (int z = minZ + 1; z <= maxZ - 1; z++)
            {
                AddCandidate(minX, z, originX, originZ, shell, seenColumns, candidates, shouldSkip);
                if (maxX != minX)
                {
                    AddCandidate(maxX, z, originX, originZ, shell, seenColumns, candidates, shouldSkip);
                }
            }
        }

        private static void AddCandidate(
            int x,
            int z,
            double originX,
            double originZ,
            int shell,
            HashSet<long> seenColumns,
            List<CandidateColumn> candidates,
            Func<int, int, bool> shouldSkip)
        {
            if (shouldSkip != null && shouldSkip(x, z))
            {
                return;
            }

            long key = MakeColumnKey(x, z);
            if (!seenColumns.Add(key))
            {
                return;
            }

            double dx = (x + 0.5) - originX;
            double dz = (z + 0.5) - originZ;
            candidates.Add(new CandidateColumn(x, z, shell, (dx * dx) + (dz * dz)));
        }

        private static bool IsInsideAnyArea(IReadOnlyList<Cuboidi> areas, int x, int y, int z)
        {
            foreach (Cuboidi area in areas)
            {
                if (area != null && area.Contains(x, y, z))
                {
                    return true;
                }
            }

            return false;
        }

        private static long MakeColumnKey(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }

        private static int GetOccupiedMin(int start, int endExclusive)
        {
            return Math.Min(start, endExclusive);
        }

        private static int GetOccupiedMax(int start, int endExclusive)
        {
            return Math.Max(start, endExclusive) - 1;
        }

        private readonly struct CandidateColumn
        {
            public CandidateColumn(int x, int z, int shell, double distanceSquared)
            {
                X = x;
                Z = z;
                Shell = shell;
                DistanceSquared = distanceSquared;
            }

            public int X { get; }
            public int Z { get; }
            public int Shell { get; }
            public double DistanceSquared { get; }
        }
    }
}
