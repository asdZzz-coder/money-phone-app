using static ZZZ.Mobile.State;

namespace ZZZ.Mobile;

public record DebtItem(DebtRow Row, string Person, string Status, Color StatusFg, Color StatusBg, string Line,
                       string Rest, Color RestColor, double Progress, Color ProgressColor, string Sub);

/// <summary>欠款紀錄:淨額大卡片、別人欠我/我欠別人、篩選、清單。點一筆可以記錄還款、編輯、標記還清或刪除。</summary>
public class DebtsPage : TabPage
{
    readonly CollectionView list = new() { SelectionMode = SelectionMode.None };
    readonly Label netText = new() { FontSize = 32, FontAttributes = FontAttributes.Bold };
    readonly Label netSub = Ui.L("", 13, Theme.OnHeroMuted);
    readonly (Border Card, Label Value, Label Sub) lent = Ui.Tile("別人欠我", Theme.Inc), owe = Ui.Tile("我欠別人", Theme.Exp);
    readonly Label countText = Ui.L1("", 13, Theme.Muted);
    readonly Label emptyText = new() { HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(20, 40), FontSize = 14 };
    int filter; // 0 未還清、1 已還清、2 全部

    public DebtsPage() : base("欠款")
    {
        var hero = Ui.HeroCard(new VerticalStackLayout { Spacing = 2, Children = { Ui.L1("欠款淨額(別人欠我 − 我欠別人)", 13, Theme.OnHeroMuted), netText, netSub } });
        var tiles = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) } };
        tiles.Add(lent.Card, 0);
        tiles.Add(owe.Card, 1);
        var (segment, _) = Ui.Segment(["未還清", "已還清", "全部"], 0, i => { filter = i; Refresh(); });
        list.Header = new VerticalStackLayout
        {
            Spacing = 12, Padding = new Thickness(16, 16, 16, 4),
            Children = { Header("欠款紀錄", "借給別人、跟別人借的錢"), hero, tiles, segment, countText },
        };
        list.Footer = new BoxView { HeightRequest = 90, Color = Colors.Transparent };
        list.EmptyView = emptyText.Fg(Theme.Subtle);
        list.ItemTemplate = new DataTemplate(Row);
        Content = new Grid { Children = { list, Ui.Fab(Ops.AddDebt) } };
    }

    View Row()
    {
        var person = Ui.L1("", 16, bold: true);
        person.SetBinding(Label.TextProperty, static (DebtItem i) => i.Person);
        var statusText = new Label { FontSize = 11, FontAttributes = FontAttributes.Bold };
        statusText.SetBinding(Label.TextProperty, static (DebtItem i) => i.Status);
        statusText.SetBinding(Label.TextColorProperty, static (DebtItem i) => i.StatusFg);
        var status = new Border { Content = statusText, Padding = new Thickness(8, 2), StrokeThickness = 0, StrokeShape = Theme.Round(9), VerticalOptions = LayoutOptions.Center };
        status.SetBinding(Border.BackgroundColorProperty, static (DebtItem i) => i.StatusBg);
        var line = Ui.L1("", 13, Theme.Muted);
        line.SetBinding(Label.TextProperty, static (DebtItem i) => i.Line);
        var rest = new Label { FontSize = 17, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.End };
        rest.SetBinding(Label.TextProperty, static (DebtItem i) => i.Rest);
        rest.SetBinding(Label.TextColorProperty, static (DebtItem i) => i.RestColor);
        var bar = new ProgressBar { HeightRequest = 6, Margin = new Thickness(0, 4, 0, 0) };
        bar.SetBinding(ProgressBar.ProgressProperty, static (DebtItem i) => i.Progress);
        bar.SetBinding(ProgressBar.ProgressColorProperty, static (DebtItem i) => i.ProgressColor);
        var sub = Ui.L("", 12, Theme.Subtle);
        sub.SetBinding(Label.TextProperty, static (DebtItem i) => i.Sub);

        var top = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        top.Add(person, 0);
        top.Add(status, 1);
        status.HorizontalOptions = LayoutOptions.Start;
        top.Add(rest, 2);
        var card = new Border
        {
            Content = new VerticalStackLayout { Spacing = 3, Children = { top, line, bar, sub } },
            Padding = new Thickness(14, 12), Margin = new Thickness(16, 0, 16, 8), StrokeThickness = 1, StrokeShape = Theme.Round(12),
        }.Fill(Theme.Surface).Stroke_(Theme.Line);
        card.OnTap(async () => { if (card.BindingContext is DebtItem it) await Menu(it.Row); });
        return card;
    }

    static async Task Menu(DebtRow d)
    {
        var actions = d.Done ? new[] { "編輯", "刪除" } : ["記錄還款", "編輯", "標記還清", "刪除"];
        switch (await Dialogs.Menu($"{d.Person} · {d.Direction} · 未還 {Money(d.Rest)}", actions))
        {
            case "記錄還款": await Ops.PayDebt(d); break;
            case "編輯": await Ops.EditDebt(d); break;
            case "標記還清": await Ops.SettleDebt(d); break;
            case "刪除": await Ops.DeleteDebt(d); break;
        }
    }

    public override void Refresh()
    {
        var all = Db.Debts();
        double oweSum = all.Where(d => !d.Done && d.IOwe).Sum(d => d.Rest), lentSum = all.Where(d => !d.Done && !d.IOwe).Sum(d => d.Rest);
        var rows = filter == 1 ? all.Where(d => d.Done) : filter == 2 ? all : all.Where(d => !d.Done);
        var shown = rows.ToList();
        list.ItemsSource = shown.Select(Item).ToList();
        double net = lentSum - oweSum;
        netText.Text = (net > 0 ? "+" : "") + Money(net);
        netText.TextColor = (net >= 0 ? Theme.OnHero : Theme.HeroExp).Now;
        netSub.Text = net > 0 ? "整體而言別人欠你較多" : net < 0 ? "整體而言你欠別人較多" : "收支打平";
        lent.Value.Text = Money(lentSum);
        owe.Value.Text = Money(oweSum);
        lent.Sub.Text = $"{all.Count(d => !d.Done && !d.IOwe)} 筆未還";
        int late = all.Count(d => d.Late);
        owe.Sub.Text = $"{all.Count(d => !d.Done && d.IOwe)} 筆未還" + (late > 0 ? $" · {late} 筆逾期" : "");
        countText.Text = $"共 {shown.Count} 筆 · 點一筆可以記錄還款或編輯";
        emptyText.Text = Theme.Cutely(filter == 0 ? "沒有未還清的欠款\n按右下角的「+」新增" : "沒有符合的欠款紀錄", "🐻");
    }

    static DebtItem Item(DebtRow d)
    {
        var (fg, bg) = d.Done ? (Theme.Inc, Theme.IncSoft) : d.Late ? (Theme.Warn, Theme.WarnSoft) : (Theme.Muted, Theme.Ghost);
        var subs = new List<string> { d.Date };
        if (d.Due != "") subs.Add($"到期 {d.Due}");
        if (d.WalletId != null) subs.Add($"經過「{d.WalletName}」");
        if (d.Note != "") subs.Add(d.Note);
        return new DebtItem(d, d.Person, d.Status, fg.Now, bg.Now,
            $"{d.Direction} · 金額 {Money(d.Amount)} · {d.ProgressText}",
            d.Done ? "已還清" : $"未還 {Money(d.Rest)}", (d.Done ? Theme.Muted : d.IOwe ? Theme.Exp : Theme.Inc).Now,
            d.Progress, (d.Done ? Theme.Inc : Theme.Ink).Now, string.Join(" · ", subs));
    }
}
