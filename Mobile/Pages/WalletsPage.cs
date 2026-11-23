using static ZZZ.Mobile.State;

namespace ZZZ.Mobile;

/// <summary>
/// 錢包清單(取代電腦版的側欄):點一個錢包就只看它的記錄;「⋯」可以編輯、排序、刪除。
/// 名稱下方一行和電腦版相同:信用卡的應繳金額、放了存錢目標的錢包還沒分配的錢、存錢用錢包的說明。
/// </summary>
public class WalletsPage : SubPage
{
    readonly VerticalStackLayout items = new() { Spacing = 8 };

    public WalletsPage() : base("錢包")
    {
        var buttons = new Grid { ColumnSpacing = 10, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) } };
        buttons.Add(Ui.Btn("+ 新增錢包", Ui.Kind.Accent, async () => { await Ops.AddWallet(); Refresh(); }), 0);
        buttons.Add(Ui.Btn("⇄ 轉移資金", Ui.Kind.Outline, Ops.Transfer), 1);
        Body = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 12, Padding = new Thickness(16, 12, 16, 32),
                Children = { buttons, items, Ui.L("點錢包只看它的記錄;按「⋯」編輯、調整順序或刪除。", 12, Theme.Subtle) },
            },
        };
        State.Changed += () => { if (Window != null) Refresh(); }; // 關掉的頁面(或換主題前的舊畫面)不再更新
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    void Refresh()
    {
        var wallets = Db.Wallets();
        var goals = Db.Goals().Where(g => g.Linked).GroupBy(g => g.WalletId!.Value).ToDictionary(x => x.Key);
        items.Children.Clear();
        double savings = wallets.Where(w => w.IsSavings).Sum(w => w.Balance), owed = wallets.Sum(w => w.IsCard ? w.Owed : 0);
        items.Add(Item(null, "🗂", "全部錢包", wallets.Sum(w => w.Balance), $"流動資金 {Money(Liquid(wallets))}", false,
                       $"流動資金 = 一般錢包的餘額合計\n不含存錢用錢包({Money(savings)})與信用卡" +
                       (owed > 0 ? $"\n總餘額已扣掉信用卡欠款 {Money(owed)}" : "")));
        foreach (var w in wallets)
        {
            var bill = w.IsCard ? Db.Bill(w) : null;
            string sub;
            bool warn = false;
            string? tip = null;
            if (bill != null) { sub = bill.Short; warn = bill.Late; tip = bill.Tip; }
            else if (goals.TryGetValue(w.Id, out var gs))
            {
                double free = w.Balance - gs.Sum(g => g.Saved);
                warn = free < -0.0001;
                sub = warn ? $"分配超出 {Money(-free)}" : $"未分配 {Money(free)}";
                tip = $"存錢目標:{string.Join("、", gs.Select(g => g.Name))}\n已分配給目標 {Money(gs.Sum(g => g.Saved))},未分配 {Money(free)}";
            }
            else sub = w.IsSavings ? "存錢用,不算流動資金" : "";
            var icon = w.IsCute ? Theme.CuteIcon(w) : w.IsCard ? "💳" : w.IsSavings || goals.ContainsKey(w.Id) ? "🐷" : "👛";
            items.Add(Item(w, icon, w.Name, w.Balance, sub, warn, tip));
        }
    }

    View Item(Wallet? w, string icon, string name, double balance, string sub, bool warn, string? tip)
    {
        bool selected = SelWallet == w?.Id, cute = w?.IsCute == true;
        var text = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { Ui.L1(name, 15, bold: true) } };
        if (sub != "") text.Add(Ui.L1(sub, 12, warn ? Theme.Warn : cute ? Theme.CuteAccent : Theme.Muted));
        var grid = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) } };
        grid.Add(new Label { Text = icon, FontSize = 22, VerticalOptions = LayoutOptions.Center }, 0);
        grid.Add(text, 1);
        grid.Add(new Label { Text = Money(balance), FontSize = 16, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }
            .Fg(balance < 0 ? Theme.Exp : Theme.Text), 2);
        if (w != null)
        {
            var more = Ui.Btn("⋯", Ui.Kind.Ghost, () => Menu(w), 36);
            more.WidthRequest = 40;
            more.Padding = 0;
            grid.Add(more, 3);
        }
        else if (tip != null)
        {
            var info = Ui.Btn("?", Ui.Kind.Ghost, () => Dialogs.Info(name, tip), 36);
            info.WidthRequest = 40;
            info.Padding = 0;
            grid.Add(info, 3);
        }
        var card = new Border { Content = grid, Padding = new Thickness(14, 12), StrokeThickness = selected ? 2 : 1, StrokeShape = Theme.Round(cute ? 22 : 12) }
            .Fill(cute ? Theme.CuteSoft : selected ? Theme.Sel : Theme.Surface)
            .Stroke_(selected ? (cute ? Theme.CuteAccent : Theme.Ink) : cute ? Theme.CuteLine : Theme.Line);
        card.OnTap(async () =>
        {
            SelWallet = w?.Id;
            CatFilter = null;
            NotifyChanged();
            await Navigation.PopAsync();
            // 從「更多」進來的也切到收支記錄,看選到的錢包
            if (Shell.Current is { } shell && shell.CurrentState.Location.OriginalString.Contains("records") != true)
                await shell.GoToAsync("//records");
        });
        return card;
    }

    async Task Menu(Wallet w)
    {
        var bill = w.IsCard ? Db.Bill(w) : null;
        var actions = new List<string> { "編輯錢包", "設定風格(一般 / 可愛)", "往上移", "往下移" };
        if (bill != null) actions.Insert(0, "帳單明細");
        actions.Add("刪除錢包");
        var act = await Dialogs.Menu(w.Name, actions.ToArray());
        switch (act)
        {
            case "帳單明細": await Dialogs.Info(w.Name, bill!.Tip); break;
            case "編輯錢包": await Ops.EditWallet(w.Id); break;
            case "設定風格(一般 / 可愛)": await Ops.SetStyle(w); break;
            case "往上移": Ops.MoveWallet(w.Id, -1); break;
            case "往下移": Ops.MoveWallet(w.Id, 1); break;
            case "刪除錢包": await Ops.DeleteWallet(w.Id); break;
        }
        Refresh();
    }
}
