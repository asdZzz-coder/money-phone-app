using Microsoft.Maui.Controls.Shapes;

namespace ZZZ.Mobile;

/// <summary>建立介面元件的小工具。版面全部用程式碼組成(沒有 XAML),配色見 <see cref="Theme"/>。</summary>
public static class Ui
{
    public enum Kind { Accent, Ghost, Outline, Danger, DangerSolid, OnHero }

    public static Label L(string text = "", double size = 14, Pair? color = null, bool bold = false) =>
        new Label
        {
            Text = text, FontSize = size, LineBreakMode = LineBreakMode.WordWrap,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
        }.Fg(color ?? Theme.Text);

    /// <summary>單行、超出以 … 截斷的文字。</summary>
    public static Label L1(string text = "", double size = 14, Pair? color = null, bool bold = false)
    {
        var l = L(text, size, color, bold);
        l.LineBreakMode = LineBreakMode.TailTruncation;
        l.MaxLines = 1;
        return l;
    }

    public static Border Card(View content, double pad = 14) =>
        new Border { Content = content, Padding = pad, StrokeShape = Theme.Round(14), StrokeThickness = 1 }
            .Fill(Theme.Surface).Stroke_(Theme.Line);

    /// <param name="deco">可愛主題時在右上角放裝飾(收支記錄的大卡片自己處理,傳 false)</param>
    public static Border HeroCard(View content, bool deco = true)
    {
        View inner = content;
        if (deco && Theme.CuteMode)
            inner = new Grid { Children = { content, new Label { Text = "✨🎀", FontSize = 24, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start } } };
        return new Border { Content = inner, Padding = new Thickness(18, 16), StrokeShape = Theme.Round(18), StrokeThickness = 0 }.Hero();
    }

    public static Border Pill(string text, Pair fg, Pair bg) =>
        new Border
        {
            Content = L1(text, 11, fg, bold: true), Padding = new Thickness(8, 2), StrokeThickness = 0,
            StrokeShape = Theme.Round(10), VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Start,
        }.Fill(bg);

    /// <param name="guard">false:對話框裡的按鈕(外層操作還在等對話框結果,不能被「同時只做一件事」擋掉)</param>
    public static Button Btn(string text, Kind kind = Kind.Ghost, Func<Task>? click = null, double height = 42, bool guard = true)
    {
        var b = new Button
        {
            Text = text, FontSize = 14, CornerRadius = (int)Theme.R(10), HeightRequest = height, Padding = new Thickness(14, 0),
            FontAttributes = kind is Kind.Accent or Kind.DangerSolid ? FontAttributes.Bold : FontAttributes.None,
            BorderWidth = kind == Kind.Outline ? 1 : 0,
        };
        (Pair bg, Pair fg) = kind switch
        {
            Kind.Accent => (Theme.Ink, Theme.OnInk),
            Kind.Outline => (Theme.Surface, Theme.Text),
            Kind.Danger => (Theme.ExpSoft, Theme.Exp),
            Kind.DangerSolid => (Theme.Exp, Theme.White),
            Kind.OnHero => (Theme.HeroBtn, Theme.OnHero),
            _ => (Theme.Ghost, Theme.Text),
        };
        b.SetAppThemeColor(Button.BackgroundColorProperty, bg.Light, bg.Dark);
        b.SetAppThemeColor(Button.TextColorProperty, fg.Light, fg.Dark);
        b.SetAppThemeColor(Button.BorderColorProperty, Theme.Line.Light, Theme.Line.Dark);
        if (click != null) b.Clicked += (_, _) => { if (guard) Run(click); else Do(click); };
        return b;
    }

    /// <summary>右下角的圓形「+」按鈕。</summary>
    public static Button Fab(Func<Task> click)
    {
        var b = Btn("+", Kind.Accent, click, 58);
        b.WidthRequest = 58;
        b.CornerRadius = 29;
        b.FontSize = 28;
        b.Padding = new Thickness(0, 0, 0, 4);
        b.HorizontalOptions = LayoutOptions.End;
        b.VerticalOptions = LayoutOptions.End;
        b.Margin = new Thickness(0, 0, 18, 18);
        // 不加 Shadow:Android 上按下後陰影圖層會畫成整頁大小,讓畫面變灰
        return b;
    }

    /// <summary>只能同時進行一個操作(對話框還開著時,其他按鈕不會再開第二個);發生錯誤時顯示訊息。</summary>
    static bool busy;

