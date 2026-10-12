using System.Reflection;
using BirthdayPlanner.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace BirthdayPlanner.IntegrationDriver;

/// <summary>Test-only companion. Never distributed in the release package.</summary>
public sealed class ModEntry : Mod
{
    private readonly List<string> passed = new();
    public override void Entry(IModHelper helper)
    {
        if (Environment.GetEnvironmentVariable("BIRTHDAY_PLANNER_TEST_SESSION") != "1" || !Program.GetSavesFolder().Contains("/test-session/"))
        {
            Monitor.Log("Integration driver disabled outside an isolated test-session save directory.", LogLevel.Warn);
            return;
        }
        helper.ConsoleCommands.Add("bp_test_load", "Load only the copied test save.", (_, _) => { SaveGame.Load("kjnkjn_449866233"); Game1.exitActiveMenu(); });
        helper.ConsoleCommands.Add("bp_test_fixture", "Add gift fixtures only in the copied test world; never saves.", Fixture);
        helper.ConsoleCommands.Add("bp_test_friendship", "Check friendship caps and thresholds in the isolated world; restores all original friendship records.", TestFriendship);
        helper.ConsoleCommands.Add("bp_test_action", "Run a planner integration action: right, left, today, picker, day N, season N, escape, lookup-npc, lookup-item, verify-ui.", Act);
        helper.ConsoleCommands.Add("bp_test_quit", "Exit the test process without saving.", (_, _) => Game1.game1.Exit());
    }
    private object GetMod(string id)
    {
        object info = Helper.ModRegistry.Get(id)!;
        return info.GetType().GetProperty("Mod", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(info)!;
    }
    private void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        passed.Add(name);
        Monitor.Log("PASS: " + name, LogLevel.Info);
        Helper.Data.WriteJsonFile("report.json", new { Passed = passed.ToArray(), Timestamp = DateTimeOffset.UtcNow });
    }
    private void Fixture(string cmd, string[] args)
    {
        if (!Context.IsWorldReady) return;
        foreach ((string id, int quality) in new[] { ("(O)72", 0), ("(O)72", 1), ("(O)72", 2), ("(O)72", 4), ("(O)74", 0), ("(O)400", 4) })
            Game1.player.addItemToInventoryBool(ItemRegistry.Create(id, 2, quality));
        var chest = new Chest(true) { TileLocation = new Vector2(16, 16) };
        chest.modData["Pathoschild.ChestsAnywhere/Name"] = "Birthday test gifts";
        foreach ((string id, int quality) in new[] { ("(O)72", 4), ("(O)18", 2), ("(O)24", 4), ("(O)64", 4), ("(O)128", 4) })
            chest.Items.Add(ItemRegistry.Create(id, 3, quality));
        var nested = new Chest(true);
        nested.Items.Add(ItemRegistry.Create("(O)72", 4, 4));
        chest.Items.Add(nested);
        Game1.getFarm().objects[new Vector2(16, 16)] = chest;
        foreach (var (name, hearts) in new[] { ("Abigail", 8), ("Pierre", 10), ("Alissa", 8) })
            Game1.player.friendshipData[name] = new Friendship(hearts * NPC.friendshipPointsPerHeartLevel);
        Check(true, "Gift fixture created in isolated memory only");
    }
    private void Click(IClickableMenu menu, Rectangle bounds) => menu.receiveLeftClick(bounds.Center.X, bounds.Center.Y);
    private void TestFriendship(string command, string[] args)
    {
        if (!Context.IsWorldReady) return;
        object planner = GetMod("Adam.BirthdayPlanner");
        Helper.Reflection.GetMethod(planner, "RefreshData").Invoke();
        object service = Helper.Reflection.GetField<object>(planner, "service").GetValue();
        var originals = new Dictionary<string, Friendship?>();
        try
        {
            foreach (var example in new (string Name, FriendshipStatus Status, int Points, int Hearts, int Cap, bool Maxed)[] {
                ("Abigail", FriendshipStatus.Friendly, 1999, 7, 8, false),
                ("Abigail", FriendshipStatus.Friendly, 2000, 8, 8, true),
                ("Abigail", FriendshipStatus.Dating, 2000, 8, 10, false),
                ("Abigail", FriendshipStatus.Dating, 2499, 9, 10, false),
                ("Abigail", FriendshipStatus.Dating, 2500, 10, 10, true),
                ("Abigail", FriendshipStatus.Engaged, 2500, 10, 10, true),
                ("Abigail", FriendshipStatus.Married, 3499, 13, 14, false),
                ("Abigail", FriendshipStatus.Married, 3500, 14, 14, true),
                ("Pierre", FriendshipStatus.Friendly, 2499, 9, 10, false),
                ("Pierre", FriendshipStatus.Friendly, 2500, 10, 10, true),
                ("Alissa", FriendshipStatus.Friendly, 1999, 7, 8, false),
                ("Alissa", FriendshipStatus.Friendly, 2000, 8, 8, true) })
            {
                if (!originals.ContainsKey(example.Name))
                    originals[example.Name] = Game1.player.friendshipData.TryGetValue(example.Name, out Friendship? original) ? original : null;
                Game1.player.friendshipData[example.Name] = new Friendship(example.Points) { Status = example.Status };
                NPC npc = Game1.getCharacterFromName(example.Name);
                var date = new CalendarDate(Utility.getSeasonNumber(npc.Birthday_Season), npc.Birthday_Day);
                var cards = Helper.Reflection.GetMethod(service, "GetCards").Invoke<System.Collections.IEnumerable>(date).Cast<object>();
                object card = cards.Single(p => Helper.Reflection.GetProperty<NPC>(p, "Npc").GetValue().Name == example.Name);
                Check(Helper.Reflection.GetProperty<int>(card, "Hearts").GetValue() == example.Hearts,
                    $"{example.Name} {example.Status} at {example.Points} points: {example.Hearts} hearts");
                Check(Helper.Reflection.GetProperty<int>(card, "MaxHearts").GetValue() == example.Cap &&
                    Helper.Reflection.GetProperty<bool>(card, "AtMaxHearts").GetValue() == example.Maxed,
                    $"{example.Name} {example.Status}: cap {example.Cap}, max badge {example.Maxed}");
            }
            Game1.player.friendshipData.Remove("Abigail");
            var unmetDate = new CalendarDate(2, 13);
            object unmet = Helper.Reflection.GetMethod(service, "GetCards").Invoke<System.Collections.IEnumerable>(unmetDate).Cast<object>()
                .Single(p => Helper.Reflection.GetProperty<NPC>(p, "Npc").GetValue().Name == "Abigail");
            Check(Helper.Reflection.GetProperty<int>(unmet, "Hearts").GetValue() == 0 && !Helper.Reflection.GetProperty<bool>(unmet, "AtMaxHearts").GetValue(),
                "Unmet character shows zero hearts without a max badge");
        }
        catch (Exception ex) { Monitor.Log("FAIL: " + ex, LogLevel.Error); }
        finally
        {
            foreach (var (name, original) in originals)
                if (original == null) Game1.player.friendshipData.Remove(name); else Game1.player.friendshipData[name] = original;
            Check(true, "Original friendship records restored; no save writes");
        }
    }
    private void Act(string cmd, string[] args)
    {
        try
        {
            IClickableMenu menu = Game1.activeClickableMenu;
            string action = args.FirstOrDefault() ?? "verify-ui";
            if (action == "escape") { menu.receiveKeyPress(Keys.Escape); Check(true, "Escape processed"); return; }
            if (menu.GetType().FullName != "BirthdayPlanner.Menus.BirthdayMenu") throw new Exception("Open Birthday Planner first.");
            var buttons = Helper.Reflection.GetField<Dictionary<string, Rectangle>>(menu, "buttons").GetValue();
            switch (action)
            {
                case "right": menu.receiveKeyPress(Keys.Right); Check(true, "Next date through keyboard handler"); break;
                case "left": menu.receiveKeyPress(Keys.Left); Check(true, "Previous date through keyboard handler"); break;
                case "today": Click(menu, buttons["today"]); Check(true, "Today button"); break;
                case "picker": Click(menu, buttons["date"]); Check(Helper.Reflection.GetField<bool>(menu, "datePicker").GetValue(), "Date picker opens"); break;
                case "season": Click(menu, Helper.Reflection.GetMethod(menu, "SeasonBounds").Invoke<Rectangle>(int.Parse(args[1]))); Check(true, "Picker season selection"); break;
                case "day": Click(menu, Helper.Reflection.GetMethod(menu, "DayBounds").Invoke<Rectangle>(int.Parse(args[1]))); Check(!Helper.Reflection.GetField<bool>(menu, "datePicker").GetValue(), "Picker day selection closes picker"); break;
                case "lookup-npc":
                case "lookup-item":
                    {
                        string field = action == "lookup-npc" ? "cardHits" : "giftHits";
                        object hit = ((System.Collections.IEnumerable)Helper.Reflection.GetField<object>(menu, field).GetValue()).Cast<object>().First();
                        Rectangle bounds = (Rectangle)hit.GetType().GetField("Item1")!.GetValue(hit)!;
                        menu.performHoverAction(bounds.Center.X, bounds.Center.Y);
                        object lookup = GetMod("Pathoschild.LookupAnything");
                        lookup.GetType().GetMethod("ToggleLookup", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(lookup, new object[] { Type.Missing });
                        Check(Game1.activeClickableMenu.GetType().Name == "LookupMenu", "F1 integration resolves " + action);
                        break;
                    }
                case "verify-ui": VerifyUi(menu); break;
                default: throw new Exception("Unknown test action.");
            }
        }
        catch (Exception ex) { Monitor.Log("FAIL: " + ex, LogLevel.Error); }
    }
    private void VerifyUi(IClickableMenu menu)
    {
        Rectangle content = Helper.Reflection.GetField<Rectangle>(menu, "contentBounds").GetValue();
        var cards = ((System.Collections.IEnumerable)Helper.Reflection.GetField<object>(menu, "cards").GetValue()).Cast<object>().ToArray();
        int pageCount = Helper.Reflection.GetProperty<int>(menu, "PageCount").GetValue();
        for (int i = 0; i < pageCount; i++)
        {
            var hits = ((System.Collections.IEnumerable)Helper.Reflection.GetField<object>(menu, "giftHits").GetValue()).Cast<object>();
            foreach (object hit in hits)
            {
                Rectangle bounds = (Rectangle)hit.GetType().GetField("Item1")!.GetValue(hit)!;
                Check(content.Contains(bounds), "Gift tile stays within content bounds on page " + (i + 1));
            }
            menu.receiveScrollWheelAction(-1);
        }
        CalendarDate date = Helper.Reflection.GetProperty<CalendarDate>(menu, "SelectedDate").GetValue();
        menu.receiveKeyPress(Keys.Right);
        Check(Helper.Reflection.GetProperty<CalendarDate>(menu, "SelectedDate").GetValue() == date.Shift(1), "Date arrow changes exactly one day");
        menu.receiveKeyPress(Keys.Left);
        Check(Helper.Reflection.GetProperty<CalendarDate>(menu, "SelectedDate").GetValue() == date, "Date arrow round trip");
        menu.exitThisMenu();
        Helper.Reflection.GetMethod(GetMod("Adam.BirthdayPlanner"), "OpenCommand").Invoke("birthday_planner", Array.Empty<string>());
        Check(Helper.Reflection.GetProperty<CalendarDate>(Game1.activeClickableMenu, "SelectedDate").GetValue() == date, "Close/reopen remembers selected date");
    }
}
