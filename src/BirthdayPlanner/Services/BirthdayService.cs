using BirthdayPlanner.Core;
using StardewModdingAPI;
using StardewValley;

namespace BirthdayPlanner.Services;

internal sealed record BirthdayCard(NPC Npc, string Home, bool GiftedToday, int Hearts, int MaxHearts, GiftSelection<Item> Selection)
{
    public bool AtMaxHearts => MaxHearts > 0 && Hearts >= MaxHearts;
}

internal sealed class BirthdayService
{
    private readonly OwnedItemScanner scanner;
    private readonly HomeAreaResolver homeAreas;
    private readonly IMonitor monitor;
    private readonly HashSet<string> warned = new();
    public IReadOnlyList<GiftStock<Item>> Stock { get; private set; } = Array.Empty<GiftStock<Item>>();
    public IReadOnlyList<NPC> Characters { get; private set; } = Array.Empty<NPC>();

    public BirthdayService(IModHelper helper, IMonitor monitor)
    {
        scanner = new OwnedItemScanner(helper, monitor);
        homeAreas = new HomeAreaResolver(helper.Translation);
        this.monitor = monitor;
    }

    public void Refresh(bool includeUnmet)
    {
        homeAreas.Clear();
        Stock = scanner.Scan().Where(p => p.Item.canBeGivenAsGift()).ToArray();
        Characters = Utility.getAllCharacters()
            .Where(npc => SafeNpc(npc, includeUnmet))
            .GroupBy(npc => npc.Name, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(npc => npc.displayName, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public List<BirthdayCard> GetCards(CalendarDate date) => Characters
        .Where(npc => string.Equals(npc.Birthday_Season, date.Season, StringComparison.OrdinalIgnoreCase) && npc.Birthday_Day == date.Day)
        .Select(npc => new BirthdayCard(npc, GetHome(npc),
            Game1.player.friendshipData.TryGetValue(npc.Name, out Friendship? friendship) && friendship.GiftsToday > 0,
            Game1.player.getFriendshipHeartLevelForNPC(npc.Name), Utility.GetMaximumHeartsForCharacter(npc),
            GiftRanker.Select(Stock, item => GetTaste(npc, item)))).ToList();

    public static CalendarDate Today() => new(Utility.getSeasonNumber(Game1.currentSeason), Game1.dayOfMonth);

    private bool SafeNpc(NPC npc, bool includeUnmet)
    {
        try
        {
            return npc.IsVillager && npc.CanReceiveGifts() &&
                (includeUnmet || Game1.player.friendshipData.ContainsKey(npc.Name)) &&
                CalendarDate.TryCreate(Utility.getSeasonNumber(npc.Birthday_Season ?? ""), npc.Birthday_Day, out _);
        }
        catch (Exception ex) { Warn(npc.Name, ex); return false; }
    }

    private GiftPreference GetTaste(NPC npc, Item item)
    {
        try
        {
            return npc.getGiftTasteForThisItem(item) switch { 0 => GiftPreference.Love, 2 => GiftPreference.Like, _ => GiftPreference.Other };
        }
        catch (Exception ex) { Warn(npc.Name + "/" + item.QualifiedItemId, ex); return GiftPreference.Other; }
    }

    private string GetHome(NPC npc)
    {
        try { return homeAreas.GetArea(npc); }
        catch (Exception ex)
        {
            Warn(npc.Name + "/home-area", ex);
            return "?";
        }
    }

    private void Warn(string context, Exception ex)
    {
        if (warned.Add(context)) monitor.Log($"Couldn't read birthday/gift data for {context}: {ex.Message}", LogLevel.Warn);
    }
}
