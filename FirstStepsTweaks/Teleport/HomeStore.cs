using System.Collections.Generic;
using System.Linq;
using FirstStepsTweaks.Services;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Teleport
{
    public sealed class HomeStore
    {
        private const string LegacyHomeKey = "fst_homepos";
        private const string HomesKey = "fst_homes";
        public const string MigratedLegacyHomeName = "home";

        private readonly HomeDataSerializer serializer;
        private readonly HomeNameNormalizer normalizer;
        private readonly DefaultHomeResolver defaultHomeResolver;

        public HomeStore()
            : this(new HomeDataSerializer(), new HomeNameNormalizer(), new DefaultHomeResolver())
        {
        }

        public HomeStore(HomeDataSerializer serializer, HomeNameNormalizer normalizer, DefaultHomeResolver defaultHomeResolver)
        {
            this.serializer = serializer;
            this.normalizer = normalizer;
            this.defaultHomeResolver = defaultHomeResolver;
        }

        public IReadOnlyDictionary<string, HomeLocation> GetAll(IServerPlayer player)
        {
            return LoadHomes(player);
        }

        public int Count(IServerPlayer player)
        {
            return LoadHomes(player).Count;
        }

        public bool Contains(IServerPlayer player, string homeName)
        {
            return LoadHomes(player).ContainsKey(NormalizeHomeName(homeName));
        }

        public bool TryGet(IServerPlayer player, string homeName, out Vec3d position)
        {
            return TryGet(LoadHomes(player), homeName, out position);
        }

        public bool TryGetDefault(IServerPlayer player, out string homeName, out Vec3d position)
        {
            return TryGetDefault(LoadHomes(player), out homeName, out position);
        }

        public bool TryGet(IReadOnlyDictionary<string, HomeLocation> homes, string homeName, out Vec3d position)
        {
            position = null;
            if (homes == null || !homes.TryGetValue(NormalizeHomeName(homeName), out HomeLocation location))
            {
                return false;
            }

            position = new Vec3d(location.X, location.Y, location.Z);
            return true;
        }

        public bool TryGetDefault(IReadOnlyDictionary<string, HomeLocation> homes, out string homeName, out Vec3d position)
        {
            homeName = null;
            position = null;
            KeyValuePair<string, HomeLocation>? resolved = defaultHomeResolver.Resolve(homes);
            if (resolved == null)
            {
                return false;
            }

            homeName = resolved.Value.Key;
            HomeLocation location = resolved.Value.Value;
            position = new Vec3d(location.X, location.Y, location.Z);
            return true;
        }

        public void Set(IServerPlayer player, string homeName)
        {
            if (player?.Entity?.Pos == null)
            {
                return;
            }

            Set(player, homeName, player.Entity.Pos.X, player.Entity.Pos.Y, player.Entity.Pos.Z);
        }

        public void Set(IServerPlayer player, string homeName, Vec3d position)
        {
            if (position == null)
            {
                return;
            }

            Set(player, homeName, position.X, position.Y, position.Z);
        }

        public void Set(IServerPlayer player, string homeName, double x, double y, double z)
        {
            if (player == null)
            {
                return;
            }

            Dictionary<string, HomeLocation> homes = LoadHomes(player);
            string normalizedHomeName = NormalizeHomeName(homeName);
            long createdOrder = homes.TryGetValue(normalizedHomeName, out HomeLocation existing)
                ? existing.CreatedOrder
                : GetNextCreatedOrder(homes);

            homes[normalizedHomeName] = new HomeLocation(x, y, z, createdOrder);
            SaveHomes(player, homes);
        }

        public bool Remove(IServerPlayer player, string homeName)
        {
            Dictionary<string, HomeLocation> homes = LoadHomes(player);
            bool removed = homes.Remove(NormalizeHomeName(homeName));
            if (!removed)
            {
                return false;
            }

            SaveHomes(player, homes);
            return true;
        }

        public void Clear(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            player.SetModdata(HomesKey, null);
            player.SetModdata(LegacyHomeKey, null);
        }

        public string NormalizeHomeName(string homeName)
        {
            return normalizer.Normalize(homeName);
        }

        private Dictionary<string, HomeLocation> LoadHomes(IServerPlayer player)
        {
            byte[] data = player?.GetModdata(HomesKey);
            byte[] legacyData = player?.GetModdata(LegacyHomeKey);
            Dictionary<string, HomeLocation> homes = serializer.Deserialize(data);
            bool changed = EnsureCreationOrder(homes);

            if (homes.Count > 0 || legacyData == null)
            {
                if (changed)
                {
                    SaveHomes(player, homes);
                }

                return homes;
            }

            if (!serializer.TryDeserializeLegacy(legacyData, out HomeLocation legacyLocation))
            {
                return homes;
            }

            legacyLocation.CreatedOrder = GetNextCreatedOrder(homes);
            homes[MigratedLegacyHomeName] = legacyLocation;
            SaveHomes(player, homes);
            player.SetModdata(LegacyHomeKey, null);
            return homes;
        }

        private void SaveHomes(IServerPlayer player, Dictionary<string, HomeLocation> homes)
        {
            if (player == null)
            {
                return;
            }

            player.SetModdata(HomesKey, serializer.Serialize(homes));
        }

        private static bool EnsureCreationOrder(Dictionary<string, HomeLocation> homes)
        {
            bool changed = false;
            long nextOrder = homes.Values
                .Where(location => location != null && location.CreatedOrder > 0)
                .Select(location => location.CreatedOrder)
                .DefaultIfEmpty(0L)
                .Max();

            foreach (KeyValuePair<string, HomeLocation> pair in homes.OrderBy(pair => pair.Key, System.StringComparer.OrdinalIgnoreCase))
            {
                if (pair.Value == null || pair.Value.CreatedOrder > 0)
                {
                    continue;
                }

                nextOrder++;
                pair.Value.CreatedOrder = nextOrder;
                changed = true;
            }

            return changed;
        }

        private static long GetNextCreatedOrder(IReadOnlyDictionary<string, HomeLocation> homes)
        {
            return homes.Values
                .Where(location => location != null)
                .Select(location => location.CreatedOrder)
                .DefaultIfEmpty(0L)
                .Max() + 1L;
        }
    }
}
