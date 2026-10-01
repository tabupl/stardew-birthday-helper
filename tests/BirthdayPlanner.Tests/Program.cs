using BirthdayPlanner.Core;

int assertions = 0;
void Check(bool condition, string name)
{
    assertions++;
    if (!condition) throw new Exception("FAIL: " + name);
}
GiftStock<string> Stock(string id, int quality = 0, int count = 1, bool bag = false, string source = "Chest", string? name = null) =>
    new(id, name ?? id, id, quality, count, source, bag);

// Birthdays must wrap cleanly across seasons and years, in both directions.
Check(new CalendarDate(3, 28).Shift(1) == new CalendarDate(0, 1), "Winter 28 -> Spring 1");
Check(new CalendarDate(0, 1).Shift(-1) == new CalendarDate(3, 28), "Spring 1 -> Winter 28");
Check(new CalendarDate(0, 28).Shift(1) == new CalendarDate(1, 1), "Spring -> Summer");
Check(new CalendarDate(2, 7).Shift(112) == new CalendarDate(2, 7), "Recurring yearly date");
Check(!CalendarDate.TryCreate(0, 0, out _), "Reject day zero");
Check(!CalendarDate.TryCreate(0, 29, out _), "Reject day 29");
Check(!CalendarDate.TryCreate(4, 1, out _), "Reject season five");
Check(!CalendarDate.TryCreate(-1, 1, out _), "Reject negative season");
for (int i = 0; i < 112; i++)
{
    var date = new CalendarDate(i / 28, i % 28 + 1);
    Check(date.Shift(1).Shift(-1) == date, "Calendar round trip " + i);
}

// A loved normal item beats a liked iridium item: never fill the loved list with likes.
var selection = GiftRanker.Select(new[] { Stock("love"), Stock("like", 4) }, id => id == "love" ? GiftPreference.Love : GiftPreference.Like);
Check(selection.Preference == GiftPreference.Love && selection.Gifts.Count == 1 && selection.Gifts[0].Item == "love", "Loved-first regardless of quality");

// Rank only quality that really exists; merge matching stacks, not different qualities.
selection = GiftRanker.Select(new[] { Stock("fruit", 0), Stock("fruit", 1), Stock("fruit", 2), Stock("fruit", 4, 2), Stock("fruit", 4, 3, true, "Bag") }, _ => GiftPreference.Love);
Check(selection.Gifts.Select(p => p.Quality).SequenceEqual(new[] { 4, 2, 1, 0 }), "Iridium > gold > silver > normal");
Check(selection.Gifts[0].Count == 5 && selection.Gifts[0].InventoryCount == 3, "Combined quantity retains inventory subtotal");
Check(selection.Gifts[0].Sources.Count == 2, "Retain chest and inventory locations");
Check(selection.Gifts.Count == 4, "Quality variants stay separate");

// Fall back only to owned liked gifts and ignore zero-count ghost stacks.
selection = GiftRanker.Select(new[] { Stock("missing-love", count: 0), Stock("like", 2), Stock("hate", 4) }, id => id == "missing-love" ? GiftPreference.Love : id == "like" ? GiftPreference.Like : GiftPreference.Other);
Check(selection.Preference == GiftPreference.Like && selection.Gifts.Count == 1, "Owned liked fallback");
selection = GiftRanker.Select(new[] { Stock("hate"), Stock("neutral") }, _ => GiftPreference.Other);
Check(selection.Preference == GiftPreference.Other && selection.Gifts.Count == 0, "Do not recommend neutral or hated gifts");
selection = GiftRanker.Select(Array.Empty<GiftStock<string>>(), _ => GiftPreference.Love);
Check(selection.Gifts.Count == 0, "Empty storage");

selection = GiftRanker.Select(Enumerable.Range(0, 12).Select(i => Stock("gift" + i)), _ => GiftPreference.Love);
Check(selection.Gifts.Count == 5, "At most five suggestions");
selection = GiftRanker.Select(new[] { Stock("roe:salmon", 2, name: "Salmon Roe"), Stock("roe:sturgeon", 2, name: "Sturgeon Roe") }, _ => GiftPreference.Love);
Check(selection.Gifts.Count == 2, "Preserved-item flavors stay distinct");
selection = GiftRanker.Select(new[] { Stock("Z", bag: true), Stock("A") }, _ => GiftPreference.Love);
Check(selection.Gifts[0].Item == "Z", "Prefer inventory when qualities tie");
Console.WriteLine($"PASS: {assertions} assertions covering calendar boundaries, gift fallback, quality, flavors, deduplication, and the five-gift limit.");
