using BirthdayPlanner.Core;
using BirthdayPlanner.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace BirthdayPlanner.Menus;

internal sealed class BirthdayMenu : IClickableMenu
{
    private readonly BirthdayService service;
    private readonly ITranslationHelper text;
    private readonly Action<CalendarDate> saveDate;
    private readonly Action refreshData;
    private readonly bool lookupInstalled;
    private List<BirthdayCard> cards = new();
    private readonly List<List<int>> pages = new();
    private readonly List<(Rectangle Bounds, BirthdayCard Card)> cardHits = new();
    private readonly List<(Rectangle Bounds, GiftSuggestion<Item> Gift)> giftHits = new();
    private readonly Dictionary<string, Rectangle> buttons = new();
    private CalendarDate selected;
    private int page;
    private bool datePicker;
    private int pickerSeason;
    private float scale;
    private string? tooltip;
    private Rectangle contentBounds;
    private Rectangle pickerBounds;

    // Lookup Anything's documented custom-menu convention. Only one is non-null.
    public NPC? HoveredNpc;
    public Item? HoveredItem;
    internal CalendarDate SelectedDate => selected;
    internal int CharacterCount => cards.Count;
    internal int PageCount => pages.Count;

    public BirthdayMenu(BirthdayService service, ITranslationHelper text, CalendarDate selected,
        Action<CalendarDate> saveDate, Action refreshData, bool lookupInstalled)
    {
        this.service = service;
        this.text = text;
        this.selected = selected;
        this.saveDate = saveDate;
        this.refreshData = refreshData;
        this.lookupInstalled = lookupInstalled;
        Layout();
        ReloadCards();
    }

    private int U(int value) => (int)Math.Round(value * scale);
    private string T(string key, object? tokens = null) => text.Get(key, tokens).ToString();
    private string DateLabel(CalendarDate date) => $"{Utility.getSeasonNameFromNumber(date.SeasonIndex)} {date.Day}";
    private string FriendshipLabel(BirthdayCard card) => T(card.AtMaxHearts ? "character.max-hearts" : "character.hearts",
        new { hearts = Math.Clamp(card.Hearts, 0, card.MaxHearts), max = card.MaxHearts });

    private void Layout()
    {
        width = Math.Min(1120, Game1.uiViewport.Width - 64);
        height = Math.Min(820, Game1.uiViewport.Height - 72);
        xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
        yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;
        scale = Math.Clamp(Math.Min(width / 820f, height / 600f), .45f, 1f);
        initializeUpperRightCloseButton();
        buttons.Clear();
        int x = xPositionOnScreen + U(28), y = yPositionOnScreen + U(65);
        buttons["previous"] = new(x, y, U(46), U(42));
        buttons["date"] = new(x + U(54), y, U(220), U(42));
        buttons["next"] = new(x + U(282), y, U(46), U(42));
        buttons["refresh"] = new(xPositionOnScreen + width - U(152), y, U(124), U(42));
        buttons["today"] = new(buttons["refresh"].X - U(112), y, U(104), U(42));
        contentBounds = new(x, yPositionOnScreen + U(144), width - U(56), height - U(208));
        int footerY = yPositionOnScreen + height - U(53);
        buttons["page-previous"] = new(x, footerY, U(44), U(34));
        buttons["page-next"] = new(x + U(52), footerY, U(44), U(34));
        pickerBounds = new(xPositionOnScreen + (width - U(480)) / 2, yPositionOnScreen + (height - U(328)) / 2, U(480), U(328));
    }

    private int Columns => Math.Clamp((contentBounds.Width - U(28)) / U(160), 1, 5);
    private int CardHeight(BirthdayCard card) => U(104) + (card.Selection.Gifts.Count == 0
        ? U(48) : ((card.Selection.Gifts.Count + Columns - 1) / Columns) * U(82));

