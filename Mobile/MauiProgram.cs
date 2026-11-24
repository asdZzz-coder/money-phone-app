using System.Globalization;

namespace ZZZ.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // 資料放在 App 自己的資料夾(解除安裝才會刪掉);一定要在第一次用到 Database 之前設定
        Environment.SetEnvironmentVariable("LEDGER_DATA_DIR", Path.Combine(FileSystem.AppDataDirectory, "Ledger"));

        // 日期一律顯示為 yyyy-MM-dd(與資料庫格式一致),數字用千分位
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("zh-TW").Clone();
        culture.DateTimeFormat.ShortDatePattern = "yyyy-MM-dd";
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = culture;

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
#if ANDROID
        // 輸入框外面已經有圓角框,拿掉 Android 預設的底線
        static void NoUnderline(Android.Views.View v) =>
            v.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (h, _) => NoUnderline(h.PlatformView));
        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("NoUnderline", (h, _) => NoUnderline(h.PlatformView));
        Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("NoUnderline", (h, _) => NoUnderline(h.PlatformView));
        // 對話框、「記一筆」是另外的視窗,也不讓系統的「強制深色」反轉顏色(見 SystemBars)
        Microsoft.Maui.Handlers.PageHandler.Mapper.AppendToMapping("NoForceDark", (h, _) =>
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(29)) h.PlatformView.ForceDarkAllowed = false;
        });
#endif
        return builder.Build();
    }
}
