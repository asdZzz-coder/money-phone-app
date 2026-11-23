using static ZZZ.Mobile.State;

namespace ZZZ.Mobile;

/// <summary>更多:錢包、分類、匯出匯入、刪除全部資料、外觀(跟隨系統/淺色/深色/可愛)、檢查更新、關於。</summary>
public class MorePage : TabPage
{
    readonly Label versionText = Ui.L1("", 12, Theme.Muted);

    public MorePage() : base("更多")
    {
        var themes = new[] { Theme.FollowSystem, Theme.LightMode, Theme.DarkMode, Theme.CuteTheme };
        var (themeSeg, _) = Ui.Segment(["跟隨系統", "淺色", "深色", "可愛 🎀"], Math.Max(Array.IndexOf(themes, Theme.Load()), 0), i =>
        {
            // 換進或換出可愛主題要重建畫面(等按鈕的點擊處理完再換),重建後停在「更多」
            if (Theme.Apply(themes[i])) Dispatcher.Dispatch(() => App.Rebuild(4));
        });

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 8, Padding = new Thickness(16, 16, 16, 32),
                Children =
                {
                    Header("更多"),
                    Ui.Section("錢包與分類"),
                    Row("👛", "錢包", "新增、編輯、排序、設定風格(一般 / 可愛)", () => Navigation.PushAsync(new WalletsPage())),
                    Row("🏷", "管理分類", "支出、收入的分類", () => Navigation.PushAsync(new CategoriesPage())),
                    Ui.Section("資料"),
                    Row("⬆", "匯出資料", "完整備份(.json)或收支記錄(.csv),可存到雲端硬碟或傳到電腦", Ops.Export),
                    Row("⬇", "匯入資料", "還原完整備份,或追加 CSV 收支記錄;電腦版匯出的檔案也可以", Ops.Import),
                    Row("🗄", "手機內的備份", "匯入前、刪除前自動存的備份", Ops.LocalBackups),
                    Row("⚠", "刪除全部資料", "會先自動備份,並要求再次確認", Ops.ClearAll, danger: true),
                    Ui.Section("外觀"),
                    themeSeg,
                    Ui.Section("關於"),
                    Row("↻", "檢查更新", "有新版會下載新的安裝檔(APK)", () => Ops.CheckUpdate(silent: false)),
                    Row("ℹ", "關於簡易記帳", "版本與資料位置", () => Dialogs.Info("關於簡易記帳",
                        $"版本 {Updater.CurrentVersion}\n資料位置:{Path.Combine(Database.DataDir, "ledger.db")}\n\n" +
                        "資料存在手機裡,解除安裝 App 會一起刪除,記得定期「匯出資料」備份。")),
                    versionText,
                },
            },
        };
    }

    static View Row(string icon, string title, string sub, Func<Task> action, bool danger = false)
    {
        var grid = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        grid.Add(new Border
        {
            WidthRequest = 36, HeightRequest = 36, StrokeThickness = 0, StrokeShape = Theme.Round(10), VerticalOptions = LayoutOptions.Center,
            Content = new Label { Text = icon, FontSize = 16, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }.Fg(danger ? Theme.Exp : Theme.Ink),
        }.Fill(danger ? Theme.ExpSoft : Theme.Ghost), 0);
        grid.Add(new VerticalStackLayout
        {
            Spacing = 2, VerticalOptions = LayoutOptions.Center,
            Children = { Ui.L1(title, 15, danger ? Theme.Exp : Theme.Text, bold: true), Ui.L(sub, 12, Theme.Muted) },
        }, 1);
        grid.Add(new Label { Text = "›", FontSize = 20, VerticalOptions = LayoutOptions.Center }.Fg(Theme.Subtle), 2);
        return Ui.Card(grid, 12).OnTap(action);
    }

    public override void Refresh() => versionText.Text = $"簡易記帳 Android 版 v{Updater.CurrentVersion}";
}

/// <summary>管理分類:支出、收入兩組,可新增或刪除(已存在的記錄不受影響)。</summary>
public class CategoriesPage : SubPage
{
    readonly VerticalStackLayout expList = new() { Spacing = 6 }, incList = new() { Spacing = 6 };
    readonly Entry expBox = new() { Placeholder = "新的支出分類", FontSize = 15 }, incBox = new() { Placeholder = "新的收入分類", FontSize = 15 };

    public CategoriesPage() : base("管理分類")
    {
        foreach (var e in new[] { expBox, incBox })
        {
            e.SetAppThemeColor(Entry.TextColorProperty, Theme.Text.Light, Theme.Text.Dark);
            e.SetAppThemeColor(Entry.PlaceholderColorProperty, Theme.Subtle.Light, Theme.Subtle.Dark);
        }
        expBox.Completed += (_, _) => Ui.Run(() => Add(Database.Expense));
        incBox.Completed += (_, _) => Ui.Run(() => Add(Database.Income));
        Body = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 8, Padding = new Thickness(16, 12, 16, 32),
                Children =
                {
                    Ui.Section("支出分類"), AddRow(expBox, Database.Expense), expList,
                    Ui.Section("收入分類"), AddRow(incBox, Database.Income), incList,
                    Ui.L("刪除分類不會影響已存在的記錄。圖表的顏色依這裡的順序決定。", 12, Theme.Subtle),
                },
            },
        };
        Reload();
    }

    View AddRow(Entry box, string type)
    {
        var grid = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        grid.Add(Dialogs.InputBox(box), 0);
        grid.Add(Ui.Btn("新增", Ui.Kind.Accent, () => Add(type), 48), 1);
        return grid;
    }

    async Task Add(string type)
    {
        var box = type == Database.Expense ? expBox : incBox;
        var name = (box.Text ?? "").Trim();
        if (name == "") { box.Focus(); return; }
        if (!Db.AddCategory(type, name)) { await Dialogs.Info("提示", "這個分類已經存在"); return; }
        box.Text = "";
        Reload();
        NotifyChanged();
    }

    void Reload()
    {
        foreach (var (type, list) in new[] { (Database.Expense, expList), (Database.Income, incList) })
        {
            list.Children.Clear();
            foreach (var name in Db.Categories(type))
            {
                var grid = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
                grid.Add(new Label { Text = name, FontSize = 15, VerticalOptions = LayoutOptions.Center }.Fg(Theme.Text), 0);
                var del = Ui.Btn("✕", Ui.Kind.Ghost, async () =>
                {
                    if (!await Dialogs.Confirm("刪除分類", $"刪除{type}分類「{name}」?\n已存在的記錄不受影響。", "刪除", danger: true)) return;
                    Db.DeleteCategory(type, name);
                    Reload();
                    NotifyChanged();
                }, 34);
                del.WidthRequest = 40;
                del.Padding = 0;
                grid.Add(del, 1);
                list.Add(Ui.Card(grid, 10));
            }
        }
    }
}