    private void ReloadCards()
    {
        cards = service.GetCards(selected);
        pages.Clear();
        var current = new List<int>();
        int used = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            int size = CardHeight(cards[i]) + U(12);
            if (current.Count > 0 && used + size > contentBounds.Height)
            {
                pages.Add(current);
                current = new();
                used = 0;
            }
            current.Add(i);
            used += size;
        }
        if (current.Count > 0) pages.Add(current);
        page = Math.Clamp(page, 0, Math.Max(0, pages.Count - 1));
        BuildHitboxes();
    }

    private void BuildHitboxes()
    {
        cardHits.Clear();
        giftHits.Clear();
        int y = contentBounds.Y;
        if (pages.Count == 0) return;
        foreach (int index in pages[page])
        {
            BirthdayCard card = cards[index];
            cardHits.Add((new(contentBounds.X, y, contentBounds.Width, U(85)), card));
            int tileWidth = (contentBounds.Width - U(28)) / Columns;
            for (int i = 0; i < card.Selection.Gifts.Count; i++)
                giftHits.Add((new(contentBounds.X + U(14) + i % Columns * tileWidth, y + U(101) + i / Columns * U(82), tileWidth - U(5), U(76)), card.Selection.Gifts[i]));
            y += CardHeight(card) + U(12);
        }
        performHoverAction(Game1.getMouseX(), Game1.getMouseY());
    }

    private void SelectDate(CalendarDate date)
    {
        selected = date;
        page = 0;
        datePicker = false;
        saveDate(date);
        ReloadCards();
        Game1.playSound("shwip");
    }

    private void ChangePage(int delta)
    {
        int next = Math.Clamp(page + delta, 0, Math.Max(0, pages.Count - 1));
        if (next != page) { page = next; BuildHitboxes(); Game1.playSound("shwip"); }
    }

    public void Refresh()
    {
        refreshData();
        ReloadCards();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        Layout();
        ReloadCards();
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (upperRightCloseButton?.containsPoint(x, y) == true) { exitThisMenu(); return; }
        if (datePicker)
        {
            for (int season = 0; season < 4; season++)
                if (SeasonBounds(season).Contains(x, y)) { pickerSeason = season; Game1.playSound("smallSelect"); return; }
            for (int day = 1; day <= 28; day++)
                if (DayBounds(day).Contains(x, y)) { SelectDate(new(pickerSeason, day)); return; }
            datePicker = false;
            return;
        }
        foreach ((string id, Rectangle bounds) in buttons)
        {
            if (!bounds.Contains(x, y)) continue;
            switch (id)
            {
                case "previous": SelectDate(selected.Shift(-1)); break;
                case "next": SelectDate(selected.Shift(1)); break;
                case "today": SelectDate(BirthdayService.Today()); break;
                case "refresh": Refresh(); Game1.playSound("smallSelect"); break;
                case "date": datePicker = true; pickerSeason = selected.SeasonIndex; HoveredItem = null; HoveredNpc = null; break;
                case "page-previous": ChangePage(-1); break;
                case "page-next": ChangePage(1); break;
            }
            return;
        }
    }

    public override void receiveScrollWheelAction(int direction) => ChangePage(direction < 0 ? 1 : -1);
    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape && datePicker) { datePicker = false; return; }
        if (datePicker) { if (key == Keys.Escape) exitThisMenu(); return; }
        switch (key)
        {
            case Keys.Left: SelectDate(selected.Shift(-1)); return;
            case Keys.Right: SelectDate(selected.Shift(1)); return;
            case Keys.Home: SelectDate(BirthdayService.Today()); return;
            case Keys.Up: case Keys.PageUp: ChangePage(-1); return;
            case Keys.Down: case Keys.PageDown: ChangePage(1); return;
        }
        base.receiveKeyPress(key);
    }

    public override void receiveGamePadButton(Buttons button)
    {
        switch (button)
        {
            case Buttons.DPadLeft: SelectDate(selected.Shift(-1)); break;
            case Buttons.DPadRight: SelectDate(selected.Shift(1)); break;
            case Buttons.LeftShoulder: ChangePage(-1); break;
            case Buttons.RightShoulder: ChangePage(1); break;
            case Buttons.Y: SelectDate(BirthdayService.Today()); break;
            case Buttons.B: if (datePicker) datePicker = false; else exitThisMenu(); break;
            default: base.receiveGamePadButton(button); break;
        }
    }

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        HoveredNpc = null;
        HoveredItem = null;
        tooltip = null;
        if (datePicker) return;
        foreach (var hit in giftHits)
        {
            if (!hit.Bounds.Contains(x, y)) continue;
            HoveredItem = hit.Gift.Item;
            tooltip = hit.Gift.Name + "\n" + T("gift.quantity", new { count = hit.Gift.Count, inventory = hit.Gift.InventoryCount })
                + "\n" + string.Join("\n", hit.Gift.Sources.Take(8)) + (lookupInstalled ? "\n" + T("lookup.item") : "");
            return;
        }
        foreach (var hit in cardHits)
        {
            if (!hit.Bounds.Contains(x, y)) continue;
            HoveredNpc = hit.Card.Npc;
            tooltip = hit.Card.Npc.displayName + "\n" + T("character.home", new { home = hit.Card.Home })
                + "\n" + FriendshipLabel(hit.Card)
                + (lookupInstalled ? "\n" + T("lookup.npc") : "");
            return;
        }
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .55f);
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);
        Label(b, T("menu.title"), xPositionOnScreen + U(28), yPositionOnScreen + U(21), 1.1f);
        DrawButton(b, "previous", "<");
        DrawButton(b, "date", DateLabel(selected) + "  v");
        DrawButton(b, "next", ">");
        DrawButton(b, "today", T("menu.today"));
        DrawButton(b, "refresh", T("menu.refresh"));
        Label(b, T("menu.stock-date", new { date = DateLabel(BirthdayService.Today()) }), contentBounds.X, yPositionOnScreen + U(116), .7f, new Color(110, 85, 64));

        int y = contentBounds.Y;
        if (pages.Count == 0)
        {
            Label(b, T("menu.empty"), contentBounds.X + U(18), y + U(36), .95f);
            Label(b, T("menu.empty-hint"), contentBounds.X + U(18), y + U(75), .75f);
        }
        else foreach (int index in pages[page])
        {
            DrawCard(b, cards[index], y);
            y += CardHeight(cards[index]) + U(12);
        }
        DrawButton(b, "page-previous", "^", page > 0);
        DrawButton(b, "page-next", "v", page + 1 < pages.Count);
        string pageText = pages.Count > 0 ? T("menu.page", new { page = page + 1, pages = pages.Count, count = cards.Count }) : T("menu.no-birthdays");
        Label(b, pageText, contentBounds.X + U(108), buttons["page-next"].Y + U(6), .7f);
        string hint = lookupInstalled ? T("menu.controls-lookup") : T("menu.controls");
        int hintWidth = (int)(Game1.smallFont.MeasureString(hint).X * .6f * scale);
        if (hintWidth < width / 2)
            Label(b, hint, xPositionOnScreen + width - U(28) - hintWidth, buttons["page-next"].Y + U(8), .6f, new Color(110, 85, 64));
        upperRightCloseButton?.draw(b);
        if (datePicker) DrawDatePicker(b);
        else if (tooltip != null) drawHoverText(b, tooltip, Game1.smallFont);
        drawMouse(b);
    }

    private void DrawCard(SpriteBatch b, BirthdayCard card, int y)
    {
        Rectangle bounds = new(contentBounds.X, y, contentBounds.Width, CardHeight(card));
        b.Draw(Game1.staminaRect, bounds, new Color(246, 223, 180));
        b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y, U(4), bounds.Height), new Color(167, 95, 61));
        Rectangle portrait = new(bounds.X + U(14), y + U(12), U(64), U(64));
        try { b.Draw(card.Npc.Portrait, portrait, new Rectangle(0, 0, 64, 64), Color.White); }
        catch { Label(b, "?", portrait.X + U(20), portrait.Y + U(16)); }
        string friendship = FriendshipLabel(card);
        int badgeWidth = (int)Math.Ceiling(Game1.smallFont.MeasureString(friendship).X * .65f * scale) + U(16);
        Rectangle badge = new(bounds.Right - U(14) - badgeWidth, y + U(14), badgeWidth, U(31));
        b.Draw(Game1.staminaRect, badge, card.AtMaxHearts ? new Color(207, 229, 171) : new Color(252, 234, 196));
        Label(b, friendship, badge.X + U(8), badge.Y + U(7), .65f,
            card.AtMaxHearts ? new Color(51, 91, 42) : new Color(111, 84, 63));
        Label(b, Fit(card.Npc.displayName, badge.X - bounds.X - U(104), 1f), bounds.X + U(92), y + U(16));
        Label(b, Fit(T("character.home", new { home = card.Home }), bounds.Width - U(110), .7f), bounds.X + U(92), y + U(48), .7f);
        string category = card.Selection.Preference switch { GiftPreference.Love => T("gift.loved"), GiftPreference.Like => T("gift.liked"), _ => T("gift.none") };
        if (selected == BirthdayService.Today() && card.GiftedToday) category += "  /  " + T("gift.already-given");
        Label(b, Fit(category, bounds.Width - U(28), .7f), bounds.X + U(14), y + U(82), .7f,
            card.Selection.Preference == GiftPreference.Love ? new Color(136, 55, 63) : new Color(88, 92, 49));
        if (card.Selection.Gifts.Count == 0)
            Label(b, Fit(T("gift.none-hint"), bounds.Width - U(28), .7f), bounds.X + U(14), y + U(115), .7f);
        foreach (var hit in giftHits.Where(p => p.Bounds.Y >= y && p.Bounds.Y < y + bounds.Height))
        {
            Rectangle tile = hit.Bounds;
            b.Draw(Game1.staminaRect, tile, tile.Contains(Game1.getMouseX(), Game1.getMouseY()) ? new Color(255, 243, 211) : new Color(252, 234, 196));
            hit.Gift.Item.drawInMenu(b, new Vector2(tile.X + U(2), tile.Y), .65f * scale, 1f, .9f, StackDrawType.HideButShowQuality, Color.White, true);
            Label(b, "x" + hit.Gift.Count, tile.X + U(68), tile.Y + U(9), .65f);
            Label(b, Fit(hit.Gift.InventoryCount > 0 ? T("gift.in-bag") : T("gift.in-storage"), tile.Width - U(74), .55f), tile.X + U(68), tile.Y + U(31), .55f, new Color(111, 84, 63));
            Label(b, Fit(hit.Gift.Name, tile.Width - U(12), .63f), tile.X + U(6), tile.Y + U(56), .63f);
        }
    }

    private void DrawButton(SpriteBatch b, string id, string label, bool enabled = true)
    {
        Rectangle bounds = buttons[id];
        Color color = !enabled ? new Color(231, 211, 180) : bounds.Contains(Game1.getMouseX(), Game1.getMouseY()) ? new Color(255, 244, 213) : new Color(243, 222, 183);
        b.Draw(Game1.staminaRect, bounds, color);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Bottom - U(2), bounds.Width, U(2)), new Color(172, 123, 80));
        float fontScale = .8f * scale;
        Vector2 size = Game1.smallFont.MeasureString(label) * fontScale;
        b.DrawString(Game1.smallFont, label, new Vector2(bounds.Center.X - size.X / 2, bounds.Center.Y - size.Y / 2), enabled ? Game1.textColor : Color.Gray, 0, Vector2.Zero, fontScale, SpriteEffects.None, 1);
    }

    private Rectangle SeasonBounds(int season) => new(pickerBounds.X + U(20 + season * 112), pickerBounds.Y + U(54), U(104), U(36));
    private Rectangle DayBounds(int day) => new(pickerBounds.X + U(24 + (day - 1) % 7 * 62), pickerBounds.Y + U(111 + (day - 1) / 7 * 46), U(56), U(40));
    private void DrawDatePicker(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, new Rectangle(xPositionOnScreen, yPositionOnScreen, width, height), Color.Black * .45f);
        drawTextureBox(b, pickerBounds.X, pickerBounds.Y, pickerBounds.Width, pickerBounds.Height, Color.White);
        Label(b, T("menu.choose-date"), pickerBounds.X + U(24), pickerBounds.Y + U(20), .85f);
        for (int season = 0; season < 4; season++)
        {
            Rectangle bounds = SeasonBounds(season);
            b.Draw(Game1.staminaRect, bounds, season == pickerSeason ? new Color(218, 172, 113) : new Color(246, 224, 184));
            Label(b, Fit(Utility.getSeasonNameFromNumber(season), bounds.Width - U(12), .7f), bounds.X + U(6), bounds.Y + U(8), .7f);
        }
        for (int day = 1; day <= 28; day++)
        {
            Rectangle bounds = DayBounds(day);
            bool current = selected.SeasonIndex == pickerSeason && selected.Day == day;
            b.Draw(Game1.staminaRect, bounds, current ? new Color(218, 172, 113) : bounds.Contains(Game1.getMouseX(), Game1.getMouseY()) ? new Color(255, 246, 217) : new Color(246, 224, 184));
            Label(b, day.ToString(), bounds.X + U(16), bounds.Y + U(8), .8f);
        }
        Label(b, T("menu.picker-hint"), pickerBounds.X + U(24), pickerBounds.Bottom - U(34), .65f);
    }

    private void Label(SpriteBatch b, string label, int x, int y, float fontScale = 1f, Color? color = null) =>
        b.DrawString(Game1.smallFont, label, new Vector2(x, y), color ?? Game1.textColor, 0, Vector2.Zero, fontScale * scale, SpriteEffects.None, 1);
    private string Fit(string value, int maxWidth, float fontScale)
    {
        if (Game1.smallFont.MeasureString(value).X * fontScale * scale <= maxWidth) return value;
        while (value.Length > 0 && Game1.smallFont.MeasureString(value + "...").X * fontScale * scale > maxWidth)
            value = value[..^1];
        return value + "...";
    }
}
