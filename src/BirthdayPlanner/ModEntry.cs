using System.Collections;
using System.Reflection;
using BirthdayPlanner.Core;
using BirthdayPlanner.Integrations;
using BirthdayPlanner.Menus;
using BirthdayPlanner.Services;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace BirthdayPlanner;

public sealed class ModEntry : Mod
{
    private ModConfig config = new();
    private BirthdayService service = null!;
    private readonly PerScreen<CalendarDate?> selectedDate = new();
    private readonly PerScreen<bool> dirty = new(() => true);

    public override void Entry(IModHelper helper)
    {
        config = helper.ReadConfig<ModConfig>();
        service = new BirthdayService(helper, Monitor);
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => { selectedDate.Value = null; dirty.Value = true; };
        helper.Events.GameLoop.SaveLoaded += (_, _) => { selectedDate.Value = null; dirty.Value = true; };
        helper.Events.GameLoop.DayStarted += (_, _) => dirty.Value = true;
        helper.Events.Player.InventoryChanged += (_, _) => dirty.Value = true;
        helper.Events.World.ChestInventoryChanged += (_, _) => dirty.Value = true;
        helper.Events.Content.AssetsInvalidated += (_, _) => dirty.Value = true;
        helper.Events.GameLoop.OneSecondUpdateTicked += (_, _) =>
        {
            if (Context.IsWorldReady && dirty.Value && Game1.activeClickableMenu is BirthdayMenu menu) menu.Refresh();
        };
        helper.ConsoleCommands.Add("birthday_planner", "Open Birthday Planner. Optional: birthday_planner spring 13", OpenCommand);
        helper.ConsoleCommands.Add("birthday_planner_verify", "Read-only diagnostic: birthdays, gifts, layout, and owned-stock parity with Lookup Anything. Writes verification/runtime-report.json in this mod folder.", VerifyCommand);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        IGenericModConfigMenuApi? api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api == null) return;
        api.Register(ModManifest, () => config = new(), () => { Helper.WriteConfig(config); dirty.Value = true; });
        api.AddKeybindList(ModManifest, () => config.OpenMenu, value => config.OpenMenu = value,
            () => T("config.open"), () => T("config.open-hint"));
        api.AddBoolOption(ModManifest, () => config.IncludeUnmetCharacters, value => config.IncludeUnmetCharacters = value,
            () => T("config.unmet"), () => T("config.unmet-hint"));
        api.AddBoolOption(ModManifest, () => config.RememberSelectedDate, value => config.RememberSelectedDate = value,
            () => T("config.remember"), () => T("config.remember-hint"));
    }

    private string T(string key) => Helper.Translation.Get(key).ToString();

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (!Context.IsWorldReady || !config.OpenMenu.JustPressed()) return;
        if (Game1.activeClickableMenu is BirthdayMenu menu)
        {
            Helper.Input.SuppressActiveKeybinds(config.OpenMenu);
            menu.exitThisMenu();
        }
        else if (Context.IsPlayerFree)
        {
            Helper.Input.SuppressActiveKeybinds(config.OpenMenu);
            Open();
        }
    }

    private void RefreshData()
    {
        service.Refresh(config.IncludeUnmetCharacters);
        dirty.Value = false;
    }

    private string StatePath => $"data/{Game1.uniqueIDForThisGame}-{Game1.player.UniqueMultiplayerID}.json";

    private CalendarDate GetSelectedDate()
    {
        if (!config.RememberSelectedDate) return BirthdayService.Today();
        if (selectedDate.Value.HasValue) return selectedDate.Value.Value;
        try
        {
            DateState? state = Helper.Data.ReadJsonFile<DateState>(StatePath);
            if (state != null && CalendarDate.TryCreate(state.SeasonIndex, state.Day, out CalendarDate date))
                return (selectedDate.Value = date).Value;
        }
        catch (Exception ex) { Monitor.Log($"Couldn't read the last selected date; using today. {ex.Message}", LogLevel.Warn); }
        return (selectedDate.Value = BirthdayService.Today()).Value;
    }

    private void SaveDate(CalendarDate date)
    {
        selectedDate.Value = date;
        if (!config.RememberSelectedDate) return;
        try { Helper.Data.WriteJsonFile(StatePath, new DateState { SeasonIndex = date.SeasonIndex, Day = date.Day }); }
        catch (Exception ex) { Monitor.Log($"Couldn't remember the selected date: {ex.Message}", LogLevel.Warn); }
    }

    private void Open(CalendarDate? explicitDate = null)
    {
        try
        {
            RefreshData();
            CalendarDate date = explicitDate ?? GetSelectedDate();
            if (explicitDate.HasValue) SaveDate(date);
            Game1.activeClickableMenu = new BirthdayMenu(service, Helper.Translation, date, SaveDate, RefreshData,
                Helper.ModRegistry.IsLoaded("Pathoschild.LookupAnything"));
            Game1.playSound("bigSelect");
        }
        catch (Exception ex) { Monitor.Log($"Couldn't open Birthday Planner: {ex}", LogLevel.Error); }
    }

    private void OpenCommand(string command, string[] args)
    {
        if (!Context.IsWorldReady) { Monitor.Log("Load a save before opening Birthday Planner.", LogLevel.Warn); return; }
        if (args.Length == 0) { Open(); return; }
        if (args.Length != 2 || !int.TryParse(args[1], out int day) || !CalendarDate.TryCreate(Utility.getSeasonNumber(args[0].ToLowerInvariant()), day, out CalendarDate date))
        {
            Monitor.Log("Usage: birthday_planner [spring|summer|fall|winter 1..28]", LogLevel.Warn);
            return;
        }
        Open(date);
    }

    private void VerifyCommand(string command, string[] args)
    {
        if (!Context.IsWorldReady) { Monitor.Log("Load a save before running the diagnostic.", LogLevel.Warn); return; }
        try
        {
            RefreshData();
            var birthdays = Enumerable.Range(0, 112).Select(i => new CalendarDate(i / 28, i % 28 + 1))
                .SelectMany(date => service.GetCards(date).Select(card => new {
                    date.Season, date.Day, Name = card.Npc.Name, DisplayName = card.Npc.displayName, card.Home, HomeMap = card.Npc.DefaultMap,
                    Preference = card.Selection.Preference.ToString(), Gifts = card.Selection.Gifts.Select(gift => new {
                        Id = gift.Item.QualifiedItemId, gift.Name, gift.Quality, gift.Count, gift.InventoryCount, gift.Sources })
                })).ToArray();
            object? parity = null;
            try
            {
                IModInfo? lookupInfo = Helper.ModRegistry.Get("Pathoschild.LookupAnything");
                object? lookup = lookupInfo?.GetType().GetProperty("Mod", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(lookupInfo);
                if (lookup != null)
                {
                    object gameHelper = lookup.GetType().GetField("GameHelper", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(lookup)!;
                    IEnumerable original = (IEnumerable)gameHelper.GetType().GetMethod("GetAllOwnedItems")!.Invoke(gameHelper, null)!;
                    var reference = new List<(string Key, int Count)>();
                    foreach (object found in original)
                    {
                        Item item = (Item)found.GetType().GetProperty("Item")!.GetValue(found)!;
                        if (item.canBeGivenAsGift()) reference.Add((OwnedItemScanner.VariantKey(item) + "|" + item.Quality,
                            (int)found.GetType().GetMethod("GetCount")!.Invoke(found, null)!));
                    }
                    var expected = reference.GroupBy(p => p.Key).ToDictionary(p => p.Key, p => p.Sum(q => q.Count));
                    var actual = service.Stock.GroupBy(p => p.VariantKey + "|" + p.Quality).ToDictionary(p => p.Key, p => p.Sum(q => q.Count));
                    var mismatches = expected.Keys.Union(actual.Keys).Where(key => expected.GetValueOrDefault(key) != actual.GetValueOrDefault(key))
                        .Select(key => new { Key = key, LookupAnything = expected.GetValueOrDefault(key), BirthdayPlanner = actual.GetValueOrDefault(key) }).ToArray();
                    parity = new { Compared = true, Matches = mismatches.Length == 0, Mismatches = mismatches };
                }
            }
            catch (Exception ex) { parity = new { Compared = false, Reason = ex.Message }; }
            var report = new {
                Timestamp = DateTimeOffset.UtcNow, Game = Game1.version, Smapi = Constants.ApiVersion.ToString(),
                Context.IsMainPlayer, Context.IsMultiplayer, Birthdays = birthdays,
                OwnedGiftStacks = service.Stock.Count, OtherPlayersInventoriesIncluded = false, LookupAnythingParity = parity,
                OpenMenu = Game1.activeClickableMenu is BirthdayMenu menu ? new { Date = menu.SelectedDate, menu.CharacterCount, menu.PageCount } : null
            };
            Helper.Data.WriteJsonFile("verification/runtime-report.json", report);
            Monitor.Log($"Verified {birthdays.Length} birthday characters and {service.Stock.Count} owned gift stacks. Report: {Path.Combine(Helper.DirectoryPath, "verification", "runtime-report.json")}", LogLevel.Info);
        }
        catch (Exception ex) { Monitor.Log($"Birthday Planner diagnostic failed: {ex}", LogLevel.Error); }
    }
}
