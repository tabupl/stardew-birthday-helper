// Adapted from Lookup Anything's WorldItemScanner (MIT, Pathoschild).
// See THIRD_PARTY_NOTICES.md. Adds source labels and isolates broken mod containers.
using BirthdayPlanner.Core;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace BirthdayPlanner.Services;

internal sealed class OwnedItemScanner
{
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly HashSet<string> warned = new();
    public OwnedItemScanner(IModHelper helper, IMonitor monitor) => (this.helper, this.monitor) = (helper, monitor);

    public List<GiftStock<Item>> Scan()
    {
        var found = new List<GiftStock<Item>>();
        var seen = new HashSet<Item>(ReferenceEqualityComparer.Instance);
        foreach (GameLocation location in GetLocations())
        {
            string name = LocationLabel(location);
            RunSafely(name, () =>
            {
                foreach (Furniture furniture in location.furniture)
                    ScanItem(furniture, name + " / " + furniture.DisplayName, false, true, seen, found);
                Chest? fridge = location switch { FarmHouse house => house.fridge.Value, IslandFarmHouse house => house.fridge.Value, _ => null };
                ScanItem(fridge, name + " / " + T("source.fridge"), false, false, seen, found, false);
                foreach (NPC npc in location.characters)
                {
                    Hat? hat = (npc as Child)?.hat.Value ?? (npc as Horse)?.hat.Value;
                    ScanItem(hat, name, false, false, seen, found);
                }
                foreach (Building building in location.buildings)
                {
                    if (building is JunimoHut hut)
                        ScanItem(hut.GetOutputChest(), name + " / " + T("source.junimo"), false, false, seen, found, false);
                    foreach (Chest chest in building.buildingChests)
                        ScanItem(chest, name + " / " + ChestLabel(chest), false, false, seen, found, false);
                }
                foreach (SObject item in location.objects.Values)
                {
                    if (item is Chest || !(item.IsSpawnedObject || item.isForage() || item.Category == SObject.litterCategory))
                    {
                        string source = name + " / " + (item is Chest chest ? ChestLabel(chest) : item.DisplayName);
                        if (item is Chest) source += $" ({(int)item.TileLocation.X}, {(int)item.TileLocation.Y})";
                        ScanItem(item, source, false, true, seen, found);
                    }
                }
            });
        }
        RunSafely("inventory", () =>
        {
            foreach (Item? item in Game1.player.Items.Concat(new Item?[] {
                Game1.player.shirtItem.Value, Game1.player.pantsItem.Value, Game1.player.boots.Value,
                Game1.player.hat.Value, Game1.player.leftRing.Value, Game1.player.rightRing.Value }))
                ScanItem(item, T("source.inventory"), true, false, seen, found);
        });
        int hayCount = Game1.getFarm()?.piecesOfHay.Value ?? 0;
        if (hayCount > 0)
        {
            Item hay = ItemRegistry.Create("(O)178");
            hay.Stack = hayCount;
            ScanItem(hay, T("source.silo"), false, false, seen, found);
        }
        return found;
    }

    internal static IEnumerable<GameLocation> GetLocations() => Game1.locations
        .Concat(Game1.locations.SelectMany(p => p.GetInstancedBuildingInteriors()))
        .Distinct(ReferenceEqualityComparer.Instance).Cast<GameLocation>();

    internal static string VariantKey(Item item) => item.QualifiedItemId + "|" +
        (item is SObject obj ? obj.preservedParentSheetIndex.Value : "") + "|" + item.Name;

    internal static string LocationLabel(GameLocation? location) =>
        location == null ? "?" : string.IsNullOrWhiteSpace(location.DisplayName) ? location.Name : location.DisplayName;

    private string ChestLabel(Chest chest) => chest.modData.TryGetValue("Pathoschild.ChestsAnywhere/Name", out string? name) && !string.IsNullOrWhiteSpace(name)
        ? name : T("source.chest");
    private string T(string key) => helper.Translation.Get(key).ToString();

    private void ScanItem(Item? item, string source, bool inventory, bool inWorld, HashSet<Item> seen, List<GiftStock<Item>> found, bool includeRoot = true, object? parent = null)
    {
        if (item == null || !seen.Add(item)) return;
        if (includeRoot)
        {
            int count = Math.Max(1, item.Stack);
            if (parent is Fence && item is Torch && count == 93) count = 1;
            found.Add(new(VariantKey(item), item.DisplayName, item, item.Quality, count, source, inventory));
        }
        RunSafely(item.GetType().FullName ?? item.Name, () =>
        {
            foreach (Item? child in Contents(item, inWorld))
                ScanItem(child, source, inventory, false, seen, found, parent: item);
        });
    }

    private IEnumerable<Item?> Contents(Item root, bool inWorld)
    {
        if (root is SObject obj)
        {
            if (obj.MinutesUntilReady <= 0 || obj is Cask) yield return obj.heldObject.Value;
        }
        else if (!(root.GetType().Namespace ?? "").StartsWith("StardewValley", StringComparison.Ordinal))
        {
            Item? held = helper.Reflection.GetField<Item>(root, "heldObject", false)?.GetValue()
                ?? helper.Reflection.GetProperty<Item>(root, "heldObject", false)?.GetValue();
            if (held != null) yield return held;
        }
        switch (root)
        {
            case StorageFurniture storage:
                foreach (Item item in storage.heldItems) yield return item;
                break;
            case Chest chest when !inWorld || chest.playerChest.Value:
                foreach (Item item in chest.GetItemsForPlayer(Game1.player.UniqueMultiplayerID)) yield return item;
                break;
            case Tool tool:
                foreach (SObject item in tool.attachments) yield return item;
                break;
        }
    }

    private void RunSafely(string context, Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            if (warned.Add(context)) monitor.Log($"Couldn't scan {context}; other storage is still included. {ex.Message}", LogLevel.Warn);
        }
    }
}
