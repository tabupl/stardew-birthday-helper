# Birthday Planner

A SMAPI popup for managing birthday gifts across vanilla and modded Stardew Valley
characters. It reads active character data and evaluates concrete owned items using
the game's gift-taste function, including modded items, category tastes, and exceptions.

[Download the latest mod ZIP](https://github.com/tabupl/stardew-birthday-helper/releases/latest/download/BirthdayPlanner-1.0.2.zip)
· [Releases](https://github.com/tabupl/stardew-birthday-helper/releases)

Press **Left Control + B** to open it. Browse individual dates with arrows or click
the date to choose a season and day. **Today** returns to the game date. The selected
date persists per farm and player in the mod's `data` directory.

Each character has a portrait, home area, and up to five owned gift suggestions.
Each header shows your current hearts and the game’s current limit, with a green
**Max hearts** badge when reached: normally 8 for undated romanceable NPCs, 10 for
other friendships or dating, and 14 for spouses. This uses the game’s own limit
function and your local player’s friendship; gifts remain available at the cap.
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

Requires **Stardew Valley 1.6.15+** and **SMAPI 4.3.1+**.

1. Close Stardew Valley.
2. Download `BirthdayPlanner-1.0.2.zip` from [GitHub Releases](https://github.com/tabupl/stardew-birthday-helper/releases/latest).
   Use the mod ZIP asset; the automatically generated source-code archives are for developers.
3. Extract the ZIP and copy its `BirthdayPlanner` folder into your game's `Mods` folder.
   The resulting path should be `Mods/BirthdayPlanner/manifest.json`.
4. Start Stardew Valley through SMAPI, load your farm, and press **Left Control + B**.

On Windows, Steam → Stardew Valley → Manage → Browse local files opens the game folder.
Copy the extracted folder into `Mods` there. The same ZIP is intended for Mac, Windows,
and Linux; it contains platform-independent .NET 6 assemblies. See [INSTALL.txt](INSTALL.txt)
for controls and optional integrations. This mod adds no NPCs, objects, locations,
or serialized save types.

For an update, replace the mod's DLLs, manifest, translations, and documentation with
the new package. Keep `config.json` and `data/` if you want to retain settings and the
last selected date. Install it on each computer whose player wants the popup.

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
save, or intentionally unable to receive gifts, are excluded. Home areas follow exit
warps from the character's default home to the nearest outdoor
map (including multi-floor homes and farm buildings), using the game's translated map
name. A home with no resolvable outdoor exit shows Unknown area. Future-date browsing
evaluates current friendship, currently loaded tastes,
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
