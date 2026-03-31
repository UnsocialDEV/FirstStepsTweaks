# FirstStepsTweaks

FirstStepsTweaks is a server-side Vintage Story mod that groups together a set of practical multiplayer quality-of-life features for a server. The codebase is organized around feature modules, small focused services, and thin command handlers instead of one large mod class.

## What the mod includes

- Teleport quality-of-life commands: `/back`, `/homes`, `/home [name]`, `/sethome [name]`, `/delhome <name>`, `/spawn`, `/setspawn`, `/warp`, `/warps`, `/setwarp`, `/delwarp`, `/rtp`, `/tpa`, `/tpaccept`, `/tpadeny`, `/tpacancel`, `/tpatoggle`
- Join and return messages
- Donator chat prefixes with tier precedence
- Join-time invulnerability handling
- Land claim enter/exit notifications
- Discord chat relay and `/discord`
- Starter and winter kits
- Gravestone creation, recovery, lookup, and admin tools
- Utility commands such as `/wind`, `/whosonline`, `/heal`, `/feed`, and `/fsdebug`

## Tech stack

- C#
- .NET 8
- Vintage Story server mod API
- xUnit for unit tests
- PowerShell packaging script for publish output zip generation

## Runtime overview

The server entry point is [`FirstStepsTweak.cs`](./FirstStepsTweak.cs), which contains the `FirstStepsTweaks : ModSystem` class.

At startup the mod does the following:

1. Loads `firststepstweaks.json`, or migrates the legacy `FirstStepsTweaks.json` if it exists.
2. Builds a shared `FeatureRuntime` object with reusable cross-feature dependencies.
3. Registers privileges used by commands and gated features.
4. Registers feature modules:
   - `JoinFeature`
   - `TeleportFeature`
   - `DiscordFeature`
   - `UtilityFeature`
   - `GravestoneFeature` when gravestones are enabled

This keeps the entry point small and makes each feature responsible for its own event wiring and command registration.

## Repository structure

```text
FirstStepsTweaks/
|-- FirstStepsTweak.cs                 # ModSystem entry point and feature bootstrap
|-- FirstStepsTweaks.csproj            # Main mod project
|-- FirstStepsTweaks.sln               # Solution file
|-- modinfo.json                       # Vintage Story mod metadata
|-- README.md                          # Project documentation
|
|-- assets/                            # Game assets copied into the final mod zip
|   `-- firststepstweaks/
|       |-- blocktypes/
|       |-- itemtypes/
|       |-- lang/
|       `-- recipes/
|
|-- Commands/                          # Chat command endpoints and command-specific orchestration
|   |-- BackCommands.cs
|   |-- HomeCommands.cs
|   |-- SpawnCommands.cs
|   |-- WarpCommands.cs
|   |-- RtpCommands.cs
|   |-- TpaCommands.cs
|   |-- KitCommands.cs
|   |-- DiscordCommands.cs
|   |-- GravestoneCommands.cs
|   |-- WhereIsMyGraveCommand.cs
|   |-- WhosOnlineCommand.cs
|   |-- WindCommand.cs
|   |-- AdminVitalsCommands.cs
|   `-- DebugCommands.cs
|
|-- Config/                            # Strongly typed config models and feature toggles
|   `-- FirstStepsTweaksConfig.cs
|
|-- Features/                          # Feature composition root layer
|   |-- IFeatureModule.cs
|   |-- FeatureRuntime.cs
|   |-- JoinFeature.cs
|   |-- TeleportFeature.cs
|   |-- DiscordFeature.cs
|   |-- UtilityFeature.cs
|   `-- GravestoneFeature.cs
|
|-- Discord/                           # Discord-specific integration
|   |-- DiscordBridge.cs
|   |-- DiscordBridgeConfig.cs
|   |-- DiscordConfigStore.cs
|   |-- DiscordLastMessageStore.cs
|   |-- Messaging/
|   `-- Transport/
|
|-- Gravestones/                       # Gravestone sub-components with narrow responsibilities
|   |-- GraveBlockSynchronizer.cs
|   |-- GraveClaimPolicy.cs
|   |-- GraveInventoryRestorer.cs
|   |-- GraveInventorySnapshotter.cs
|   `-- GravePlacementService.cs
|
|-- Infrastructure/                    # Wrappers around game API and low-level adapters
|   |-- LandClaims/
|   |-- Messaging/
|   |-- Players/
|   `-- Teleport/
|
|-- Services/                          # Feature workflows, business rules, repositories, formatters
|   |-- GravestoneService.cs
|   |-- GraveManager.cs
|   |-- JoinService.cs
|   |-- JoinInvulnerabilityService.cs
|   |-- JoinMessageFormatter.cs
|   |-- LandClaimNotificationService.cs
|   |-- LandClaimMessageFormatter.cs
|   |-- ItemService.cs
|   |-- KitClaimStore.cs
|   |-- KitItemConsolidator.cs
|   `-- TeleportBypass.cs
|
|-- Teleport/                          # Teleport persistence and request state
|   |-- HomeStore.cs
|   |-- SpawnStore.cs
|   |-- WarpStore.cs
|   |-- RtpCooldownStore.cs
|   |-- TpaPreferenceStore.cs
|   |-- TpaRequestStore.cs
|   `-- TpaRequestRecord.cs
|
|-- scripts/                           # Build and packaging scripts
|   `-- CreateModZip.ps1
|
`-- Tests/
    `-- FirstStepsTweaks.Tests/        # xUnit test project for pure logic and policies
