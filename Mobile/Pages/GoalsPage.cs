using static ZZZ.Mobile.State;

namespace ZZZ.Mobile;

/// <summary>存錢目標:整體進度、每個目標一張卡片(存入、取出、每年繳費的「繳費」、紀錄、編輯、刪除)。</summary>
public class GoalsPage : TabPage
{
    readonly VerticalStackLayout cards = new() { Spacing = 10 };
    readonly Label savedText = new Label { FontSize = 32, FontAttributes = FontAttributes.Bold }.Fg(Theme.OnHero);
    readonly Label pctText = Ui.L1("", 13, Theme.OnHeroMuted);
    readonly ContentView totalBar = new();
    readonly (Border Card, Label Value, Label Sub) target = Ui.Tile("目標總額"), done = Ui.Tile("已達成");

    public GoalsPage() : base("存錢目標")
    {
        var hero = Ui.HeroCard(new VerticalStackLayout
        {
            Spacing = 4, Children = { Ui.L1("目前已存", 13, Theme.OnHeroMuted), savedText, totalBar, pctText },
        });
        var tiles = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) } };
        tiles.Add(target.Card, 0);
        tiles.Add(done.Card, 1);
        var scroll = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 12, Padding = new Thickness(16, 16, 16, 100),
                Children = { Header("存錢目標", "旅行基金、緊急預備金,或每年要繳的稅費"), hero, tiles, cards },
            },
        };
        Content = new Grid { Children = { scroll, Ui.Fab(Ops.AddGoal) } };
    }

    public override void Refresh()
    {
        var goals = Db.Goals();
        double saved = goals.Sum(g => g.Saved), total = goals.Sum(g => g.Target);
        double pct = total > 0 ? Math.Clamp(goals.Sum(g => Math.Min(g.Saved, g.Target)) / total, 0, 1) : 0;
        savedText.Text = Money(saved);
        totalBar.Content = Ui.Bar(pct, Theme.Inc, 8);
        pctText.Text = $"整體進度 {pct:P0}";
        target.Value.Text = Money(total);
        target.Sub.Text = $"{goals.Count} 個目標 · 還差 {Money(goals.Sum(g => g.Remaining))}";
        int n = goals.Count(g => g.Done), toPay = goals.Count(g => g.Yearly && g.Late), late = goals.Count(g => !g.Yearly && g.Late);
        done.Value.Text = $"{n} / {goals.Count}";
        done.Sub.Text = toPay > 0 ? $"{toPay} 筆費用該繳了" : late > 0 ? $"{late} 個目標已過期"
                      : n == goals.Count && n > 0 ? "全部達成,太棒了!" : "繼續加油";
        cards.Children.Clear();
        if (goals.Count == 0)
            cards.Add(new Label { Text = Theme.Cutely("還沒有存錢目標\n按右下角的「+」建立第一個", "🐷"), HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(20, 30), FontSize = 14 }.Fg(Theme.Subtle));
        foreach (var g in goals) cards.Add(Card(g));
    }

    static View Card(GoalRow g)
    {
        var (pfg, pbg) = g.Late ? (Theme.Warn, Theme.WarnSoft) : g.Done ? (Theme.Inc, Theme.IncSoft) : (Theme.Muted, Theme.Ghost);
        var initial = new Border
        {
            WidthRequest = 42, HeightRequest = 42, StrokeThickness = 0, StrokeShape = Theme.Round(12), VerticalOptions = LayoutOptions.Center,
            Content = new Label { Text = g.Initial, FontSize = 18, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }.Fg(Theme.Ink),
        }.Fill(Theme.Ghost);
        var name = new HorizontalStackLayout { Spacing = 8, Children = { Ui.L1(g.Name, 16, bold: true), Ui.Pill(g.Status, pfg, pbg) } };
        var head = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        head.Add(initial, 0);
        head.Add(new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { name, Ui.L(g.SubText, 12, Theme.Muted) } }, 1);

        var amounts = new HorizontalStackLayout
        {
            Spacing = 6, Children =
            {
                Ui.L(g.SavedText, 22, bold: true),
                new Label { Text = g.TargetText, FontSize = 14, VerticalOptions = LayoutOptions.End, Margin = new Thickness(0, 0, 0, 3) }.Fg(Theme.Muted),
                new Label { Text = g.PercentText, FontSize = 13, VerticalOptions = LayoutOptions.End, Margin = new Thickness(8, 0, 0, 3), FontAttributes = FontAttributes.Bold }.Fg(g.Done ? Theme.Inc : Theme.Ink),
            },
        };
        var buttons = new Grid { ColumnSpacing = 8 };
        var list = new List<Button>
        {
            Ui.Btn("存入", Ui.Kind.Accent, () => Ops.GoalMove(g, deposit: true), 38),
            Ui.Btn("取出", Ui.Kind.Ghost, () => Ops.GoalMove(g, deposit: false), 38),
        };
        if (g.Yearly) list.Add(Ui.Btn("繳費", g.Late ? Ui.Kind.Danger : Ui.Kind.Ghost, () => Ops.PayGoal(g), 38));
        var more = Ui.Btn("⋯", Ui.Kind.Ghost, () => Menu(g), 38);
        for (int i = 0; i < list.Count; i++)
        {
            buttons.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            buttons.Add(list[i], i);
        }
        buttons.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(48)));
        buttons.Add(more, list.Count);

        return Ui.Card(new VerticalStackLayout
        {
            Spacing = 8,
            Children = { head, amounts, Ui.Bar(g.Progress, g.Late ? Theme.Warn : g.Done ? Theme.Inc : Theme.Ink, 8), Ui.L(g.Hint, 13, g.Late ? Theme.Warn : Theme.Muted), buttons },
        });
    }

    static async Task Menu(GoalRow g)
    {
        switch (await Dialogs.Menu(g.Name, $"存取紀錄({g.Count} 筆)", "編輯", "刪除"))
        {
            case "編輯": await Ops.EditGoal(g); break;
            case "刪除": await Ops.DeleteGoal(g); break;
            case string s when s.StartsWith("存取紀錄"):
                if (Application.Current?.Windows.FirstOrDefault()?.Page is Shell shell) await shell.Navigation.PushAsync(new DepositsPage(g.Id));
                break;
        }
    }
}

