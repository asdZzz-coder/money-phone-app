namespace ZZZ.Mobile;

/// <param name="IsDate">日期欄:可點日曆選,也可直接輸入 YYYY-MM-DD;值為輸入框裡的文字</param>
/// <param name="Options">有值時改用下拉選單,值為選到的那一項(Initial 為預設選項)</param>
/// <param name="Numeric">金額、數字欄:顯示數字鍵盤</param>
public record Field(string Label, string Initial = "", string Hint = "", bool IsDate = false, string[]? Options = null,
                    bool Numeric = false);

/// <summary>
/// 統一風格的訊息/確認/輸入對話框(對應電腦版的 DialogWindow)。
/// 對話框是蓋在目前畫面上的半透明頁面,按返回鍵等於取消。
/// </summary>
public static class Dialogs
{
    enum Kind { Info, Success, Error, Question, Danger, Input }

    public static Task Info(string title, string message) => Alert(Kind.Info, title, message);
    public static Task Success(string title, string message) => Alert(Kind.Success, title, message);
    public static Task Error(string title, string message) => Alert(Kind.Error, title, message);

    static async Task Alert(Kind kind, string title, string message)
    {
        var d = new DialogPage(kind == Kind.Info ? "i" : kind == Kind.Success ? "✓" : "!",
                               kind == Kind.Success ? (Theme.Inc, Theme.IncSoft) : kind == Kind.Error ? (Theme.Exp, Theme.ExpSoft) : (Theme.Ink, Theme.Ghost),
                               title, message);
        d.SetButtons(null, "知道了", false);
        await d.Show();
    }

    public static async Task<bool> Confirm(string title, string message, string okText = "確定", bool danger = false)
    {
        var d = new DialogPage(danger ? "!" : "?", danger ? (Theme.Exp, Theme.ExpSoft) : (Theme.Ink, Theme.Ghost), title, message);
        d.SetButtons("取消", okText, danger);
        return await d.Show() != null;
    }

    /// <param name="validate">回傳錯誤訊息則留在對話框;回傳 null 表示通過。</param>
    /// <param name="preview">每次輸入改變時依目前的值算出一段說明,顯示在欄位下方(例如分期每期要繳多少)</param>
    public static async Task<string[]?> Prompt(string title, string message, Field[] fields,
                                               Func<string[], string?>? validate = null, string okText = "確定", bool danger = false,
                                               Func<string[], string>? preview = null)
    {
        var d = new DialogPage(danger ? "!" : "✎", danger ? (Theme.Exp, Theme.ExpSoft) : (Theme.Ink, Theme.Ghost), title, message);
        d.AddFields(fields, preview);
        d.Validate = validate;
        d.SetButtons("取消", okText, danger);
        return await d.Show();
    }

    /// <summary>顯示數個大型選項按鈕,回傳所選索引;取消回傳 null。</summary>
    public static async Task<int?> Choose(string title, string message, (string Icon, string Title, string Description)[] options)
    {
        var d = new DialogPage("?", (Theme.Ink, Theme.Ghost), title, message);
        d.AddChoices(options);
        d.SetButtons("取消", null, false);
        var r = await d.Show();
        return r != null ? int.Parse(r[0]) : null;
    }

