using StardewModdingAPI;
using StardewValley;

namespace BirthdayPlanner.Services;

/// <summary>Find the outdoor area containing a home, rather than naming its room or house.</summary>
internal sealed class HomeAreaResolver
{
    private readonly ITranslationHelper text;
    private readonly Dictionary<string, string> cache = new(StringComparer.Ordinal);

    public HomeAreaResolver(ITranslationHelper text) => this.text = text;
    public void Clear() => cache.Clear();

    public string GetArea(NPC npc)
    {
        string? home = npc.DefaultMap;
        if (string.IsNullOrWhiteSpace(home))
            home = npc.GetData()?.Home?.FirstOrDefault(p => GameStateQuery.CheckConditions(p.Condition))?.Location;
        if (string.IsNullOrWhiteSpace(home)) return UnknownArea();
        if (cache.TryGetValue(home, out string? area)) return area;

        GameLocation? start = Game1.getLocationFromName(home);
        var pending = new Queue<GameLocation>();
        var visited = new HashSet<GameLocation>(ReferenceEqualityComparer.Instance);
        if (start != null) pending.Enqueue(start);
        while (pending.TryDequeue(out GameLocation? location))
        {
            if (!visited.Add(location)) continue;
            if (location.IsOutdoors)
                return cache[home] = OwnedItemScanner.LocationLabel(location);

            // Instanced farm buildings may not have a conventional exit warp.
            GameLocation? parent = location.ParentBuilding?.GetParentLocation();
            if (parent != null) pending.Enqueue(parent);
            foreach (Warp warp in location.warps)
            {
                GameLocation? target = Game1.getLocationFromName(warp.TargetName);
                if (target != null && !visited.Contains(target)) pending.Enqueue(target);
            }
        }
        // Don't mislabel a modded village as Pelican Town based on HomeRegion="Town".
        return cache[home] = UnknownArea();
    }

    private string UnknownArea() => text.Get("character.unknown-area").ToString();
}
