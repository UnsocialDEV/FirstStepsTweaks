# FirstStepsTweaks

FirstStepsTweaks is a server-side Vintage Story mod that bundles practical multiplayer quality-of-life systems into a single mod while keeping the code split into small focused features, commands, stores, and services.

The current runtime is centered on a small [`FirstStepsTweak.cs`](C:\Users\daytonwatson\source\repos\FirstStepsTweaks\FirstStepsTweaks\FirstStepsTweak.cs) entry point that loads config, registers privileges, builds shared runtime dependencies, and wires feature modules.

## What the mod currently includes

- Teleport commands: `/back`, `/sethome`, `/home`, `/delhome`, `/homes`, `/setspawn`, `/spawn`, `/setwarp`, `/warp`, `/warps`, `/delwarp`, `/rtp`, `/tpa`, `/tpaccept`, `/tpadeny`, `/tpacancel`, `/tpatoggle`
- Emergency / location recovery commands: `/setstormshelter`, `/stormshelter`, `/stuck`
- Join and return messaging
- Join-time invulnerability handling
- Donator chat prefixes
- Discord invite command, Discord chat relay, Discord account linking, and Discord-driven donator privilege sync
- Starter and winter kits
- Gravestones, grave recovery, grave lookup, and grave admin commands
- Utility commands such as `/whosonline`, `/wind`, `/heal`, `/feed`, and `/fsdebug`

## Tech stack

- C#
- .NET 8
- Vintage Story server mod API
- xUnit for unit tests
- PowerShell packaging script for creating the final mod zip

## Runtime overview

The server entry point is [`FirstStepsTweak.cs`](C:\Users\daytonwatson\source\repos\FirstStepsTweaks\FirstStepsTweaks\FirstStepsTweak.cs), which contains the `FirstStepsTweaks : ModSystem` class.

At startup the mod does the following:

1. Loads `firststepstweaks.json`.
2. Falls back to the legacy `FirstStepsTweaks.json` name and migrates it to `firststepstweaks.json` when needed.
3. Applies config upgrades through `JoinConfigUpgrader` and `TeleportConfigUpgrader`.
4. Builds a shared `FeatureRuntime` with cross-feature services such as player messaging, player lookup, back-location tracking, teleport warmups, land claim access, gravestone services, and Discord link rewards.
5. Registers privileges used by commands and donor features.
6. Registers feature modules in this order:
   - `JoinFeature`
   - `TeleportFeature`
   - `ChatFeature`
   - `DiscordFeature`
   - `UtilityFeature`
   - `GravestoneFeature` when `Features.EnableCorpseService` is enabled

Registered privileges currently include:

- `firststepstweaks.back`
- `firststepstweaks.supporter`
- `firststepstweaks.contributor`
- `firststepstweaks.sponsor`
- `firststepstweaks.patron`
- `firststepstweaks.founder`
- `firststepstweaks.graveadmin`
- `firststepstweaks.bypassteleportcooldown`

## Feature map

| Feature | Main entry point | Active behavior |
|---|---|---|
| Join | `JoinFeature` | Join broadcasts, return messaging, join invulnerability, Discord link reward claim-on-join flow |
| Teleport | `TeleportFeature` | Back, homes, spawn, storm shelter, stuck escape, warps, RTP, and TPA |
| Chat | `ChatFeature` | Donator chat prefix application on player chat |
| Discord | `DiscordFeature` | Discord relay, `/discord`, `/discordlink`, `/discordunlink`, Discord link polling, avatar enrichment, and donor privilege synchronization |
| Utility | `UtilityFeature` | Kits, `/whosonline`, `/wind`, `/heal`, `/feed`, and `/fsdebug` |
| Gravestone | `GravestoneFeature` | `/whereismygrave` plus `/graveadmin` when corpse features are enabled |

## Command surface

### Teleport and movement

