using StardewModdingAPI.Utilities;

namespace BirthdayPlanner;

public sealed class ModConfig
{
    public KeybindList OpenMenu { get; set; } = KeybindList.Parse("LeftControl + B");
    public bool IncludeUnmetCharacters { get; set; } = true;
    public bool RememberSelectedDate { get; set; } = true;
}

internal sealed class DateState
{
    public int SeasonIndex { get; set; }
    public int Day { get; set; }
}
