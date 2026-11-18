using static ZZZ.Mobile.State;

namespace ZZZ.Mobile;

/// <summary>清單的一列(顏色依目前深色/淺色預先算好)。</summary>
public record RecItem(RecordRow Row, string Title, string Tag, Color TagFg, Color TagBg, string Sub, string Amount, Color AmountColor);

/// <summary>收支記錄:目前選的錢包、餘額大卡片、期間切換、收入/支出/結餘、記錄清單。</summary>
public class RecordsPage : TabPage
{
    readonly CollectionView list = new() { SelectionMode = SelectionMode.None };
    readonly Label walletText = Ui.L1("", 15, bold: true);
    readonly Label walletIcon = Ui.L("👛", 15);
    readonly Label heroLabel = Ui.L1("", 13, Theme.OnHeroMuted);
    readonly Label balanceText = new() { FontSize = 32, FontAttributes = FontAttributes.Bold };
    readonly Label balanceSub = Ui.L("", 13, Theme.OnHeroMuted);
    readonly HorizontalStackLayout cardButtons = new() { Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };
    readonly Label periodText = Ui.L1("", 16, bold: true);
    readonly (Border Card, Label Value, Label Sub) inc = Ui.Tile("收入", Theme.Inc), exp = Ui.Tile("支出", Theme.Exp), net = Ui.Tile("結餘");
    readonly Border catChip;
    readonly Label catChipText = Ui.L1("", 13, Theme.Ink, bold: true);
    readonly Label countText = Ui.L1("", 13, Theme.Muted);
    CardBill? bill;
    readonly Border hero;
    readonly Label cuteDeco = new() { FontSize = 26, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start, IsVisible = false };
    readonly Button payBtn, installBtn;

