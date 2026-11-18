using AndroidX.Core.View;
using Microsoft.Maui.Platform;

namespace ZZZ.Mobile;

static partial class SystemBars
{
    public static partial void Update()
    {
        if (Platform.CurrentActivity?.Window is not { } window) return;
        bool dark = Theme.IsDark;
        var bg = Theme.Bg.Now.ToPlatform();
        // 視窗底色原本是啟動畫面的深色;換成頁面底色,頁面淡入淡出時才不會透出深灰
        window.SetBackgroundDrawable(new Android.Graphics.Drawables.ColorDrawable(bg));
#pragma warning disable CA1422 // Android 15 起畫面延伸到狀態列底下,這兩行只對舊版有作用
        window.SetStatusBarColor(bg);
        window.SetNavigationBarColor(Theme.Surface.Now.ToPlatform());
#pragma warning restore CA1422
        if (WindowCompat.GetInsetsController(window, window.DecorView) is not { } bars) return;
        bars.AppearanceLightStatusBars = !dark;
        bars.AppearanceLightNavigationBars = !dark;
    }

    public static partial void UpdateFor(Page page)
    {
        if (page.Handler?.PlatformView is not Android.Views.View view) return;
        if (ViewCompat.GetWindowInsetsController(view) is not { } bars) return;
        bars.AppearanceLightStatusBars = !Theme.IsDark;
        bars.AppearanceLightNavigationBars = !Theme.IsDark;
    }
}
