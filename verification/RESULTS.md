# Verification results

Checked on 1 October 2026 against Stardew Valley 1.6.15 (build 24356), SMAPI 4.5.2,
Lookup Anything 1.55.0, and the installed mod pack on macOS. The isolated session
loaded 96 code mods and 150 content packs, including the planner and test driver.
Save Backup and Daily Screenshot were excluded from the copied test pack.

## Build and automated checks

- Release build: zero warnings and zero errors against installed game assemblies.
- Core regression suite: 131 passing assertions covering all 112 recurring dates,
  year/season boundaries, loved-first selection, liked fallback, real quality ordering,
  matching-stack totals, flavor separation, inventory tie preference, and five-gift limit.
- ZIP integrity checked; package includes only two portable mod DLLs, manifest,
  English/Polish translations, installation instructions, and license notices.
- English and Polish translations have the same 34 keys.

## Runtime

- SMAPI loads the planner and registers its Generic Mod Config Menu options.
- Read-only diagnostics enumerate 176 active, giftable birthday NPCs across all 112 dates.
- Gift scanning matches the installed Lookup Anything scanner's item/quality totals,
  both before and after adding inventory gifts and a nested test chest.
- Spring 14 has four modded birthday characters across two character pages. Hit-box
  checks keep every gift tile inside the content area on both pages.
- Keyboard date arrows change one day and round-trip correctly. The date picker selects
  Summer 21, and Today returns to the actual game date (Spring 2).
- Closing/reopening remembers the selected date; the selection also survives restarting
  the game and reloading the copied save. Date data is stored in the mod folder.
- F1 lookup paths open both the modded NPC Aster and a Prismatic Shard. Closing
  Lookup Anything restores the planner. Public hover fields support the installed
  Lookup Anything 1.55.0 as well as its newer documented convention.
- All 176 recommendation lists stay within five gifts and sort quality descending;
  seven characters use liked-gift fallback with the fixture stock.
- Visual inspection confirms the popup, portraits, home labels, empty-date state,
  character pages, and owned gift tiles. Icons preserve item quality and quantities.

See [runtime-summary.json](runtime-summary.json) for recorded counts and assertions.

## Scope and remaining limits

Testing used a copied local test save under `test-session/config`; fixtures were
added only in memory, and the process was exited without saving. The normal game
Mods folder and original saves were not edited. The test driver and copied game data
are excluded from Git and the release ZIP.

A Windows runtime and a real LAN host/guest session have not been tested. The release
uses portable .NET 6 assemblies and no native libraries or networking code. Ownership
uses the current player and the world state visible to that client, following Lookup
Anything's approach. Unloaded or unsynchronized remote storage and unusual custom
containers may require further support. NPCs absent or not yet unlocked in a save
are intentionally excluded.

The diagnostic command's optional reflection is used only for verification. Normal
birthday/gift scanning does not call Lookup Anything's private code.