/// <summary>一個目標的存入與取出紀錄;點一筆可以編輯或刪除。</summary>
public class DepositsPage : SubPage
{
    readonly long goalId;
    readonly VerticalStackLayout items = new() { Spacing = 8 };
    readonly Label head = Ui.L("", 14, Theme.Muted);

    public DepositsPage(long goalId) : base("存取紀錄")
    {
        this.goalId = goalId;
        Body = new ScrollView { Content = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(16, 12, 16, 32), Children = { head, items } } };
        State.Changed += () => { if (Window != null) Refresh(); }; // 關掉的頁面(或換主題前的舊畫面)不再更新
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    void Refresh()
    {
        var g = Db.Goals().FirstOrDefault(x => x.Id == goalId);
        items.Children.Clear();
        if (g == null) { head.Text = "這個目標已刪除"; return; }
        Title = g.Name;
        head.Text = (g.Linked ? $"存在「{g.WalletName}」 · " : "") + $"已存 {Money(g.Saved)} / {Money(g.Target)} · {g.Count} 筆";
        var list = Db.Deposits(g.Id);
        if (list.Count == 0) items.Add(Ui.L(Theme.Cutely("還沒有存取紀錄\n回到目標按「存入」開始存錢吧", "🐷"), 14, Theme.Subtle));
        foreach (var d in list)
        {
            var grid = new Grid { ColumnSpacing = 10, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
            grid.Add(new VerticalStackLayout { Spacing = 2, Children = { Ui.L1(d.Title, 15, bold: true), Ui.L1(d.Date, 12, Theme.Muted) } }, 0);
            grid.Add(new Label { Text = d.AmountText, FontSize = 16, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }
                .Fg(d.IsDeposit ? Theme.Inc : Theme.Exp), 1);
            items.Add(Ui.Card(grid, 12).OnTap(async () =>
            {
                switch (await Dialogs.Menu($"{d.Date} · {d.AmountText}", "編輯", "刪除"))
                {
                    case "編輯": await Ops.EditDeposit(g, d); break;
                    case "刪除": await Ops.DeleteDeposit(d); break;
                }
            }));
        }
    }
}
