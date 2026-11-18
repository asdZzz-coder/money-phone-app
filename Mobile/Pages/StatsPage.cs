using static ZZZ.Mobile.State;

namespace ZZZ.Mobile;

/// <summary>
/// 支出圖表(唯讀):跟著目前選的錢包,有自己的期間(月、年、全部)。
/// 點圓環的一塊會在中間顯示該分類,再點一次(或點下方列表)就切到收支記錄只看那個分類;
/// 點長條圖的某個月會切到那個月。
/// </summary>
public class StatsPage : TabPage
{
    readonly Label periodText = Ui.L1("", 17, bold: true);
    readonly Label walletText = Ui.L1("", 13, Theme.Muted);
    readonly Button prev, next;
    readonly (Border Card, Label Value, Label Sub) expense = Ui.Tile("總支出", Theme.Exp), compare = Ui.Tile("與上一期相比"),
        avg = Ui.Tile("平均每天"), max = Ui.Tile("最大一筆");
    readonly DonutDrawable donut = new();
    readonly GraphicsView donutView;
    readonly VerticalStackLayout legend = new() { Spacing = 0 };
    readonly MonthBarsDrawable bars = new();
    readonly GraphicsView barsView;
    readonly Label barTitle = Ui.L1("", 15, bold: true);

    public StatsPage() : base("支出圖表")
    {
        prev = Ui.Btn("‹", Ui.Kind.Ghost, () => Go(ShiftPeriod(StatsPeriod, -1)), 38);
        next = Ui.Btn("›", Ui.Kind.Ghost, () => Go(ShiftPeriod(StatsPeriod, 1)), 38);
        prev.WidthRequest = next.WidthRequest = 44;
        periodText.HorizontalOptions = LayoutOptions.Center;
        periodText.VerticalOptions = LayoutOptions.Center;
        var nav = new Grid { ColumnSpacing = 6, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        nav.Add(prev, 0);
        nav.Add(periodText, 1);
        nav.Add(next, 2);
        var quick = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) } };
        quick.Add(Ui.Btn("本月", Ui.Kind.Ghost, () => Go(DateTime.Today.ToString("yyyy-MM")), 36), 0);
        quick.Add(Ui.Btn("今年", Ui.Kind.Ghost, () => Go(DateTime.Today.Year.ToString()), 36), 1);
        quick.Add(Ui.Btn("全部", Ui.Kind.Ghost, () => Go(""), 36), 2);

        var tiles = new Grid { ColumnSpacing = 8, RowSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) }, RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto) } };
        tiles.Add(expense.Card, 0, 0);
        tiles.Add(compare.Card, 1, 0);
        tiles.Add(avg.Card, 0, 1);
        tiles.Add(max.Card, 1, 1);

        donutView = new GraphicsView { Drawable = donut, HeightRequest = 240 };
        var donutTap = new TapGestureRecognizer();
        donutTap.Tapped += (_, e) =>
        {
            if (e.GetPosition(donutView) is not Point p) return;
            var hit = donut.HitTest(new PointF((float)p.X, (float)p.Y), new RectF(0, 0, (float)donutView.Width, (float)donutView.Height));
            if (hit != null && hit == donut.Highlighted) { Ui.Run(() => ShowCategory(hit)); return; }
            donut.Highlighted = hit;
            donutView.Invalidate();
        };
        donutView.GestureRecognizers.Add(donutTap);

        barsView = new GraphicsView { Drawable = bars, HeightRequest = 220 };
        var barsTap = new TapGestureRecognizer();
        barsTap.Tapped += (_, e) =>
        {
            if (e.GetPosition(barsView) is not Point p) return;
            if (bars.HitTest(new PointF((float)p.X, (float)p.Y), new RectF(0, 0, (float)barsView.Width, (float)barsView.Height)) is string m)
                Ui.Run(() => Go(m));
        };
        barsView.GestureRecognizers.Add(barsTap);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 12, Padding = new Thickness(16, 16, 16, 32),
                Children =
                {
                    Header("支出圖表"), walletText, nav, quick, tiles,
                    Ui.Card(new VerticalStackLayout { Spacing = 8, Children = { Ui.L1("支出分類", 15, bold: true), Ui.L("點一塊看金額,再點一次看明細", 12, Theme.Subtle), donutView, legend } }),
                    Ui.Card(new VerticalStackLayout { Spacing = 8, Children = { barTitle, Ui.L("綠色收入、紅色支出,折線為結餘;點某個月切換過去", 12, Theme.Subtle), barsView } }),
                },
            },
        };
    }

    Task Go(string period)
    {
        StatsPeriod = period;
        donut.Highlighted = null;
        Refresh();
        return Task.CompletedTask;
    }

    static Task ShowCategory(string category)
    {
        CatFilter = category;
        Month = StatsPeriod;
        NotifyChanged();
        return Shell.Current.GoToAsync("//records");
    }

    static DateTime ThisMonth => new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public override void Refresh()
    {
        var p = StatsPeriod;
        bool isYear = p.Length == 4;
        var spent = Db.Records(p, SelWallet).Where(r => !r.IsIncome).ToList();
        double total = spent.Sum(r => r.Amount);

        periodText.Text = PeriodName(p);
        prev.IsEnabled = next.IsEnabled = p != "";
        walletText.Text = $"錢包:{WalletName}(在「收支記錄」切換)";
        expense.Value.Text = Money(total);
        expense.Sub.Text = $"{spent.Count} 筆";

        // 與上一期相比
        var last = isYear ? "去年" : "上個月";
        compare.Value.TextColor = Theme.Text.Now;
        if (p == "")
        {
            compare.Value.Text = "—";
            compare.Sub.Text = "選擇月份或年份才能比較";
        }
        else
        {
            double before = Db.Records(ShiftPeriod(p, -1), SelWallet).Where(r => !r.IsIncome).Sum(r => r.Amount);
            double diff = total - before;
            if (before <= 0)
            {
                compare.Value.Text = "—";
                compare.Sub.Text = $"{last}沒有支出";
            }
            else
            {
                compare.Value.Text = (diff > 0 ? "+" : diff < 0 ? "−" : "") + $"{Math.Abs(diff) / before:P0}";
                compare.Value.TextColor = (diff > 0 ? Theme.Exp : diff < 0 ? Theme.Inc : Theme.Text).Now;
                compare.Sub.Text = diff > 0 ? $"比{last}多花 {Money(diff)}" : diff < 0 ? $"比{last}少花 {Money(-diff)}" : $"跟{last}一樣";
            }
        }

        var (days, daysNote) = CountDays(p, spent);
        avg.Value.Text = Money(days > 0 ? total / days : 0);
        avg.Sub.Text = days > 0 ? $"{daysNote}共 {days} 天" : "這段期間沒有支出";
        var big = spent.MaxBy(r => r.Amount);
        max.Value.Text = big == null ? "—" : Money(big.Amount);
        max.Sub.Text = big == null ? "這段期間沒有支出" : $"{big.Category} · {big.Date}" + (big.Note != "" ? $" · {big.Note}" : "");

        // 分類圓環圖與列表
        var cats = Db.Categories(Database.Expense);
        var stats = spent.GroupBy(r => r.Category)
            .Select(g => (Name: g.Key, Amount: g.Sum(r => r.Amount), Count: g.Count()))
            .OrderByDescending(x => x.Amount)
            .Select(x => new CategoryStat(x.Name, x.Amount, x.Count, total > 0 ? x.Amount / total : 0, ChartPalette.For(x.Name, cats)))
            .ToList();
        donut.SetData(stats, "總支出", Money(total), $"{stats.Count} 個分類");
        donutView.Invalidate();
        legend.Children.Clear();
        if (stats.Count == 0) legend.Add(Ui.L("這段期間沒有支出", 13, Theme.Subtle));
        foreach (var s in stats)
        {
            var row = new Grid { ColumnSpacing = 10, Padding = new Thickness(0, 9), ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(new GridLength(56)) } };
            row.Add(new BoxView { Color = s.Color, WidthRequest = 12, HeightRequest = 12, CornerRadius = 3, VerticalOptions = LayoutOptions.Center }, 0);
            row.Add(new VerticalStackLayout { Children = { Ui.L1(s.Name, 14, bold: true), Ui.L1($"{s.Count} 筆", 11, Theme.Subtle) } }, 1);
            row.Add(new Label { Text = s.AmountText, FontSize = 14, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }.Fg(Theme.Text), 2);
            row.Add(new Label { Text = s.ShareText, FontSize = 12, HorizontalTextAlignment = TextAlignment.End, VerticalOptions = LayoutOptions.Center }.Fg(Theme.Muted), 3);
            var name = s.Name;
            row.OnTap(() => ShowCategory(name));
            legend.Add(row);
        }

        // 每月收支:選整年時顯示該年 1~12 月;選月份時顯示近 12 個月,選到更早的月份則以它為最後一個月
        DateTime end = ThisMonth;
        if (isYear) end = new DateTime(int.Parse(p), 12, 1);
        else if (p != "")
        {
            var sel = DateTime.ParseExact(p, "yyyy-MM", null);
            if (sel > end || sel <= end.AddMonths(-12)) end = sel;
        }
        var start = end.AddMonths(-11);
        var totals = Db.MonthTotals(SelWallet).ToDictionary(m => m.Month);
        var months = Enumerable.Range(0, 12)
            .Select(i => start.AddMonths(i).ToString("yyyy-MM"))
            .Select(m => totals.TryGetValue(m, out var t) ? t : new MonthTotal(m, 0, 0))
            .ToList();
        bars.SetData(months, isYear || p == "" ? null : p);
        barsView.Invalidate();
        barTitle.Text = isYear ? $"{p} 年每月收支" : end == ThisMonth ? "近 12 個月收支" : $"{start:yyyy/M} – {end:yyyy/M} 收支";
    }

    /// <summary>計算平均用的天數;本月與今年只算到今天,全部期間從第一筆支出算起。</summary>
    static (int Days, string Note) CountDays(string p, List<RecordRow> spent)
    {
        var today = DateTime.Today;
        if (p == "")
        {
            if (spent.Count == 0) return (0, "");
            var first = spent.Select(r => DateTime.TryParse(r.Date, out var d) ? d : today).Min();
            return (Math.Max((today - first).Days + 1, 1), $"從 {first:yyyy-MM-dd} 起");
        }
        if (p.Length == 4)
        {
            int y = int.Parse(p);
            return y == today.Year ? (today.DayOfYear, "今年到今天") : (DateTime.IsLeapYear(y) ? 366 : 365, "");
        }
        var m = DateTime.ParseExact(p, "yyyy-MM", null);
        return m == ThisMonth ? (today.Day, "本月到今天") : (DateTime.DaysInMonth(m.Year, m.Month), "");
    }
}