- `/back`
- `/sethome [name]`
- `/home [name]`
- `/delhome <name>`
- `/homes`
- `/setspawn`
- `/spawn`
- `/setwarp <name>`
- `/warp <name>`
- `/warps`
- `/delwarp <name>`
- `/rtp`
- `/tpa <player>`
- `/tpaccept`
- `/tpadeny`
- `/tpacancel`
- `/tpatoggle`
- `/setstormshelter`
- `/stormshelter`
- `/stuck`

### Discord

- `/discord`
- `/discordlink`
- `/discordunlink`

`/discord` is controlled by `Features.EnableDiscordCommand`. Discord linking commands are registered by `DiscordFeature` regardless of the `/discord` invite toggle because they are part of the account-link flow rather than the invite message command.

### Kits and utility

- `/starterkit`
- `/winterkit`
- `/whosonline`
- `/wind`
- `/heal`
- `/feed`

### Gravestones

- `/whereismygrave`
- `/graveadmin list`
- `/graveadmin giveblock <player> [quantity]`
- `/graveadmin dupeitems <graveId|currentloc> <player>`
- `/graveadmin restore <graveId|currentloc> <player>`
- `/graveadmin remove <graveId|currentloc>`
- `/graveadmin teleport <graveId|currentloc>`

`/graveadmin` requires `firststepstweaks.graveadmin` and is only registered when both corpse features and corpse admin commands are enabled.

### Debug

`/fsdebug` is an admin-only debug surface for stored state inspection and repair. Current subcommand areas include:

- `chattypes`
- `player`
- `spawn`
- `warps`
- `graves`
- `discord`

The debug surface includes player data inspection, home manipulation, playtime/join state edits, TPA preference edits, warp/spawn/grave inspection, and Discord link / reward / cursor state inspection.

## Repository structure

```text
FirstStepsTweaks/
|-- FirstStepsTweak.cs
|-- FirstStepsTweaks.csproj
|-- FirstStepsTweaks.sln
|-- modinfo.json
|-- README.md
|
|-- assets/
|   `-- firststepstweaks/
|       |-- blocktypes/
|       `-- lang/
|
|-- Commands/
|   |-- BackCommands.cs
|   |-- HomeCommands.cs
|   |-- SpawnCommands.cs
|   |-- StormShelterCommands.cs
|   |-- StuckCommand.cs
|   |-- WarpCommands.cs
|   |-- RtpCommands.cs
|   |-- TpaCommands.cs
|   |-- KitCommands.cs
|   |-- DiscordCommands.cs
|   |-- DiscordLinkCommands.cs
|   |-- GravestoneCommands.cs
|   |-- WhereIsMyGraveCommand.cs
|   |-- WhosOnlineCommand.cs
|   |-- WindCommand.cs
|   |-- AdminVitalsCommands.cs
|   `-- Debug*.cs
|
|-- Config/
|   `-- FirstStepsTweaksConfig.cs
|
|-- Discord/
|   |-- DiscordBridge.cs
|   |-- DiscordConfigStore.cs
|   |-- DiscordLinkPoller.cs
|   |-- DiscordLinkService.cs
|   |-- DiscordLinkedAccountStore.cs
|   |-- DiscordLinkRewardService.cs
|   |-- DiscordPlayerAvatarService.cs
|   |-- Messaging/
|   `-- Transport/
|
|-- Economy/
|   `-- (reserved for economy-related code; currently empty in this repo snapshot)
|
|-- Features/
|   |-- ChatFeature.cs
|   |-- DiscordFeature.cs
|   |-- FeatureRuntime.cs
|   |-- GravestoneFeature.cs
|   |-- IFeatureModule.cs
|   |-- JoinFeature.cs
|   |-- TeleportFeature.cs
|   `-- UtilityFeature.cs
|
|-- Gravestones/
|   |-- GraveBlockSynchronizer.cs
|   |-- GraveClaimPolicy.cs
|   |-- GraveInventoryRestorer.cs
|   |-- GraveInventorySnapshotter.cs
|   `-- GravePlacementService.cs
|
|-- Infrastructure/
|   |-- LandClaims/
|   |-- Messaging/
|   |-- Players/
|   `-- Teleport/
|
|-- Services/
|   |-- DonatorChatPrefixApplicator.cs
|   |-- GravestoneService.cs
|   |-- HomeAccessPolicy.cs
|   |-- HomeSlotPolicy.cs
|   |-- JoinService.cs
|   |-- JoinInvulnerabilityService.cs
|   |-- JoinMessageFormatter.cs
|   |-- KitClaimStore.cs
|   |-- KitItemConsolidator.cs
|   |-- LandClaimEscapeService.cs
|   |-- LandClaimNotificationService.cs
|   |-- PlayerDonatorRoleSyncService.cs
|   |-- PlayerHomeLimitResolver.cs
|   |-- PlayerPlaytimeStore.cs
|   |-- PlayerTeleportWarmupResolver.cs
|   |-- StormShelterTeleportService.cs
|   `-- TeleportBypass.cs
|
|-- Teleport/
|   |-- HomeStore.cs
|   |-- RtpCooldownStore.cs
|   |-- SpawnStore.cs
|   |-- StormShelterStore.cs
|   |-- TpaPreferenceStore.cs
|   |-- TpaRequestStore.cs
|   `-- WarpStore.cs
|
|-- scripts/
|   `-- CreateModZip.ps1
|
`-- Tests/
    `-- FirstStepsTweaks.Tests/
```

