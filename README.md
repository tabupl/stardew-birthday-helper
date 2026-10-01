# Birthday Planner

A SMAPI popup for managing birthday gifts across vanilla and modded Stardew Valley
characters. It reads active character data and evaluates concrete owned items using
the game's gift-taste function, including modded items, category tastes, and exceptions.

Press **Left Control + B** to open it. Browse individual dates with arrows or click
the date to choose a season and day. **Today** returns to the game date. The selected
date persists per farm and player in the mod's `data` directory.

Each character has a portrait, home location, and up to five owned gift suggestions.
Loved gifts take priority; liked gifts appear only if no loved gift is available.
Iridium, gold, silver, and normal variants remain distinct and sort in that order.
Matching stacks are combined. Hover a gift for quantities and storage locations,
including Chests Anywhere chest names and tile coordinates. Characters with no
matching gift remain visible. Multiple birthdays use character pages on the same date.

**F1 on a portrait or gift** opens Lookup Anything when installed; closing its view
returns to the planner. The integration uses its documented `HoveredNpc` and
`HoveredItem` convention. Generic Mod Config Menu provides shortcut, unmet-character,
and date-persistence options. English and Polish UI text is included.

## Install

Extract `dist/BirthdayPlanner-1.0.0.zip` and copy `BirthdayPlanner` to your game’s
`Mods` directory. See [INSTALL.txt](INSTALL.txt). The same package is intended for
Mac, Windows, and Linux; it contains platform-independent .NET 6 assemblies.
It adds no NPCs, objects, locations, or serialized save types.

## Storage and multiplayer

The scanner adapts Lookup Anything's MIT-licensed approach. It scans the current
player's inventory, chests (including per-player chest inventories), farmhouse and
island fridges, building storage, dressers, nested held items, tool attachments,
retrievable machine output, and silo hay. It excludes other players' inventories,
spawned forage, ground debris, and output still processing (casks are retrievable).
Source locations and quantities are shown without moving, reserving, or gifting items.

The scanner operates on the world state visible to the local client, like Lookup
Anything. A guest does not need a special host-side companion. Remote locations
may have visibility or synchronization limitations; Refresh reads the available
state again. Unknown storage subclasses may need support. Each player installs
the mod if they want its UI; this mod sends no network messages.

Birthdays include currently active, giftable NPCs with valid birthday dates,
including characters not yet met by default. NPCs not spawned/unlocked in the current
save, or intentionally unable to receive gifts, are excluded. Home labels use the
character's default home and the game's location display name; some mods provide
internal-looking labels. Future-date browsing evaluates currently loaded tastes
and current stock, not predicted future stock or future conditional preferences.

## Build

Install a .NET SDK that can target .NET 6 (the .NET 6 SDK includes its targeting pack),
Stardew Valley, and SMAPI. No NuGet packages are required. From this repository:

```sh
python3 scripts/build.py
```

If the Steam directory is elsewhere:

```sh
python3 scripts/build.py --game-path "/path/to/game/executable/directory"
```

Use `--dotnet /path/to/dotnet` for a non-global SDK. The build script runs the
independent core regression tests, compiles against installed game assemblies,
creates the mod folder and ZIP under `dist`, verifies ZIP integrity, and writes
SHA-256 hashes. It does not install into the game or copy the game into the package.

## Verification

The core tests cover year/season navigation, invalid dates, loved-first fallback,
quality ordering, matching stack aggregation, flavor distinctions, inventory priority,
and the five-suggestion limit. In SMAPI's console:

```text
birthday_planner spring 13
birthday_planner_verify
```

The read-only diagnostic enumerates birthdays and recommendations for all 112 dates,
and compares owned gift stock with the installed Lookup Anything scanner when available.
It writes `verification/runtime-report.json` inside the mod folder. Diagnostic reflection
is optional and isolated from normal planner functionality.

See [verification/RESULTS.md](verification/RESULTS.md) for actual checks and remaining
limits. Isolated runtime fixtures and generated logs are excluded from Git and release ZIPs.

## License

MIT. See [LICENSE](LICENSE) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for
Lookup Anything attribution.