    public RecordsPage() : base("收支記錄")
    {
        // ---- 頂端:錢包選擇與轉移資金 ----
        var walletChip = new Border
        {
            Padding = new Thickness(14, 8), StrokeThickness = 1, StrokeShape = Theme.Round(20), HorizontalOptions = LayoutOptions.Start,
            Content = new HorizontalStackLayout { Spacing = 6, Children = { walletIcon, walletText, Ui.L("▾", 13, Theme.Muted) } },
        }.Fill(Theme.Surface).Stroke_(Theme.Line).OnTap(() => Navigation.PushAsync(new WalletsPage()));
        var top = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        top.Add(walletChip, 0);
        top.Add(Ui.Btn("⇄ 轉移資金", Ui.Kind.Outline, Ops.Transfer, 38), 1);

        // ---- 大卡片:餘額或信用卡本期應繳 ----
        cardButtons.Add(payBtn = Ui.Btn("繳卡費", Ui.Kind.OnHero, Ops.PayCard, 36));
        cardButtons.Add(installBtn = Ui.Btn("分期", Ui.Kind.OnHero, Ops.Installments, 36));
        hero = Ui.HeroCard(new Grid { Children = { new VerticalStackLayout { Spacing = 2, Children = { heroLabel, balanceText, balanceSub, cardButtons } }, cuteDeco } })
            .OnTap(async () => { if (bill != null) await Dialogs.Info(heroLabel.Text, bill.Tip); });

        // ---- 期間 ----
        var prev = Ui.Btn("‹", Ui.Kind.Ghost, () => { Shift(-1); return Task.CompletedTask; }, 38);
        var next = Ui.Btn("›", Ui.Kind.Ghost, () => { Shift(1); return Task.CompletedTask; }, 38);
        prev.WidthRequest = next.WidthRequest = 44;
        periodText.HorizontalOptions = LayoutOptions.Center;
        periodText.VerticalOptions = LayoutOptions.Center;
        periodText.OnTap(PickPeriod);
        var period = new Grid { ColumnSpacing = 6, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto) } };
        period.Add(prev, 0);
        period.Add(periodText, 1);
        period.Add(next, 2);
        period.Add(Ui.Btn("本月", Ui.Kind.Ghost, () => { Month = DateTime.Today.ToString("yyyy-MM"); Refresh(); return Task.CompletedTask; }, 38), 3);
        period.Add(Ui.Btn("全部", Ui.Kind.Ghost, () => { Month = ""; Refresh(); return Task.CompletedTask; }, 38), 4);

        var tiles = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) } };
        tiles.Add(inc.Card, 0);
        tiles.Add(exp.Card, 1);
        tiles.Add(net.Card, 2);

        // ---- 從圖表點分類過來的篩選 ----
        catChip = new Border
        {
            Padding = new Thickness(12, 6), StrokeThickness = 0, StrokeShape = Theme.Round(16), HorizontalOptions = LayoutOptions.Start,
            Content = new HorizontalStackLayout { Spacing = 8, Children = { catChipText, Ui.L("✕", 13, Theme.Muted) } },
        }.Fill(Theme.Sel).OnTap(() => { CatFilter = null; Refresh(); return Task.CompletedTask; });

        list.Header = new VerticalStackLayout
        {
            Spacing = 12, Padding = new Thickness(16, 16, 16, 4),
            Children = { Header("收支記錄", DateTime.Today.ToString("yyyy 年 M 月 d 日 dddd")), top, hero, period, tiles, catChip, countText },
        };
        list.Footer = new BoxView { HeightRequest = 90, Color = Colors.Transparent };
        list.EmptyView = new Label { Text = "這段期間沒有記錄\n按右下角的「+」記一筆", HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(20, 40), FontSize = 14 }.Fg(Theme.Subtle);
        list.ItemTemplate = new DataTemplate(Row);

        Content = new Grid { Children = { list, Ui.Fab(() => AddRecordPage.Open()) } };
    }

    View Row()
    {
        var title = new Label { FontSize = 15, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 }.Fg(Theme.Text);
        title.SetBinding(Label.TextProperty, static (RecItem i) => i.Title);
        var tagText = new Label { FontSize = 11, FontAttributes = FontAttributes.Bold };
        tagText.SetBinding(Label.TextProperty, static (RecItem i) => i.Tag);
        tagText.SetBinding(Label.TextColorProperty, static (RecItem i) => i.TagFg);
        var tag = new Border { Content = tagText, Padding = new Thickness(7, 1), StrokeThickness = 0, StrokeShape = Theme.Round(9), VerticalOptions = LayoutOptions.Center };
        tag.SetBinding(Border.BackgroundColorProperty, static (RecItem i) => i.TagBg);
        var sub = Ui.L1("", 12, Theme.Muted);
        sub.SetBinding(Label.TextProperty, static (RecItem i) => i.Sub);
        var amount = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
        amount.SetBinding(Label.TextProperty, static (RecItem i) => i.Amount);
        amount.SetBinding(Label.TextColorProperty, static (RecItem i) => i.AmountColor);

        var line1 = new HorizontalStackLayout { Spacing = 8, Children = { tag, title } };
        var grid = new Grid
        {
            ColumnSpacing = 10, RowSpacing = 3,
            ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto) },
        };
        grid.Add(line1, 0, 0);
        grid.Add(sub, 0, 1);
        grid.Add(amount, 1, 0);
        Grid.SetRowSpan(amount, 2);
        var card = new Border { Content = grid, Padding = new Thickness(14, 10), Margin = new Thickness(16, 0, 16, 8), StrokeThickness = 1, StrokeShape = Theme.Round(12) }
            .Fill(Theme.Surface).Stroke_(Theme.Line);
        card.OnTap(async () => { if (card.BindingContext is RecItem it) await ItemMenu(it.Row); });
        return card;
    }

    static async Task ItemMenu(RecordRow r)
    {
        var title = $"{r.Date} · {r.Category} · {r.AmountText}";
        var act = await Dialogs.Menu(title, r is DebtMoveRow { Payment: false } ? ["編輯欠款"] : ["編輯", "刪除"]);
        if (act == "刪除") await Ops.DeleteRecord(r);
        else if (act != null) await Ops.EditRecord(r);
    }

    void Shift(int delta)
    {
        Month = ShiftPeriod(Month == "" ? DateTime.Today.ToString("yyyy-MM") : Month, delta);
        Refresh();
    }

    async Task PickPeriod()
    {
        var v = await Dialogs.Prompt("選擇期間", "輸入月份(例如 2026-03)或年份(例如 2026);留空為全部期間。",
            [new Field("期間", Month, "YYYY-MM 或 YYYY", Numeric: false)],
            vals => vals[0] == "" || (vals[0].Length == 4 && int.TryParse(vals[0], out _)) ||
                    DateTime.TryParseExact(vals[0], "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out _)
                ? null : "格式為 YYYY-MM 或 YYYY");
        if (v == null) return;
        Month = v[0];
        Refresh();
    }

    public override void Refresh()
    {
        var wallets = Db.Wallets();
        var rows = FilteredRecords();
        // 轉帳(轉移資金、繳卡費)與經過錢包的借款、還款也列在清單裡,但不算進收入、支出
        var shown = CatFilter == null
            ? rows.Concat(Db.Transfers(Month, SelWallet)).Concat(Db.DebtMoves(Month, SelWallet))
                  .OrderByDescending(r => r.Date, StringComparer.Ordinal).ToList()
            : rows;
        list.ItemsSource = shown.Select(Item).ToList();

        double incSum = rows.Where(r => r.IsIncome).Sum(r => r.Amount), expSum = rows.Where(r => !r.IsIncome).Sum(r => r.Amount);
        double bal = wallets.Where(w => SelWallet == null || w.Id == SelWallet).Sum(w => w.Balance);
        var name = WalletName;
        walletText.Text = name;
        var periodName = Month == "" ? "全部期間" : Month;
        periodText.Text = PeriodName(Month);

        // 選到信用卡時,大卡片改成顯示本期應繳與繳款日
        var card = wallets.FirstOrDefault(w => w.Id == SelWallet && w.IsCard);
        bill = card != null ? Db.Bill(card) : null;
        heroLabel.Text = card != null ? $"{card.Name} · 本期應繳" : SelWallet == null ? "目前總餘額" : $"{name} · 餘額";
        balanceText.Text = Money(bill?.Remaining ?? bal);
        // 錢包設成可愛風:粉嫩漸層、圓一點的卡片、右上角小動物
        var selected = wallets.FirstOrDefault(w => w.Id == SelWallet);
        bool cute = selected?.IsCute == true;
        walletIcon.Text = cute ? Theme.CuteIcon(selected!) : "👛";
        if (cute) hero.CuteHero(); else hero.Hero();
        hero.StrokeShape = Theme.Round(cute ? 26 : 18);
        cuteDeco.IsVisible = cute;
        cuteDeco.Text = cute ? Theme.CuteIcon(selected!) : "";
        heroLabel.Fg(cute ? Theme.OnCuteHeroMuted : Theme.OnHeroMuted);
        balanceSub.Fg(cute ? Theme.OnCuteHeroMuted : Theme.OnHeroMuted);
        foreach (var b in new[] { payBtn, installBtn })
        {
            var (bg, fg) = cute ? (new Pair(Color.FromArgb("#B3FFFFFF"), Color.FromArgb("#33FFFFFF")), Theme.CuteAccent)
                                : (new Pair(Color.FromArgb("#33FFFFFF"), Color.FromArgb("#33FFFFFF")), Theme.OnHero);
            b.SetAppThemeColor(Button.BackgroundColorProperty, bg.Light, bg.Dark);
            b.SetAppThemeColor(Button.TextColorProperty, fg.Light, fg.Dark);
        }
        var color = cute
            ? bill?.Late == true ? Theme.Warn : bill != null || bal >= 0 ? Theme.OnCuteHero : Theme.Exp
            : bill?.Late == true ? Theme.HeroWarn : bill != null || bal >= 0 ? Theme.OnHero : Theme.HeroExp;
        balanceText.TextColor = color.Now;
        balanceSub.Text = bill?.Detail ?? (SelWallet == null ? $"全部錢包 · 流動資金 {Money(Liquid(wallets))}"
                          : wallets.FirstOrDefault(w => w.Id == SelWallet)?.IsSavings == true ? "存錢用錢包,不算流動資金" : "");
        balanceSub.IsVisible = balanceSub.Text != "";
        cardButtons.IsVisible = card != null;

        inc.Value.Text = Money(incSum);
        exp.Value.Text = Money(expSum);
        net.Value.Text = (incSum - expSum > 0 ? "+" : "") + Money(incSum - expSum);
        net.Value.TextColor = (incSum >= expSum ? Theme.Inc : Theme.Exp).Now;
        inc.Sub.Text = $"{rows.Count(r => r.IsIncome)} 筆";
        exp.Sub.Text = $"{rows.Count(r => !r.IsIncome)} 筆";
        net.Sub.Text = periodName;
        catChip.IsVisible = CatFilter != null;
        catChipText.Text = $"只看 {Database.Expense} · {CatFilter}";
        countText.Text = $"共 {shown.Count} 筆 · 點一筆可以編輯或刪除";
    }

    static RecItem Item(RecordRow r)
    {
        var (tag, fg, bg) = r switch
        {
            { IsTransfer: true } => (r.Type, Theme.Muted, Theme.Ghost),
            { IsIncome: true } => (r.Type, Theme.Inc, Theme.IncSoft),
            _ => (r.Type, Theme.Exp, Theme.ExpSoft),
        };
        var amountColor = r.IsTransfer ? Theme.Text : r.IsIncome ? Theme.Inc : Theme.Exp;
        var parts = new List<string> { r.Date, r.Wallet };
        if (r.NoteText != "") parts.Add(r.NoteText);
        return new RecItem(r, r.Category, tag, fg.Now, bg.Now, string.Join(" · ", parts), r.AmountText, amountColor.Now);
    }
}