## Layer responsibilities

### `Features/`

Feature modules are the composition root for a functional area. A feature should:

- instantiate the small collaborators it needs
- wire event handlers and command registration
- respect feature toggles from config

Feature modules should not become hidden business-logic containers.

### `Commands/`

Command classes are the public player and admin surface. They should:

- register chat commands
- validate user input
- delegate work to services, policies, stores, or helpers
- return player-facing responses

If a command starts owning reusable rules, split that rule into a focused collaborator.

### `Services/`

Services and policy-style classes own gameplay workflows and rules. Current examples include:

- `JoinService`
- `PlayerDonatorRoleSyncService`
- `LandClaimEscapeService`
- `StormShelterTeleportService`
- `HomeAccessPolicy`
- `HomeSlotPolicy`
- `KitItemConsolidator`

### `Teleport/`

Keep teleport persistence and request state here:

- homes
- warps
- spawn data
- storm shelter state
- RTP cooldowns
- TPA preferences and pending requests

This folder should stay focused on storage and simple serialization concerns, not command registration.

### `Discord/`

Discord-only integration is isolated here:

- relay configuration and validation
- webhook transport
- message normalization and translation
- account-link state and polling
- Discord role lookup and privilege synchronization
- avatar/profile enrichment

### `Infrastructure/`

Infrastructure adapts awkward game API or external boundaries into smaller shapes the rest of the mod can use, such as player lookup, player messaging, teleport warmups, and land claim access.

## Persistence model

The project uses more than one storage style. Keep new data in the narrowest place that matches its lifetime.

### Config files

- `firststepstweaks.json` for main mod config
- `firststepstweaks.discord.json` for Discord relay and link config
- `FirstStepsTweaks.json` is migrated automatically to the lowercase main config name when found

### World save data

Used for shared server state that must survive restarts. Current examples include:

- gravestones
- warps
- spawn position
- storm shelter position
- Discord relay cursor
- Discord link cursor
- linked Discord accounts
- Discord reward claim state

### Player-scoped persisted data

Used for state tied to a specific player. Current examples include:

- homes
- starter and winter kit claim flags
- join history and last seen day
- accumulated playtime
- TPA preference

### In-memory runtime state

Used for temporary state that does not need to survive restart. Current examples include:

- `/back` locations
- active teleport warmups
- pending TPA requests
- RTP cooldown cache when implemented as runtime store

## Configuration overview

The main config object is `FirstStepsTweaksConfig` in [`Config/FirstStepsTweaksConfig.cs`](C:\Users\daytonwatson\source\repos\FirstStepsTweaks\FirstStepsTweaks\Config\FirstStepsTweaksConfig.cs).