    public static async void Run(Func<Task> action)
    {
        if (busy) return;
        busy = true;
        try { await action(); }
        catch (Exception ex) { await Dialogs.Error("發生錯誤", ex.Message); }
        finally { busy = false; }
    }

    /// <summary>不受 busy 限制的版本,給對話框內部使用。</summary>
    public static async void Do(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { await Dialogs.Error("發生錯誤", ex.Message); }
    }

    public static T OnTap<T>(this T view, Func<Task> action, bool guard = true) where T : View
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => { if (guard) Run(action); else Do(action); };
        view.GestureRecognizers.Add(tap);
        return view;
    }

    /// <summary>分段選擇(例如 支出/收入、未還清/已還清/全部)。回傳元件與設定目前選項的方法。</summary>
    public static (View View, Action<int> Select) Segment(string[] options, int selected, Action<int> changed)
    {
        var grid = new Grid { ColumnSpacing = 4, Padding = 4 };
        var buttons = new List<Button>();
        void Paint(int sel)
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                // 可愛主題:選到的那一格用粉紅底白字
                var (bg, fg) = i == sel ? (Theme.CuteMode ? (Theme.Ink, Theme.OnInk) : (Theme.Surface, Theme.Text)) : (Theme.Ghost, Theme.Muted);
                b.SetAppThemeColor(Button.BackgroundColorProperty, bg.Light, bg.Dark);
                b.SetAppThemeColor(Button.TextColorProperty, fg.Light, fg.Dark);
                b.FontAttributes = i == sel ? FontAttributes.Bold : FontAttributes.None;
            }
        }
        for (int i = 0; i < options.Length; i++)
        {
            int k = i;
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var b = new Button { Text = options[i], FontSize = 14, CornerRadius = (int)Theme.R(8), HeightRequest = 36, Padding = 0 };
            b.Clicked += (_, _) => { Paint(k); changed(k); };
            buttons.Add(b);
            grid.Add(b, i);
        }
        Paint(selected);
        var box = new Border { Content = grid, StrokeThickness = 0, StrokeShape = Theme.Round(11) }.Fill(Theme.Ghost);
        return (box, Paint);
    }

    /// <summary>進度條(0–1)。</summary>
    public static View Bar(double value, Pair color, double height = 6)
    {
        value = Math.Clamp(value, 0, 1);
        var grid = new Grid { HeightRequest = height, ColumnSpacing = 0 };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Math.Max(value, 0.0001), GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Math.Max(1 - value, 0.0001), GridUnitType.Star)));
        var fill = new BoxView { CornerRadius = height / 2, IsVisible = value > 0 };
        fill.SetAppThemeColor(BoxView.ColorProperty, color.Light, color.Dark);
        grid.Add(fill, 0);
        return new Border { Content = grid, StrokeThickness = 0, StrokeShape = Theme.Round(height / 2), HeightRequest = height }.Fill(Theme.Ghost);
    }

    /// <summary>統計小卡:標題、大數字、說明。</summary>
    public static (Border Card, Label Value, Label Sub) Tile(string title, Pair? valueColor = null)
    {
        var value = L1("", 20, valueColor ?? Theme.Text, bold: true);
        var sub = L("", 12, Theme.Muted);
        var card = Card(new VerticalStackLayout { Spacing = 2, Children = { L1(title, 12, Theme.Muted), value, sub } }, 12);
        return (card, value, sub);
    }

    /// <summary>小節標題;可愛主題前面加愛心、用粉紅色。</summary>
    public static Label Section(string text) => new Label
    {
        Text = Theme.CuteMode ? $"♡ {text}" : text, FontSize = 13, FontAttributes = FontAttributes.Bold, Margin = new Thickness(4, 12, 0, 2),
    }.Fg(Theme.CuteMode ? Theme.Ink : Theme.Muted);

    /// <summary>頁面底色。</summary>
    public static T Page<T>(this T page, string title) where T : ContentPage
    {
        page.Title = title;
        page.SetAppThemeColor(VisualElement.BackgroundColorProperty, Theme.Bg.Light, Theme.Bg.Dark);
        return page;
    }

    public static BoxView Divider()
    {
        var b = new BoxView { HeightRequest = 1, Margin = new Thickness(0, 4) };
        b.SetAppThemeColor(BoxView.ColorProperty, Theme.Line.Light, Theme.Line.Dark);
        return b;
    }
}
