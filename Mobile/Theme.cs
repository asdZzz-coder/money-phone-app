using Microsoft.Maui.Controls.Shapes;

namespace ZZZ.Mobile;

/// <summary>
/// 配色與電腦版 Colors.Light.xaml / Colors.Dark.xaml 相同。每個顏色是一組(淺色, 深色),
/// 介面用 SetAppThemeColor 綁定,切換深色/淺色時自動換色。
/// 顏色只用來表示意義:綠 = 收入/達成,紅 = 支出/危險,橘 = 逾期;其餘是墨色與灰階。
/// </summary>
public record Pair(Color Light, Color Dark)
{
    public Color Now => Theme.IsDark ? Dark : Light;
}

public static class Theme
{
    static Pair P(string light, string dark) => new(Color.FromArgb(light), Color.FromArgb(dark));

    public static readonly Pair Bg = P("#F4F5F7", "#111317");
    public static readonly Pair Surface = P("#FFFFFF", "#1B1E24");
    public static readonly Pair Ink = P("#1F2937", "#E5E7EB");
    public static readonly Pair OnInk = P("#FFFFFF", "#111317");
    public static readonly Pair Text = P("#1F2937", "#E5E7EB");
    public static readonly Pair Muted = P("#6B7280", "#9CA3AF");
    public static readonly Pair Subtle = P("#9CA3AF", "#6E7681");
    public static readonly Pair Line = P("#E5E7EB", "#2A2F37");
    public static readonly Pair LineStrong = P("#CDD1D7", "#3D434D");
    public static readonly Pair Sel = P("#EDF1F7", "#273040");
    public static readonly Pair Ghost = P("#EEF0F3", "#262B33");
    public static readonly Pair Inc = P("#0E9F6E", "#34C38F");
    public static readonly Pair IncSoft = P("#E6F6EF", "#14322A");
    public static readonly Pair Exp = P("#E5484D", "#F26B70");
    public static readonly Pair ExpSoft = P("#FDECEC", "#3A1F23");
    public static readonly Pair Warn = P("#EA580C", "#F59E5B");
    public static readonly Pair WarnSoft = P("#FFF1E6", "#3A2819");
    /// <summary>大卡片(HeroBrush)永遠是深色,上面的字固定用白色系。</summary>
    public static readonly Pair OnHero = P("#FFFFFF", "#FFFFFF");
    public static readonly Pair OnHeroMuted = P("#B6BDC8", "#B6BDC8");
    public static readonly Pair HeroWarn = P("#FDBA74", "#FDBA74");
    public static readonly Pair HeroExp = P("#FCA5A5", "#FCA5A5");

    // ---------- 錢包的可愛風(粉嫩色系);一般風格用上面的墨色與灰階 ----------
    public static readonly Pair CuteSoft = P("#FFF0F5", "#33222B");
    public static readonly Pair CuteLine = P("#F9C6D8", "#5A3445");
    public static readonly Pair CuteAccent = P("#D9467A", "#F49AC1");
    public static readonly Pair OnCuteHero = P("#5A2A3C", "#FFFFFF");
    public static readonly Pair OnCuteHeroMuted = P("#8A5A6A", "#F3D4E0");

    static Brush Gradient(string a, string b) => new LinearGradientBrush(
        [new GradientStop(Color.FromArgb(a), 0), new GradientStop(Color.FromArgb(b), 1)], new Point(0, 0), new Point(1, 1));

    static readonly Brush HeroLight = Gradient("#2B3545", "#111827"), HeroDark = Gradient("#2E3746", "#1E2430");
    static readonly Brush CuteHeroLight = Gradient("#FFB3CC", "#FFD8B8"), CuteHeroDark = Gradient("#6E3550", "#6B4636");

    /// <summary>可愛風錢包的圖示:一般錢包小兔、存錢用小豬、信用卡蝴蝶結。</summary>
    public static string CuteIcon(Wallet w) => w.IsCard ? "🎀" : w.IsSavings ? "🐷" : "🐰";

    public static Border CuteHero(this Border b)
    {
        b.SetAppTheme<Brush>(Border.BackgroundProperty, CuteHeroLight, CuteHeroDark);
        return b;
    }

    public static bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

    // ---------- 主題偏好:跟隨系統 / 淺色 / 深色(存在資料夾的 theme.txt) ----------
    public const string FollowSystem = "system", LightMode = "light", DarkMode = "dark";

    static string ThemeFile => System.IO.Path.Combine(Database.DataDir, "theme.txt");

    public static string Load()
    {
        try { return File.Exists(ThemeFile) ? File.ReadAllText(ThemeFile).Trim() : FollowSystem; }
        catch (IOException) { return FollowSystem; }
        catch (UnauthorizedAccessException) { return FollowSystem; }
    }

    public static void Apply(string mode, bool save = true)
    {
        if (Application.Current is { } app)
            app.UserAppTheme = mode == DarkMode ? AppTheme.Dark : mode == LightMode ? AppTheme.Light : AppTheme.Unspecified;
        if (!save) return;
        try
        {
            Directory.CreateDirectory(Database.DataDir);
            File.WriteAllText(ThemeFile, mode);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

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
        b.SetAppTheme<Brush>(Border.BackgroundProperty, HeroLight, HeroDark);
        return b;
    }

    public static RoundRectangle Round(double r) => new() { CornerRadius = new CornerRadius(r) };
}