### `Features`

Feature toggles currently include:

- `EnableDebugCommand`
- `EnableDiscordCommand`
- `EnableSpawnCommands`
- `EnableStormShelterCommands`
- `EnableStuckCommand`
- `EnableBackCommand`
- `EnableHomeCommands`
- `EnableKitCommands`
- `EnableTpaCommands`
- `EnableWarpCommands`
- `EnableRtpCommand`
- `EnableUtilityCommands`
- `EnableCorpseService`
- `EnableCorpseAdminCommands`
- `EnableJoinBroadcasts`
- `EnableLandClaimNotifications`

### `Chat`

Donator chat settings include:

- `EnableDonatorPrefixes`
- `DonatorPrefixFormat`
- `SupporterPrefix`
- `ContributorPrefix`
- `SponsorPrefix`
- `PatronPrefix`
- `FounderPrefix`

The current default format is `{tier}` and the per-tier defaults are:

- Supporter: `•S`
- Contributor: `•C`
- Sponsor: `•SP`
- Patron: `•P`
- Founder: `•F`

### `Teleport`

Teleport settings include:

- `WarmupSeconds` default `10`
- `DonatorWarmupSeconds` nullable override for donor-tier warmup reduction
- `CancelMoveThreshold` default `0.1`
- `TickIntervalMs` default `1000`
- `TpaExpireMs` default `180000`
- `HomeLimits` for `Default`, `Supporter`, `Contributor`, `Sponsor`, `Patron`, and `Founder`

`TeleportConfigUpgrader` upgrades older configs that do not yet have the current donor warmup shape.

### `Rtp`

RTP settings include:

- `MinRadius` default `256`
- `MaxRadius` default `2048`
- `MaxAttempts` default `24`
- `CooldownSeconds` default `300`
- `UsePlayerPositionAsCenter` default `true`
- `UseWarmup` default `true`

### `Join`

Join message settings include:

- `FirstJoinMessage`
- `ReturningJoinMessage`

`JoinConfigUpgrader` upgrades older returning join messages to the current default that includes playtime.

### `DiscordCommand`

- `InviteMessage`

### `Kits`

- `EnableStarterKit`
- `EnableWinterKit`
- `StarterItems`
- `WinterItems`

### `Utility`

- `HurricaneThreshold`
- `StormThreshold`
- `StrongWindThreshold`
- `BreezyThreshold`
- `AdminPlayerNames`

### `Corpse`

- `GraveBlockCode`
- `DropCleanupTickMs`
- `EnforceGraveTickMs`
- `GraveExpireMs`
- `GraveCleanupTickMs`
- `GraveCleanupInGameDays`

### `LandClaims`

- `TickIntervalMs`
- `EnterMessage`
- `ExitMessage`

## Donator tiers and chat prefixes

Current donor tier precedence is:

- `Founder`
- `Patron`
- `Sponsor`
- `Contributor`
- `Supporter`

The related privileges are:

- `firststepstweaks.supporter`
- `firststepstweaks.contributor`
- `firststepstweaks.sponsor`
- `firststepstweaks.patron`
- `firststepstweaks.founder`

`ChatFeature` applies donor prefixes on chat. `DiscordFeature` can also synchronize those donor privileges from Discord roles for linked accounts.

## Discord integration

The current Discord system is broader than a simple chat webhook.

### Relay

- `DiscordBridge` listens to in-game chat and relays it through the configured Discord webhook/client flow.
- Relay state uses a last-message cursor store so polling can resume across restarts.
- Message normalization and translation are kept in dedicated Discord classes instead of inside the feature module.

### Account linking

- `/discordlink` generates a one-time code for the player.
- Players complete the link by posting that code in the configured Discord link channel.
- `DiscordLinkPoller` reads new link-channel messages, resolves codes, and stores the linked Discord account.
- `/discordunlink` removes the linked account and clears synced donor privileges from that player.

### Reward flow

