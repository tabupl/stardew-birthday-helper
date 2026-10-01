namespace BirthdayPlanner.Core;

/// <summary>A recurring birthday date, independent of the save's year.</summary>
public readonly record struct CalendarDate
{
    public int SeasonIndex { get; }
    public int Day { get; }
    public string Season => new[] { "spring", "summer", "fall", "winter" }[SeasonIndex];

    public CalendarDate(int seasonIndex, int day)
    {
        if (seasonIndex is < 0 or > 3 || day is < 1 or > 28)
            throw new ArgumentOutOfRangeException(nameof(day), "Birthday dates use four seasons of 28 days.");
        SeasonIndex = seasonIndex;
        Day = day;
    }

    public CalendarDate Shift(int days)
    {
        int index = ((SeasonIndex * 28 + Day - 1 + days) % 112 + 112) % 112;
        return new CalendarDate(index / 28, index % 28 + 1);
    }

    public static bool TryCreate(int seasonIndex, int day, out CalendarDate date)
    {
        if (seasonIndex is >= 0 and <= 3 && day is >= 1 and <= 28)
        {
            date = new CalendarDate(seasonIndex, day);
            return true;
        }
        date = default;
        return false;
    }
}
