using System.Net.Http;
using System.Text.Json;

// 放在 ZZZ 命名空間:共用的 DataTransfer.cs 會用到 Updater.CurrentVersion
namespace ZZZ;

/// <param name="PackageUrl">Release 附的 Android 安裝檔(Ledger-Android*.apk);沒有則為 null</param>
public record UpdateInfo(string Version, string Notes, string Url, string? PackageUrl);

/// <summary>
/// 手機版的檢查更新:查 GitHub 最新 Release,有新版就用瀏覽器下載 APK。
/// 新版 APK 用同一把金鑰簽章,安裝時會直接覆蓋舊版,資料保留。
/// </summary>
public static class Updater
{
    // 手機版的 APK 發布在這個倉庫(需為 Public,未登入才查得到最新 Release);電腦版仍在 asdZzz-coder/tools
    public const string GitHubRepo = "asdZzz-coder/money-phone-app";

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("ledger-updater"); // GitHub API 要求有 User-Agent
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    /// <summary>目前版本(csproj 的 ApplicationDisplayVersion,CI 依標籤覆寫)。共用的 DataTransfer 寫備份檔時也會用到。</summary>
    public static string CurrentVersion => AppInfo.Current.VersionString;

    static int[] Parse(string v) =>
        v.TrimStart('v', 'V').Split('.').Select(x => int.TryParse(x, out var n) ? n : 0).ToArray();

    static bool IsNewer(string latest, string current)
    {
        int[] a = Parse(latest), b = Parse(current);
        for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            int x = i < a.Length ? a[i] : 0, y = i < b.Length ? b[i] : 0;
            if (x != y) return x > y;
        }
        return false;
    }

    /// <summary>有新版回傳資訊;已是最新回傳 null;失敗(沒網路、倉庫不存在、尚無 Release)丟出例外。</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var json = await Http.GetStringAsync($"https://api.github.com/repos/{GitHubRepo}/releases/latest", cts.Token);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var latest = root.GetProperty("tag_name").GetString() ?? "";
        if (!IsNewer(latest, CurrentVersion)) return null;
        var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
        string? apk = null;
        if (root.TryGetProperty("assets", out var assets))
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                var url = asset.GetProperty("browser_download_url").GetString();
                if (name.StartsWith("Ledger-Android", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) &&
                    Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == "https" && u.Host == "github.com")
                {
                    apk = url;
                    break;
                }
            }
        return new UpdateInfo(latest, notes.Trim(), root.GetProperty("html_url").GetString() ?? "", apk);
    }

    /// <summary>把檢查更新失敗的例外轉成看得懂的原因。</summary>
    public static string Explain(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound } =>
            $"找不到更新資訊(HTTP 404)。\n請確認 GitHub 倉庫 {GitHubRepo} 已設為公開(Public),且已發布過 Release。",
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.TooManyRequests } =>
            "GitHub 暫時拒絕了查詢(請求過於頻繁),請稍後再試。",
        HttpRequestException { StatusCode: { } code } =>
            $"GitHub 回應了錯誤({(int)code} {code}),請稍後再試。",
        HttpRequestException or TaskCanceledException or OperationCanceledException =>
            "無法連線到 GitHub,請確認網路連線。",
        JsonException or KeyNotFoundException or InvalidOperationException =>
            "更新資訊的格式無法辨識,請稍後再試。",
        _ => $"檢查更新失敗:{ex.Message}",
    };
}
