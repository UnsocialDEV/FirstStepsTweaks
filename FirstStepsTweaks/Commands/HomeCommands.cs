using System;
using System.Linq;
using System.Text;
using FirstStepsTweaks.Config;
using FirstStepsTweaks.Infrastructure.Messaging;
using FirstStepsTweaks.Infrastructure.Teleport;
using FirstStepsTweaks.Services;
using FirstStepsTweaks.Teleport;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FirstStepsTweaks.Commands
{
    public sealed class HomeCommands
    {
        private readonly ICoreServerAPI api;
        private readonly TeleportConfig teleportConfig;
        private readonly HomeStore homeStore;
        private readonly IPlayerMessenger messenger;
        private readonly IBackLocationStore backLocationStore;
        private readonly ITeleportWarmupService teleportWarmupService;
        private readonly PlayerHomeLimitResolver homeLimitResolver;
        private readonly HomeSlotPolicy homeSlotPolicy;
        private readonly HomeAccessPolicy homeAccessPolicy;

        public HomeCommands(
            ICoreServerAPI api,
            FirstStepsTweaksConfig config,
            HomeStore homeStore,
            IPlayerMessenger messenger,
            IBackLocationStore backLocationStore,
            ITeleportWarmupService teleportWarmupService,
            PlayerHomeLimitResolver homeLimitResolver,
            HomeSlotPolicy homeSlotPolicy,
            HomeAccessPolicy homeAccessPolicy)
        {
            this.api = api;
            teleportConfig = config?.Teleport ?? new TeleportConfig();
            this.homeStore = homeStore;
            this.messenger = messenger;
            this.backLocationStore = backLocationStore;
            this.teleportWarmupService = teleportWarmupService;
            this.homeLimitResolver = homeLimitResolver;
            this.homeSlotPolicy = homeSlotPolicy;
            this.homeAccessPolicy = homeAccessPolicy;
        }

        public void Register()
        {
            api.ChatCommands
                .Create("sethome")
                .WithDescription("Set your default home or a named home at your current location")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("name"))
                .HandleWith(SetHome);

            api.ChatCommands
                .Create("home")
                .WithDescription("Teleport to your default home or a named home")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("name"))
                .HandleWith(Home);

            api.ChatCommands
                .Create("delhome")
                .WithDescription("Delete a named home")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(api.ChatCommands.Parsers.Word("name"))
                .HandleWith(DelHome);

            api.ChatCommands
                .Create("homes")
                .WithDescription("List your named homes")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(ListHomes);
        }

        private TextCommandResult SetHome(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            var homes = homeStore.GetAll(player);
            string requestedHomeName = args[0] as string;
            string homeName = ResolveSetHomeName(requestedHomeName, homes.Count);

            if (string.IsNullOrWhiteSpace(homeName))
            {
                messenger.SendDual(player, "If you already have homes set, you must provide a home name. Use /sethome <name>.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            int maxHomes = homeLimitResolver.Resolve(player, teleportConfig);
            bool updated = homes.ContainsKey(homeName);
            if (!homeSlotPolicy.CanCreate(homes, homeName, maxHomes))
            {
                messenger.SendDual(player, $"You already have {homes.Count}/{maxHomes} homes set. Extra homes are stored, but only your currently allowed homes are usable. Delete one before creating '{homeName}'.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            homeStore.Set(player, homeName);
            string action = updated ? "updated" : "set";
            messenger.SendDual(player, $"Home '{homeName}' {action}. ({homeStore.Count(player)}/{maxHomes})", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);

            return TextCommandResult.Success();
        }

        private TextCommandResult Home(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            int maxHomes = homeLimitResolver.Resolve(player, teleportConfig);
            var homes = homeStore.GetAll(player);
            var accessibleHomes = homeAccessPolicy.GetAccessibleHomes(homes, maxHomes);
            string requestedHomeName = homeStore.NormalizeHomeName(args[0] as string);
            string homeName;
            Vintagestory.API.MathTools.Vec3d target;

            if (string.IsNullOrWhiteSpace(requestedHomeName))
            {
                if (!homeStore.TryGetDefault(accessibleHomes, out homeName, out target))
                {
                    messenger.SendDual(player, "You do not have any homes set.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                    return TextCommandResult.Success();
                }
            }
            else if (!homeStore.TryGet(accessibleHomes, requestedHomeName, out target))
            {
                if (homes.ContainsKey(requestedHomeName))
                {
                    messenger.SendDual(player, $"Home '{requestedHomeName}' is stored, but your current tier only allows access to {maxHomes} home(s).", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                    return TextCommandResult.Success();
                }

                messenger.SendDual(player, $"Home '{requestedHomeName}' does not exist.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }
            else
            {
                homeName = requestedHomeName;
            }

            if (teleportConfig.WarmupSeconds > 0 && TeleportBypass.HasBypass(player))
            {
                TeleportBypass.NotifyBypassingCooldown(player, $"/home {homeName} warmup");
                backLocationStore.RecordCurrentLocation(player);
                player.Entity.TeleportToDouble(target.X, target.Y, target.Z);
                messenger.SendIngameError(player, "no_permission", $"Teleported to home '{homeName}'.");
                return TextCommandResult.Success();
            }

            teleportWarmupService.Begin(new TeleportWarmupRequest
            {
                Player = player,
                WarmupMessage = $"Teleporting to home '{homeName}' in {teleportConfig.WarmupSeconds} seconds. Do not move.",
                CountdownTemplate = $"Teleporting to home '{homeName}' in {{0}}...",
                CancelMessage = "Teleport cancelled because you moved.",
                SuccessIngameMessage = $"Teleported to home '{homeName}'.",
                BypassContext = $"/home {homeName} warmup",
                WarmupSeconds = teleportConfig.WarmupSeconds,
                TickIntervalMs = teleportConfig.TickIntervalMs,
                CancelMoveThreshold = teleportConfig.CancelMoveThreshold,
                WarmupInfoChatType = (int)EnumChatType.CommandSuccess,
                WarmupGeneralGroupId = GlobalConstants.GeneralChatGroup,
                WarmupGeneralChatType = (int)EnumChatType.Notification,
                CancelInfoChatType = (int)EnumChatType.CommandSuccess,
                CancelGeneralChatType = (int)EnumChatType.Notification,
                ExecuteTeleport = () =>
                {
                    backLocationStore.RecordCurrentLocation(player);
                    player.Entity.TeleportToDouble(target.X, target.Y, target.Z);
                }
            });

            return TextCommandResult.Success();
        }

        private TextCommandResult DelHome(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            string homeName = homeStore.NormalizeHomeName((string)args[0]);

            if (string.IsNullOrWhiteSpace(homeName))
            {
                messenger.SendDual(player, "Home name cannot be empty.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            if (!homeStore.Remove(player, homeName))
            {
                messenger.SendDual(player, $"Home '{homeName}' does not exist.", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            messenger.SendInfo(player, $"Home '{homeName}' deleted.", GlobalConstants.InfoLogChatGroup, (int)EnumChatType.CommandSuccess);
            messenger.SendGeneral(player, $"Home '{homeName}' deleted.", GlobalConstants.GeneralChatGroup, (int)EnumChatType.Notification);

            return TextCommandResult.Success();
        }

        private TextCommandResult ListHomes(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            var homes = homeStore.GetAll(player);
            int maxHomes = homeLimitResolver.Resolve(player, teleportConfig);
            var accessibleHomes = homeAccessPolicy.GetAccessibleHomes(homes, maxHomes);

            if (homes.Count == 0)
            {
                messenger.SendDual(player, $"You do not have any homes set. (0/{maxHomes})", (int)EnumChatType.CommandSuccess, (int)EnumChatType.Notification);
                return TextCommandResult.Success();
            }

            var builder = new StringBuilder();
            builder.AppendLine($"Homes ({accessibleHomes.Count}/{maxHomes} accessible, {homes.Count} stored):");
            string defaultHomeName = homeStore.TryGetDefault(accessibleHomes, out var resolvedDefaultHomeName, out _)
                ? resolvedDefaultHomeName
                : null;

            foreach (string homeName in homes.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                bool isAccessible = accessibleHomes.ContainsKey(homeName);
                string suffix = string.Equals(homeName, defaultHomeName, StringComparison.OrdinalIgnoreCase)
                    ? " (default)"
                    : isAccessible
                        ? string.Empty
                        : " (stored, locked by current tier)";
                builder.AppendLine($"- {homeName}{suffix}");
            }

            messenger.SendDual(player, builder.ToString().TrimEnd(), GlobalConstants.InfoLogChatGroup, (int)EnumChatType.Notification);
            return TextCommandResult.Success();
        }

        private string ResolveSetHomeName(string requestedHomeName, int existingHomeCount)
        {
            string normalizedHomeName = homeStore.NormalizeHomeName(requestedHomeName);
            if (!string.IsNullOrWhiteSpace(normalizedHomeName))
            {
                return normalizedHomeName;
            }

            return existingHomeCount == 0 ? HomeStore.MigratedLegacyHomeName : null;
        }
    }
}