- First-time successful links are eligible for a reward.
- Reward state is persisted so the grant is not duplicated.
- `DiscordLinkRewardJoinHandler` finishes the reward claim flow when the linked player next joins / is now playing.

### Donator sync and avatars

- Linked players can have donor privileges synchronized from Discord guild roles.
- `PlayerDonatorRoleSyncService` handles role inspection and privilege updates.
- `DiscordPlayerAvatarService` and related profile clients provide avatar/profile enrichment for relay behavior where configured.

## Named homes

Homes are named and the command surface is:

- `/homes`
- `/sethome [name]`
- `/home [name]`
- `/delhome <name>`

Home limits are resolved per player tier through `Teleport.HomeLimits`.

The codebase contains focused home collaborators instead of one large home manager:

- `HomeStore` for persistence
- `DefaultHomeResolver` for default-home selection
- `HomeSlotPolicy` for slot rules
- `HomeAccessPolicy` for accessibility rules
- `HomeDeletionTargetResolver` for delete targeting
- `PlayerHomeLimitResolver` for tier-based limits

## Gravestones

The gravestone system is intentionally decomposed. The higher-level `GravestoneService` coordinates smaller components under [`Gravestones/`](C:\Users\daytonwatson\source\repos\FirstStepsTweaks\FirstStepsTweaks\Gravestones), including:

- placement
- claim policy
- inventory snapshotting
- inventory restoration
- grave block synchronization

Player-facing gravestone commands are intentionally separate from the storage and rule layers.

## Build and development

### Prerequisites

- .NET 8 SDK
- access to `VintagestoryAPI.dll`

The project resolves the API reference in this order:

1. `-p:VintagestoryApiPath=...`
2. `VINTAGESTORY_API_PATH` environment variable
3. `lib/VintagestoryAPI.dll`
4. `C:\Users\daytonwatson\Desktop\VintagestoryAPI.dll`

### Build

```powershell
dotnet build .\FirstStepsTweaks.csproj -p:VintagestoryApiPath="C:\path\to\VintagestoryAPI.dll"
```

### Test

```powershell
dotnet test .\Tests\FirstStepsTweaks.Tests\FirstStepsTweaks.Tests.csproj -p:VintagestoryApiPath="C:\path\to\VintagestoryAPI.dll"
```

### Packaging

Packaging is opt-in and runs after `dotnet publish` by calling [`scripts/CreateModZip.ps1`](C:\Users\daytonwatson\source\repos\FirstStepsTweaks\FirstStepsTweaks\scripts\CreateModZip.ps1).

Default output location:

```text
%APPDATA%\VSserverData\Mods\FirstStepsTweaks.zip
```

Useful overrides:

```powershell
dotnet publish .\FirstStepsTweaks.csproj `
  -c Release `
  -r linux-x64 `
  -p:VintagestoryApiPath="C:\path\to\VintagestoryAPI.dll" `
  -p:EnableModZipPackaging=true `
  -p:ModsFolder="C:\path\to\VSserverData\Mods"
```

Build without packaging:

```powershell
dotnet build .\FirstStepsTweaks.csproj `
  -p:VintagestoryApiPath="C:\path\to\VintagestoryAPI.dll" `
  -p:EnableModZipPackaging=false
