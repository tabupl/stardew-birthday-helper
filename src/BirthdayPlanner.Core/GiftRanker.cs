namespace BirthdayPlanner.Core;

public enum GiftPreference { Other, Like, Love }

/// <summary>A concrete owned stack. Flavor and quality stay distinct.</summary>
public record GiftStock<T>(string VariantKey, string Name, T Item, int Quality, int Count, string Source, bool InInventory);
public record GiftSuggestion<T>(T Item, string Name, int Quality, int Count, int InventoryCount, IReadOnlyList<string> Sources);
public record GiftSelection<T>(GiftPreference Preference, IReadOnlyList<GiftSuggestion<T>> Gifts);

public static class GiftRanker
{
    public static GiftSelection<T> Select<T>(IEnumerable<GiftStock<T>> stock, Func<T, GiftPreference> taste, int limit = 5)
    {
        if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));
        var evaluated = stock.Where(p => p.Count > 0).Select(p => (Stock: p, Taste: taste(p.Item))).ToArray();
        GiftPreference preference = evaluated.Any(p => p.Taste == GiftPreference.Love)
            ? GiftPreference.Love
            : evaluated.Any(p => p.Taste == GiftPreference.Like) ? GiftPreference.Like : GiftPreference.Other;
        if (preference == GiftPreference.Other)
            return new(preference, Array.Empty<GiftSuggestion<T>>());

        var gifts = evaluated.Where(p => p.Taste == preference)
            .GroupBy(p => (p.Stock.VariantKey, p.Stock.Quality))
            .Select(group => new GiftSuggestion<T>(
                group.First().Stock.Item, group.First().Stock.Name, group.Key.Quality,
                group.Sum(p => p.Stock.Count), group.Where(p => p.Stock.InInventory).Sum(p => p.Stock.Count),
                group.Select(p => p.Stock.Source).Distinct().OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase).ToArray()))
            .OrderByDescending(p => p.Quality)
            .ThenByDescending(p => p.InventoryCount > 0)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(p => p.Count)
            .Take(limit).ToArray();
        return new(preference, gifts);
    }
}
