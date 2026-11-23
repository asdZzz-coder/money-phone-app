using Microsoft.Maui.Controls.Shapes;

namespace ZZZ.Mobile;

/// <summary>
/// 配色與電腦版 Colors.Light.xaml / Colors.Dark.xaml 相同。每個顏色是一組(淺色, 深色),
/// 介面用 SetAppThemeColor 綁定,切換深色/淺色時自動換色。
/// 「可愛」主題是淺色的粉嫩版本:Light 會改成 Cute 的顏色(沒有另外指定就沿用淺色)。
/// 顏色只用來表示意義:綠 = 收入/達成,紅 = 支出/危險,橘 = 逾期;其餘是墨色與灰階(可愛主題是粉色系)。
/// </summary>
public sealed class Pair(Color light, Color dark, Color? cute = null)
{
    public Color Light => Theme.CuteMode && cute is { } c ? c : light;
    public Color Dark => dark;
    public Color Now => Theme.IsDark ? Dark : Light;
}

public static class Theme
{
    static Pair P(string light, string dark, string? cute = null) =>
        new(Color.FromArgb(light), Color.FromArgb(dark), cute == null ? null : Color.FromArgb(cute));

    //                                      淺色        深色        可愛
    public static readonly Pair Bg = P("#F4F5F7", "#111317", "#FFF5F8");
    public static readonly Pair Surface = P("#FFFFFF", "#1B1E24");
    public static readonly Pair Ink = P("#1F2937", "#E5E7EB", "#D9578E");
    public static readonly Pair OnInk = P("#FFFFFF", "#111317");
    public static readonly Pair Text = P("#1F2937", "#E5E7EB", "#5B3446");
    public static readonly Pair Muted = P("#6B7280", "#9CA3AF", "#9A6F82");
    public static readonly Pair Subtle = P("#9CA3AF", "#6E7681", "#C9A3B4");
    public static readonly Pair Line = P("#E5E7EB", "#2A2F37", "#F8DCE7");
    public static readonly Pair LineStrong = P("#CDD1D7", "#3D434D", "#F0BCD0");
    public static readonly Pair Sel = P("#EDF1F7", "#273040", "#FFE6EF");
    public static readonly Pair Ghost = P("#EEF0F3", "#262B33", "#FCEBF2");
    public static readonly Pair Inc = P("#0E9F6E", "#34C38F", "#2E9E78");
    public static readonly Pair IncSoft = P("#E6F6EF", "#14322A", "#E3F5EC");
    public static readonly Pair Exp = P("#E5484D", "#F26B70", "#E0445A");
    public static readonly Pair ExpSoft = P("#FDECEC", "#3A1F23", "#FDE8EC");
    public static readonly Pair Warn = P("#EA580C", "#F59E5B", "#E2701F");
    public static readonly Pair WarnSoft = P("#FFF1E6", "#3A2819", "#FFF0E3");
    /// <summary>純白(紅底按鈕的字);不跟可愛主題換色。</summary>
    public static readonly Pair White = P("#FFFFFF", "#FFFFFF");
    /// <summary>大卡片(HeroBrush)一般是深色,上面的字用白色系;可愛主題的大卡片是粉色漸層,字改成深莓色。</summary>
    public static readonly Pair OnHero = P("#FFFFFF", "#FFFFFF", "#5B3446");
    public static readonly Pair OnHeroMuted = P("#B6BDC8", "#B6BDC8", "#8F6276");
    public static readonly Pair HeroWarn = P("#FDBA74", "#FDBA74", "#C2410C");
    public static readonly Pair HeroExp = P("#FCA5A5", "#FCA5A5", "#C81E4A");
    /// <summary>大卡片上的半透明按鈕底色。</summary>
    public static readonly Pair HeroBtn = P("#33FFFFFF", "#33FFFFFF", "#B3FFFFFF");

    // ---------- 錢包的可愛風(粉嫩色系);一般風格用上面的墨色與灰階 ----------
    public static readonly Pair CuteSoft = P("#FFF0F5", "#33222B", "#FFEAF2");
    public static readonly Pair CuteLine = P("#F9C6D8", "#5A3445");
    public static readonly Pair CuteAccent = P("#D9467A", "#F49AC1");
    public static readonly Pair OnCuteHero = P("#5A2A3C", "#FFFFFF");
    public static readonly Pair OnCuteHeroMuted = P("#8A5A6A", "#F3D4E0");

