namespace ZZZ.Mobile;

public class App : Application
{
    public App()
    {
        Theme.Apply(Theme.Load(), save: false);
        RequestedThemeChanged += (_, _) => SystemBars.Update();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell()) { Title = "簡易記帳" };
        window.Created += (_, _) =>
        {
            SystemBars.Update();
            // 啟動後安靜地檢查一次更新,有新版才提示
            Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(3), () => Ui.Run(() => Ops.CheckUpdate(silent: true)));
        };
        return window;
    }

    /// <summary>
    /// 切換可愛主題後重建整個畫面:顏色是在建立元件時綁定的,換主題要重新建立才會套用。
    /// <paramref name="tab"/> 是重建後要停在哪個分頁(0 = 記帳 … 4 = 更多)。
    /// </summary>
    public static void Rebuild(int tab)
    {
        if (Current?.Windows.FirstOrDefault() is not { } window) return;
        window.Page = new AppShell(tab);
        SystemBars.Update();
    }
}

/// <summary>底部五個分頁。</summary>
public class AppShell : Shell
{
    public AppShell(int tab = 0)
    {
        this.SetAppThemeColor(BackgroundColorProperty, Theme.Bg.Light, Theme.Bg.Dark);
        this.SetAppThemeColor(ForegroundColorProperty, Theme.Text.Light, Theme.Text.Dark);
        this.SetAppThemeColor(TitleColorProperty, Theme.Text.Light, Theme.Text.Dark);
        this.SetAppThemeColor(TabBarBackgroundColorProperty, Theme.Surface.Light, Theme.Surface.Dark);
        this.SetAppThemeColor(TabBarForegroundColorProperty, Theme.Ink.Light, Theme.Ink.Dark);
        this.SetAppThemeColor(TabBarTitleColorProperty, Theme.Ink.Light, Theme.Ink.Dark);
        this.SetAppThemeColor(TabBarUnselectedColorProperty, Theme.Subtle.Light, Theme.Subtle.Dark);

        var bar = new TabBar();
        bar.Items.Add(Tab("記帳", "tab_records.png", "records", () => new RecordsPage()));
        bar.Items.Add(Tab("欠款", "tab_debts.png", "debts", () => new DebtsPage()));
        bar.Items.Add(Tab("目標", "tab_goals.png", "goals", () => new GoalsPage()));
        bar.Items.Add(Tab("圖表", "tab_stats.png", "stats", () => new StatsPage()));
        bar.Items.Add(Tab("更多", "tab_more.png", "more", () => new MorePage()));
        bar.CurrentItem = bar.Items[tab];
        Items.Add(bar);
    }

    static ShellContent Tab(string title, string icon, string route, Func<Page> create) =>
        new() { Title = title, Icon = icon, Route = route, ContentTemplate = new DataTemplate(create) };
}

/// <summary>狀態列、導覽列的圖示顏色跟著深色/淺色切換。</summary>
static partial class SystemBars
{
    public static partial void Update();

    /// <summary>全螢幕的「記一筆」頁面是另一個視窗,狀態列圖示的顏色要另外設定。</summary>
    public static partial void UpdateFor(Page page);
}