```

## Layer responsibilities

### `Features/`

Feature modules are the composition root for a functional area. A feature should:

- instantiate the services and commands it needs
- wire event handlers
- respect feature toggles from config

A feature should not become the place where business rules live.

### `Commands/`

Command classes are the public command surface for players and admins. They should:

- register chat commands
- validate command input
- delegate real work to services, stores, or helpers
- format command success/error responses

They should stay thin. If command logic becomes reusable or non-trivial, move it into a service or policy class.

### `Services/`

Services own gameplay workflows and business behavior. Good examples in this repository:

- `JoinService` for join/return message flow
- `LandClaimNotificationService` for land-claim transition behavior
- `GravestoneService` for higher-level gravestone orchestration
- `KitItemConsolidator` and formatter classes for pure logic that can be tested independently

### `Gravestones/`

This folder breaks the gravestone system into smaller roles instead of letting `GravestoneService` do everything itself. Examples:

- placement
- claim policy
- inventory snapshotting
- inventory restoration
- block synchronization

That split is the preferred direction for feature-heavy code throughout this repository.

### `Infrastructure/`

Infrastructure classes adapt the Vintage Story API or external boundaries into shapes the rest of the mod can use. Examples:

- player lookup
- player messaging
- land claim access through reflection
- teleport warmup timer handling

If a class mostly exists because the game API or an external dependency is awkward, it belongs here.

### `Teleport/`

This folder contains teleport state and storage types. Keep it focused on persistence and request state, not on command wiring or message formatting.

### `Discord/`

Discord integration is isolated from the rest of the mod:

- config loading lives in config store classes
- transport code lives under `Transport/`
- translation/parsing lives under `Messaging/`
- the bridge coordinates the end-to-end Discord relay flow

## Current feature map

| Feature | Main entry point | Main responsibilities |
|---|---|---|
| Join | `JoinFeature` | Join broadcasts, return messaging, join invulnerability, land claim notifications |
| Chat | `ChatFeature` | Donator chat prefix formatting and chat event wiring |
| Teleport | `TeleportFeature` | Back/home/spawn/warp/rtp/tpa commands, warmups, request and cooldown state |
| Discord | `DiscordFeature` | Game-to-Discord relay, Discord-to-game relay, Discord invite command |
| Utility | `UtilityFeature` | Kits, online listing, wind reporting, admin heal/feed, debug helpers |
| Gravestone | `GravestoneFeature` | Item snapshot/recovery, grave placement, claim policy, grave admin commands |

## Persistence model

The project uses more than one storage style. Keep new data in the narrowest place that matches its lifetime.

### Config files

- `firststepstweaks.json` for main mod config
- `firststepstweaks.discord.json` for Discord relay config
- legacy config file names are migrated automatically

### World save data

Used for shared server state that must survive restarts. Examples:

- gravestones
- warp definitions
- spawn position
- Discord last processed message id

### Player moddata

Used for player-scoped persistent state. Examples:

- named home positions with legacy single-home migration
- starter/winter kit claims
- join history and last seen day
- TPA enable/disable preference

### In-memory runtime state

Used for temporary state that does not need to survive a restart. Examples:

- `/back` last-location cache
- RTP cooldown tracking
- pending TPA requests
- active teleport warmups

## Configuration overview

The main config object is `FirstStepsTweaksConfig`. Major sections are:

- `Features`: feature toggles and high-level enable flags
- `Chat`: donor chat prefix toggle and prefix format
- `Teleport`: warmup timing, movement cancel threshold, TPA expiration, and per-tier home limits
- `Rtp`: radius, attempts, cooldown, center selection
- `Join`: first-join and returning-player message templates
- `DiscordCommand`: text for the `/discord` command
- `Kits`: enabled kits and item lists
- `Utility`: wind thresholds and admin name list
- `Corpse`: gravestone settings
- `LandClaims`: enter/exit notification settings

## Donator chat prefixes

Donator chat prefixes are controlled by the `Chat` config section:

- `EnableDonatorPrefixes`: enables the in-game donor prefix hook
- `DonatorPrefixFormat`: prefix template, defaulting to `[{tier}]`

Tier precedence is:

- `Founder`
- `Patron`
- `Sponsor`
- `Contributor`
- `Supporter`

Privileges used for donor chat prefixes:

- `firststepstweaks.supporter`
- `firststepstweaks.contributor`
- `firststepstweaks.sponsor`
- `firststepstweaks.patron`
- `firststepstweaks.founder`

Discord donor sync manages those donor privileges directly. It does not replace the player's base game role, so admin and other custom roles remain intact.

When adding config:

- add it to the correct section instead of dumping everything into one giant class
- keep related values together
- use sensible defaults so a missing setting does not break startup
- add a feature toggle when the behavior is optional

## Named homes

Homes are now named and the command surface is:

- `/homes`
- `/sethome [name]`
- `/home [name]`
- `/delhome <name>`

Home names are normalized case-insensitively. Existing legacy single-home player data is migrated automatically to the named home `home` the first time the player accesses homes.

If a player has no homes yet, bare `/sethome` creates a default home named `home`. Bare `/home` teleports to the `home` entry when present, otherwise it falls back to the player's oldest created home. `/homes` marks whichever home bare `/home` will use as `(default)`.

If a player's donor tier is downgraded, extra homes are kept in storage but become inaccessible until the tier supports them again. `/homes` shows which homes are currently accessible and which are stored but locked by the current tier.

Home limits are controlled by `Teleport.HomeLimits`:

- `Default`
- `Supporter`
- `Contributor`
- `Sponsor`
- `Patron`
- `Founder`

Default config values increase by tier, but admins can customize each tier independently.

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

Packaging is opt-in and runs after `dotnet publish` by calling [`scripts/CreateModZip.ps1`](./scripts/CreateModZip.ps1).

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

On non-Windows platforms, packaging requires `pwsh` to be available when `-p:EnableModZipPackaging=true` is used.

## Tests

The test project currently focuses on logic that should stay deterministic and API-light:

- `DiscordMessageTranslatorTests`
- `GraveClaimPolicyTests`
- `JoinMessageFormatterTests`
- `KitItemConsolidatorTests`
- `LandClaimMessageFormatterTests`

This is the preferred testing style for the repository:

- keep pure rules in small classes
- test formatters, translators, policies, and consolidators directly
- avoid hiding complex logic inside command handlers where unit testing becomes harder

## Architecture rules

These rules are the standard for new code in this repository.

### 1. No god classes

Do not create classes that register commands, manage storage, talk to the game API, format player messages, and implement business rules all at once.

If a class starts doing multiple unrelated jobs, split it.

### 2. One function per class means one responsibility per class

For this project, "classes must have 1 function" means one reason to exist and one reason to change. It does **not** mean a class may only contain one method.

Good:

- `JoinMessageFormatter` formats join messages
- `WarpStore` persists warp data
- `TeleportWarmupService` manages timed teleport warmups
- `GraveClaimPolicy` decides whether a grave can be claimed

Bad:

- one large `TeleportManager` that stores homes, validates permissions, formats responses, handles warmups, and registers every teleport command

### 3. Commands stay thin

Command classes should mostly do four things:

1. register the command
2. validate input
3. call the right collaborator
4. return a player-facing response

If a command needs reusable logic, extract a service, policy, formatter, or store.

### 4. Feature modules compose, they do not own domain logic

`*Feature` classes should wire together dependencies and register handlers. They should not become a second command layer or a hidden business logic layer.

### 5. Put game API wrappers behind focused abstractions

When the Vintage Story API is awkward, reflection-heavy, or hard to test, isolate it behind a small adapter interface.

Examples already in the project:

- `ILandClaimAccessor`
- `IPlayerMessenger`
- `IPlayerLookup`
- `ITeleportWarmupService`

### 6. Separate persistence from behavior

Persistent state belongs in a `Store` or repository-style class. Business behavior belongs in a service or policy class.

Examples:

- `WarpStore` stores warps
- `KitClaimStore` stores player claim flags
- `GraveManager` stores gravestone records

Those classes should not also decide gameplay rules.

### 7. Prefer small pure classes for rules and formatting

If a piece of logic can be made deterministic, make it deterministic and test it in isolation.

Preferred extraction targets:

- `*Formatter`
- `*Translator`
- `*Policy`
- `*Consolidator`

### 8. Keep folder boundaries meaningful

Put code where it belongs:

- command surface in `Commands/`
- composition in `Features/`
- gameplay workflows in `Services/`
- external or engine adapters in `Infrastructure/`
- persistence in `Teleport/` or other store-focused folders
- Discord-only code in `Discord/`

Do not add "misc", "helpers", or catch-all folders unless there is a clear architectural reason.

### 9. Prefer additive extension over editing unrelated classes

When adding a feature:

- add a new service instead of bloating an existing one
- add a new command class instead of stuffing more commands into an unrelated command file
- add a new feature module when the behavior is a distinct functional area

### 10. Test the logic you extract

Every time you split a rule out of a command or service into a pure class, strongly prefer adding or extending unit tests.

## Recommended workflow for adding a new feature

1. Add or extend config in `Config/` with sensible defaults.
2. Create the smallest focused service, policy, formatter, or store classes needed.
3. Add a new command class only if there is a player/admin command surface.
4. Register the new behavior from a feature module.
5. Wire the feature into `FirstStepsTweak.cs`.
6. Add unit tests for pure logic.
7. Update this README if the repository structure or architecture guidance changes.

## Naming guidance

Keep names explicit. Prefer names that reveal role:

- `SomethingFeature`
- `SomethingCommands`
- `SomethingService`
- `SomethingStore`
- `SomethingFormatter`
- `SomethingPolicy`
- `IWhatever` for abstractions

Avoid vague names such as:

- `Manager`
- `Helper`
- `Utils`
- `Processor`

Use those only when the class truly matches the role and there is no more precise name.

## Maintenance notes

- Keep the startup path in `FirstStepsTweak.cs` small.
- Keep feature toggles centralized in config.
- Keep Discord-specific concerns out of unrelated gameplay classes.
- Keep gravestone logic decomposed instead of growing one monolith.
- Prefer new small collaborators over extending an already-large class.

That architectural discipline matters more than saving a file or two. Small, single-purpose classes are the default design choice for this repository.