    static Brush Gradient(string a, string b) => new LinearGradientBrush(
        [new GradientStop(Color.FromArgb(a), 0), new GradientStop(Color.FromArgb(b), 1)], new Point(0, 0), new Point(1, 1));

    static readonly Brush HeroLight = Gradient("#2B3545", "#111827"), HeroDark = Gradient("#2E3746", "#1E2430");
    static readonly Brush CuteHeroLight = Gradient("#FFB3CC", "#FFD8B8"), CuteHeroDark = Gradient("#6E3550", "#6B4636");
    /// <summary>可愛主題的大卡片:粉紅到淡紫。</summary>
    static readonly Brush HeroCute = Gradient("#FFC2D9", "#E3D3FF");

    /// <summary>可愛風錢包的圖示:一般錢包小兔、存錢用小豬、信用卡蝴蝶結。</summary>
    public static string CuteIcon(Wallet w) => w.IsCard ? "🎀" : w.IsSavings ? "🐷" : "🐰";

    public static Border CuteHero(this Border b)
    {
        b.SetAppTheme<Brush>(Border.BackgroundProperty, CuteHeroLight, CuteHeroDark);
        return b;
    }

    public static bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

    // ---------- 主題偏好:跟隨系統 / 淺色 / 深色 / 可愛(存在資料夾的 theme.txt) ----------
    public const string FollowSystem = "system", LightMode = "light", DarkMode = "dark", CuteTheme = "cute";

    /// <summary>目前是可愛主題(粉嫩的淺色)。綁定顏色時就決定了,所以切換時要重建畫面(<see cref="App.Rebuild"/>)。</summary>
    public static bool CuteMode { get; private set; }

    static string ThemeFile => System.IO.Path.Combine(Database.DataDir, "theme.txt");

    public static string Load()
    {
        try { return File.Exists(ThemeFile) ? File.ReadAllText(ThemeFile).Trim() : FollowSystem; }
        catch (IOException) { return FollowSystem; }
        catch (UnauthorizedAccessException) { return FollowSystem; }
    }

    /// <summary>套用主題;回傳是否切換了可愛主題(是的話畫面要重建)。</summary>
    public static bool Apply(string mode, bool save = true)
    {
        bool cute = mode == CuteTheme, changed = cute != CuteMode;
        CuteMode = cute;
        if (Application.Current is { } app)
            app.UserAppTheme = mode == DarkMode ? AppTheme.Dark : mode is LightMode or CuteTheme ? AppTheme.Light : AppTheme.Unspecified;
        if (save)
        {
            try
            {
                Directory.CreateDirectory(Database.DataDir);
                File.WriteAllText(ThemeFile, mode);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return changed;
    }

    /// <summary>可愛主題的角更圓。</summary>
    public static double R(double r) => CuteMode ? Math.Round(r * 1.4) : r;

    // ---------- 綁定顏色的小工具 ----------
    public static T Fg<T>(this T label, Pair c) where T : Label
    {
        label.SetAppThemeColor(Label.TextColorProperty, c.Light, c.Dark);
        return label;
    }

    public static T Bg_<T>(this T view, Pair c) where T : VisualElement
    {
        view.SetAppThemeColor(VisualElement.BackgroundColorProperty, c.Light, c.Dark);
        return view;
    }

    public static Border Fill(this Border b, Pair c)
    {
        b.SetAppThemeColor(Border.BackgroundColorProperty, c.Light, c.Dark);
        return b;
    }

    public static Border Stroke_(this Border b, Pair c)
    {
        b.SetAppThemeColor(Border.StrokeProperty, c.Light, c.Dark);
        return b;
    }

    public static Border Hero(this Border b)
    {
        b.SetAppTheme<Brush>(Border.BackgroundProperty, CuteMode ? HeroCute : HeroLight, HeroDark);
        return b;
    }

    public static RoundRectangle Round(double r) => new() { CornerRadius = new CornerRadius(R(r)) };
}