```

On non-Windows platforms, packaging requires `pwsh` when `-p:EnableModZipPackaging=true` is used.

## Tests

The test project covers the current extracted logic surface rather than only a few legacy units. Representative tests include:

- `DefaultHomeResolverTests`
- `HomeAccessPolicyTests`
- `HomeSlotPolicyTests`
- `HomeDeletionTargetResolverTests`
- `PlayerHomeLimitResolverTests`
- `PlayerTeleportWarmupResolverTests`
- `StormShelterTests`
- `DiscordBridgeTests`
- `DiscordLinkServiceTests`
- `DiscordLinkPollerTests`
- `DiscordLinkRewardServiceTests`
- `DiscordLinkRewardJoinHandlerTests`
- `DiscordDonatorPrivilegePlannerTests`
- `DiscordRelayConfigurationValidatorTests`
- `DonatorChatMessageFormatterTests`
- `DonatorChatPrefixApplicatorTests`
- `DonatorTierResolverTests`
- `JoinConfigUpgraderTests`
- `TeleportConfigUpgraderTests`
- `JoinMessageFormatterTests`
- `PlaytimeFormatterTests`
- `LandClaimEscapePlannerTests`
- `LandClaimEscapeServiceTests`
- `LandClaimMessageFormatterTests`
- `GraveClaimPolicyTests`
- `KitItemConsolidatorTests`

Preferred testing style in this repo:

- keep rules in small pure classes
- test policies, formatters, translators, resolvers, planners, and consolidators directly
- avoid burying complex logic inside command handlers

## Architecture rules

These are the repository standards for new code.

### 1. No god classes

Do not create one class that registers commands, talks to the game API, stores data, formats messages, and owns gameplay rules.

If a class starts doing multiple unrelated jobs, split it.

### 2. One function per class means one responsibility per class

For this repository, "classes must have 1 function" means one reason to exist and one reason to change. It does not mean one method only.

Good examples in the current codebase:

- `PlayerHomeLimitResolver`
- `HomeAccessPolicy`
- `DiscordLinkService`
- `StormShelterTeleportService`
- `DonatorChatPrefixApplicator`
- `LandClaimEscapeService`

Bad direction:

- a single `TeleportManager` that stores homes, resolves limits, formats chat responses, manages warmups, and registers every teleport command

### 3. Commands stay thin

Command classes should mostly:

1. register the command
2. validate input
3. call the right collaborator
4. return a player-facing response

If command logic becomes reusable or non-trivial, extract a service, store, resolver, formatter, or policy.

### 4. Feature modules compose, they do not own domain logic

`*Feature` classes should wire dependencies and handlers. They should not become a second domain layer.

### 5. Separate persistence from behavior

Persistent state belongs in store-style classes. Gameplay rules belong in services, policies, planners, or formatters.

Examples:

- `HomeStore`, `SpawnStore`, `WarpStore`, `StormShelterStore`
- `KitClaimStore`
- `DiscordLinkedAccountStore`
- `PlayerDonatorRoleSyncService`
- `LandClaimEscapePlanner`

### 6. Put awkward engine access behind focused abstractions

When the Vintage Story API is awkward, isolate it behind a smaller boundary.

Examples already in the repo:

- `ILandClaimAccessor`
- `IPlayerMessenger`
- `IPlayerLookup`
- `ITeleportWarmupService`

### 7. Prefer small pure classes for rules and formatting

If a piece of logic can be deterministic, make it deterministic and unit-test it.

Preferred extraction targets include:

- `*Formatter`
- `*Resolver`
- `*Policy`
- `*Planner`
- `*Translator`

### 8. Keep folder boundaries meaningful

Put code where it belongs:

- command surface in `Commands/`
- composition in `Features/`
- gameplay workflows and rules in `Services/`
- persistence in `Teleport/` or other store-focused areas
- Discord-only code in `Discord/`
- engine and boundary adapters in `Infrastructure/`

Do not add vague catch-all folders or utility blobs.

### 9. Prefer additive extension over bloating unrelated files

When adding a feature:

- add a new focused collaborator instead of expanding an already broad class
- add a new command class when the command surface is distinct
- add or extend a feature module when the behavior is a separate runtime area

### 10. Test extracted logic

When a rule is split out of a command or service into a pure class, strongly prefer adding or extending unit tests for it.

## Known code and repo notes

- Document active behavior from feature registration and runtime wiring, not from class names alone.
- `LandClaimNotificationService` self-registers its tick listener in its constructor. It is active when constructed by `JoinFeature` with `EnableLandClaimNotifications`, even though `JoinFeature.Register()` does not explicitly call a registration method on it.
- The `Economy/` folder exists in this repo snapshot but is currently empty.
- This README intentionally describes the current code and runtime behavior, not planned or partially started systems.
