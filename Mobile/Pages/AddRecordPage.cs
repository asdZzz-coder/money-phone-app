using static ZZZ.Mobile.State;

namespace ZZZ.Mobile;

/// <summary>記一筆:支出/收入、金額、分類(點選)、錢包、日期、備註。「再記一筆」存好後留在這頁繼續輸入。</summary>
public class AddRecordPage : ContentPage
{
    readonly TaskCompletionSource done = new();
    readonly Entry amount = new() { Placeholder = "0", FontSize = 30, FontAttributes = FontAttributes.Bold, Keyboard = Keyboard.Numeric, HorizontalTextAlignment = TextAlignment.End };
    readonly Entry note = new() { Placeholder = "例如:午餐、捷運", FontSize = 15 };
    readonly Entry date = new() { Text = Today, FontSize = 15, Placeholder = "YYYY-MM-DD" };
    readonly Picker wallet = new() { Title = "錢包", FontSize = 15 };
    readonly FlexLayout cats = new() { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
    readonly Label error = Ui.L("", 13, Theme.Exp);
    readonly Label saved = Ui.L("", 13, Theme.Inc);
    readonly List<Wallet> wallets = Db.Wallets();
    string type = Database.Expense;
    string? category;

    public static async Task Open()
    {
        var nav = Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
        if (nav == null) return;
        var page = new AddRecordPage();
        await nav.PushModalAsync(page, false);
        await page.done.Task;
    }

    AddRecordPage()
    {
        this.Page("記一筆");
        foreach (var e in new[] { amount, note, date })
        {
            e.SetAppThemeColor(Entry.TextColorProperty, Theme.Text.Light, Theme.Text.Dark);
            e.SetAppThemeColor(Entry.PlaceholderColorProperty, Theme.Subtle.Light, Theme.Subtle.Dark);
        }
        wallet.SetAppThemeColor(Picker.TextColorProperty, Theme.Text.Light, Theme.Text.Dark);
        wallet.ItemsSource = wallets.Select(w => w.Name).ToList();
        wallet.SelectedIndex = Math.Max(wallets.FindIndex(w => w.Id == SelWallet), 0);

        var close = Ui.Btn("✕", Ui.Kind.Ghost, Close, 40, guard: false);
        close.WidthRequest = 44;
        var head = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        head.Add(Ui.L("記一筆", 22, bold: true), 0);
        head.Add(close, 1);

        var (segment, _) = Ui.Segment([Database.Expense, Database.Income], 0, i =>
        {
            type = i == 0 ? Database.Expense : Database.Income;
            LoadCategories();
        });

        var amountBox = Dialogs.InputBox(amount);
        amountBox.HeightRequest = 68;

        var buttons = new Grid { ColumnSpacing = 10, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) }, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Add(Ui.Btn("再記一筆", Ui.Kind.Outline, () => Save(keepOpen: true), 48, guard: false), 0);
        buttons.Add(Ui.Btn("新增", Ui.Kind.Accent, () => Save(keepOpen: false), 48, guard: false), 1);
        error.IsVisible = saved.IsVisible = false;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 8, Padding = new Thickness(16, 12, 16, 32),
                Children =
                {
                    head, segment, Label("金額"), amountBox, Label("分類"), cats,
                    Label("錢包"), Dialogs.InputBox(wallet), Label("日期"), Dialogs.DateField(date),
                    Label("備註(可空白)"), Dialogs.InputBox(note), error, saved, buttons,
                },
            },
        };
        LoadCategories();
    }

    static Label Label(string text) => new Label { Text = text, FontSize = 13, FontAttributes = FontAttributes.Bold, Margin = new Thickness(2, 8, 0, 0) }.Fg(Theme.Muted);

    void LoadCategories()
    {
        var list = Db.Categories(type);
        if (category == null || !list.Contains(category)) category = list.FirstOrDefault();
        cats.Children.Clear();
        foreach (var c in list)
        {
            bool sel = c == category;
            var chip = new Border
            {
                Padding = new Thickness(14, 8), Margin = new Thickness(0, 0, 8, 8), StrokeThickness = sel ? 0 : 1, StrokeShape = Theme.Round(18),
                Content = new Label { Text = Theme.CuteMode ? $"{Theme.CuteEmoji(c)} {c}" : c, FontSize = 14, FontAttributes = sel ? FontAttributes.Bold : FontAttributes.None }.Fg(sel ? Theme.OnInk : Theme.Text),
            }.Fill(sel ? Theme.Ink : Theme.Surface).Stroke_(Theme.Line);
            chip.OnTap(() => { category = c; LoadCategories(); return Task.CompletedTask; }, guard: false);
            cats.Children.Add(chip);
        }
        if (list.Count == 0) cats.Children.Add(Ui.L("還沒有分類,請到「更多 → 管理分類」新增。", 13, Theme.Muted));
    }

    async Task Save(bool keepOpen)
    {
        saved.IsVisible = false;
        string? err = null;
        if (!TryAmount(amount.Text ?? "", out var amt) || amt <= 0) err = "請輸入大於 0 的金額";
        else if (!DateTime.TryParse(date.Text, out _)) err = "日期格式為 YYYY-MM-DD";
        else if (wallet.SelectedIndex < 0) err = "請選擇錢包";
        else if (category == null) err = "請先在「更多 → 管理分類」建立分類";
        error.Text = err;
        error.IsVisible = err != null;
        if (err != null) return;
        var w = wallets[wallet.SelectedIndex];
        Db.AddRecord(NormalizeDate(date.Text ?? ""), type, category!, amt, (note.Text ?? "").Trim(), w.Id);
        NotifyChanged();
        if (!keepOpen) { await Close(); return; }
        saved.Text = $"已新增:{type} · {category} · {Money(amt)}({w.Name})";
        saved.IsVisible = true;
        amount.Text = "";
        note.Text = "";
        amount.Focus();
    }

    bool closing;

    async Task Close()
    {
        if (closing) return;
        closing = true;
        amount.Unfocus();
        if (Navigation.ModalStack.LastOrDefault() == this) await Navigation.PopModalAsync(false);
        done.TrySetResult();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(150), () => SystemBars.UpdateFor(this));
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () => amount.Focus());
    }

    protected override bool OnBackButtonPressed()
    {
        Ui.Do(Close);
        return true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (closing) done.TrySetResult();
    }
}