    /// <summary>精簡的動作選單(清單列的「編輯 / 刪除」等),回傳所選文字;取消回傳 null。</summary>
    public static async Task<string?> Menu(string title, params string[] actions)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null) return null;
        var r = await page.DisplayActionSheetAsync(title, "取消", null, actions);
        return actions.Contains(r) ? r : null;
    }

    /// <summary>有框的輸入欄(記一筆的頁面也用)。</summary>
    public static Border InputBox(View input) => DialogPage.Box(input);

    /// <summary>日期輸入欄:可直接打字,也可點右邊的日曆選。</summary>
    public static View DateField(Entry entry) => DialogPage.DateBox(entry);

    sealed class DialogPage : ContentPage
    {
        readonly TaskCompletionSource<string[]?> done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly VerticalStackLayout body = new() { Spacing = 0 };
        readonly Label error = Ui.L("", 13, Theme.Exp);
        readonly List<Func<string>> values = [];
        readonly HorizontalStackLayout buttons = new() { Spacing = 10, HorizontalOptions = LayoutOptions.End, Margin = new Thickness(0, 18, 0, 0) };
        View? firstInput;
        public Func<string[], string?>? Validate;

        public DialogPage(string icon, (Pair Fg, Pair Bg) color, string title, string message)
        {
            BackgroundColor = Color.FromArgb("#73000000");
            Shell.SetPresentationMode(this, PresentationMode.ModalNotAnimated);
            // 可愛主題:對話框的小圖示換成 emoji
            if (Theme.CuteMode)
                icon = icon switch { "i" => "🐰", "✓" => "🎉", "!" => "🥺", "?" => "🤔", "✎" => "✏️", _ => icon };
            var iconBox = new Border
            {
                WidthRequest = 36, HeightRequest = 36, StrokeThickness = 0, StrokeShape = Theme.Round(10),
                Content = new Label { Text = icon, FontSize = 17, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }.Fg(color.Fg),
            }.Fill(color.Bg);
            var head = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
            head.Add(iconBox, 0);
            head.Add(new Label { Text = title, FontSize = 18, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }.Fg(Theme.Text), 1);
            body.Add(head);
            if (message != "") body.Add(new Label { Text = message, FontSize = 14, LineHeight = 1.3, Margin = new Thickness(0, 12, 0, 0) }.Fg(Theme.Muted));
            error.IsVisible = false;
            error.Margin = new Thickness(0, 12, 0, 0);

            var card = new Border
            {
                StrokeThickness = 0, StrokeShape = Theme.Round(18), Padding = new Thickness(20, 20, 20, 16),
                Content = new VerticalStackLayout { Children = { body, error, buttons } },
                MaximumWidthRequest = 520, Margin = new Thickness(16, 24),
                VerticalOptions = LayoutOptions.Center,
            }.Fill(Theme.Surface);
            Content = new ScrollView { Content = new Grid { Children = { card }, MinimumHeightRequest = 200 }, VerticalOptions = LayoutOptions.Center };
        }

        public void SetButtons(string? cancel, string? ok, bool danger)
        {
            if (cancel != null) buttons.Add(Ui.Btn(cancel, Ui.Kind.Ghost, () => Close(null), guard: false));
            if (ok != null) buttons.Add(Ui.Btn(ok, danger ? Ui.Kind.DangerSolid : Ui.Kind.Accent, OnOk, guard: false));
        }

        Task OnOk()
        {
            var vals = values.Select(v => v()).ToArray();
            var err = Validate?.Invoke(vals);
            if (err != null)
            {
                error.Text = err;
                error.IsVisible = true;
                return Task.CompletedTask;
            }
            return Close(vals);
        }

        public void AddFields(Field[] fields, Func<string[], string>? preview)
        {
            Action? changed = null;
            foreach (var f in fields)
            {
                body.Add(new Label { Text = f.Label, FontSize = 13, FontAttributes = FontAttributes.Bold, Margin = new Thickness(2, 14, 0, 6) }.Fg(Theme.Muted));
                if (f.Options != null)
                {
                    var picker = new Picker { Title = f.Label, ItemsSource = f.Options, FontSize = 15 };
                    picker.SetAppThemeColor(Picker.TextColorProperty, Theme.Text.Light, Theme.Text.Dark);
                    picker.SelectedIndex = Math.Max(Array.IndexOf(f.Options, f.Initial), 0);
                    values.Add(() => picker.SelectedItem as string ?? "");
                    picker.SelectedIndexChanged += (_, _) => changed?.Invoke();
                    body.Add(Box(picker));
                    continue;
                }
                var entry = new Entry
                {
                    Text = f.Initial, Placeholder = f.Hint, FontSize = 15, ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
                    Keyboard = f.Numeric ? Keyboard.Numeric : Keyboard.Default,
                };
                entry.SetAppThemeColor(Entry.TextColorProperty, Theme.Text.Light, Theme.Text.Dark);
                entry.SetAppThemeColor(Entry.PlaceholderColorProperty, Theme.Subtle.Light, Theme.Subtle.Dark);
                values.Add(() => (entry.Text ?? "").Trim());
                if (!f.IsDate && firstInput == null && f.Initial == "") firstInput = entry;
                entry.TextChanged += (_, _) => changed?.Invoke();
                entry.Completed += (_, _) => Ui.Do(OnOk);
                body.Add(f.IsDate ? DateBox(entry) : Box(entry));
            }
            if (preview != null)
            {
                var text = Ui.L("", 13, Theme.Ink);
                var box = new Border { Content = text, StrokeThickness = 0, StrokeShape = Theme.Round(8), Padding = new Thickness(12, 9), Margin = new Thickness(0, 16, 0, 0) }.Fill(Theme.Ghost);
                body.Add(box);
                changed = () =>
                {
                    text.Text = preview(values.Select(v => v()).ToArray());
                    box.IsVisible = text.Text != "";
                };
                changed();
            }
        }

        public static Border Box(View input) =>
            new Border { Content = input, StrokeThickness = 1, StrokeShape = Theme.Round(10), Padding = new Thickness(10, 0), HeightRequest = 48 }
                .Fill(Theme.Surface).Stroke_(Theme.LineStrong);

        /// <summary>
        /// 日期欄:一般輸入框加上日曆按鈕。打什麼就保留什麼,交給 Validate 檢查;
        /// 日曆按鈕上面疊一個看不見的 DatePicker,點它會開系統的日曆,選好後填入 yyyy-MM-dd。
        /// </summary>
        public static View DateBox(Entry entry)
        {
            var picker = new DatePicker { Opacity = 0.011, WidthRequest = 48, HorizontalOptions = LayoutOptions.End, Format = "yyyy-MM-dd" };
            bool syncing = false;
            void Sync()
            {
                syncing = true;
                picker.Date = DateTime.TryParse(entry.Text, out var d) ? d.Date : DateTime.Today;
                syncing = false;
            }
            Sync();
            entry.TextChanged += (_, _) => Sync();
            picker.DateSelected += (_, _) =>
            {
                if (syncing || picker.Date is not DateTime d) return;
                entry.Text = d.ToString("yyyy-MM-dd");
            };
            var icon = new Label { Text = "📅", FontSize = 18, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(0, 0, 12, 0), InputTransparent = true };
            entry.Margin = new Thickness(0, 0, 44, 0);
            var grid = new Grid { Children = { entry, icon, picker } };
            return Box(grid);
        }

        public void AddChoices((string Icon, string Title, string Description)[] options)
        {
            for (int i = 0; i < options.Length; i++)
            {
                var (icon, head, desc) = options[i];
                var text = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center, Children = { Ui.L(head, 15, bold: true) } };
                if (desc != "") text.Add(Ui.L(desc, 12, Theme.Muted));
                var grid = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
                if (icon != "")
                    grid.Add(new Border
                    {
                        WidthRequest = 36, HeightRequest = 36, StrokeThickness = 0, StrokeShape = Theme.Round(9), VerticalOptions = LayoutOptions.Center,
                        Content = new Label { Text = icon, FontSize = 16, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }.Fg(Theme.Ink),
                    }.Fill(Theme.Ghost), 0);
                grid.Add(text, 1);
                var index = i.ToString();
                var card = new Border
                {
                    Content = grid, Padding = new Thickness(14, 12), Margin = new Thickness(0, 12, 0, 0),
                    StrokeThickness = 1, StrokeShape = Theme.Round(12),
                }.Fill(Theme.Surface).Stroke_(Theme.Line).OnTap(() => Close([index]), guard: false);
                body.Add(card);
            }
        }

        public async Task<string[]?> Show()
        {
            var nav = Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
            if (nav == null) return null;
            await nav.PushModalAsync(this, false);
            if (firstInput != null)
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), () => firstInput.Focus());
            return await done.Task;
        }

        bool closing;

        async Task Close(string[]? result)
        {
            if (closing) return;
            closing = true;
            if (Navigation.ModalStack.LastOrDefault() == this) await Navigation.PopModalAsync(false);
            done.TrySetResult(result); // 完全關掉後才回傳,呼叫端接著開下一個對話框才不會撞在一起
        }

        protected override bool OnBackButtonPressed()
        {
            Ui.Do(() => Close(null));
            return true;
        }

        /// <summary>不是按按鈕關掉的(例如被系統關閉)才當作取消;按按鈕關的結果由 Close 設定。</summary>
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (!closing) done.TrySetResult(null);
        }
    }
}
