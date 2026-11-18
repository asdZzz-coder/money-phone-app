using static ZZZ.Mobile.State;

namespace ZZZ.Mobile;

/// <summary>
/// 匯出 / 匯入 / 刪除全部資料。檔案格式與電腦版相同(DataTransfer.cs 共用),
/// 所以電腦版匯出的 .json 可以在手機匯入,反過來也可以。
/// 手機上匯出是先存到暫存資料夾,再用系統的「分享」存到雲端硬碟、傳 LINE 等。
/// </summary>
public static partial class Ops
{
    public static async Task Export()
    {
        var period = Month == "" ? "全部期間" : Month;
        var choice = await Dialogs.Choose("匯出資料", "選擇要匯出的內容,接著可以存到雲端硬碟、檔案或傳給電腦:",
        [
            ("💾", "完整備份(.json)", "所有錢包、收支、欠款、分類與存錢目標,可用「匯入資料」完整還原(電腦版也可以匯入)"),
            ("📄", "目前篩選的收支記錄(.csv)", $"{period} · {WalletName}{(CatFilter != null ? $" · {CatFilter}" : "")} · 共 {FilteredRecords().Count} 筆,可用 Excel 開啟"),
            ("📄", "全部收支記錄(.csv)", $"所有期間與錢包 · 共 {db.Records("", null).Count} 筆"),
        ]);
        if (choice == null) return;
        bool json = choice == 0;
        var name = json ? $"記帳備份_{DateTime.Now:yyyyMMdd}.json"
                 : choice == 1 ? $"記帳_{(Month == "" ? "全部" : Month)}.csv" : "記帳_全部.csv";
        var dir = Path.Combine(FileSystem.CacheDirectory, "export");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        if (json) DataTransfer.ExportBackup(db, path);
        else DataTransfer.ExportCsv(choice == 1 ? FilteredRecords() : db.Records("", null), path);
        await Share.Default.RequestAsync(new ShareFileRequest { Title = name, File = new ShareFile(path) });
    }

    public static async Task Import()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "選擇完整備份(.json)或收支記錄(.csv)" });
        if (file == null) return;
        // 先複製到暫存資料夾,共用的 DataTransfer 讀的是檔案路徑
        var dir = Path.Combine(FileSystem.CacheDirectory, "import");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, Path.GetFileName(file.FileName));
        await using (var src = await file.OpenReadAsync())
        await using (var dst = File.Create(path))
            await src.CopyToAsync(dst);
        try
        {
            if (Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase)) await ImportCsv(path);
            else await ImportBackup(path);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            await Dialogs.Error("匯入失敗", ex.Message);
        }
    }

    public static async Task ImportBackup(string path)
    {
        var (data, s) = DataTransfer.ReadBackup(path);
        var when = s.ExportedAt != "" ? $"(匯出於 {s.ExportedAt})" : "";
        if (!await Dialogs.Confirm("匯入完整備份",
                $"備份內容{when}:\n錢包 {s.Wallets} 個、收支 {s.Records} 筆、欠款 {s.Debts} 筆、存錢目標 {s.Goals} 個。\n\n" +
                "匯入後會「取代」手機上目前所有資料。\n匯入前會自動把目前資料另存一份(在「更多 → 手機內的備份」)。", "取代並匯入", danger: true))
            return;
        DataTransfer.AutoBackup(db);
        db.ReplaceAll(data);
        SelWallet = null;
        CatFilter = null;
        NotifyChanged();
        await Dialogs.Success("匯入完成", "資料已還原。");
    }

    static async Task ImportCsv(string path)
    {
        var result = DataTransfer.ReadCsv(path);
        var skipped = result.Errors.Count == 0 ? ""
            : $"\n\n有 {result.Errors.Count} 行無法辨識,將略過:\n" + string.Join("\n", result.Errors.Take(3)) +
              (result.Errors.Count > 3 ? "\n…" : "");
        if (result.Rows.Count == 0)
        {
            await Dialogs.Error("沒有可匯入的記錄", "檔案中找不到有效的收支記錄。" + skipped);
            return;
        }
        if (!await Dialogs.Confirm("匯入收支記錄",
                $"將新增 {result.Rows.Count} 筆收支記錄到現有資料(不會刪除任何資料)。\n" +
                "不存在的錢包與分類會自動建立。" + skipped, "匯入"))
            return;
        db.ImportRecords(result.Rows);
        SelWallet = null;
        CatFilter = null;
        NotifyChanged();
        await Dialogs.Success("匯入完成", $"已新增 {result.Rows.Count} 筆收支記錄。");
    }

    /// <summary>刪除全部資料:先確認一次,再要求輸入「刪除」做第二次確認。刪除前會自動存一份 .json 備份。</summary>
    public static async Task ClearAll()
    {
        const string Word = "刪除";
        if (!await Dialogs.Confirm("刪除全部資料",
                $"目前有錢包 {db.Count("wallets")} 個、收支 {db.Count("records")} 筆、轉帳 {db.Count("transfers")} 筆、" +
                $"欠款 {db.Count("debts")} 筆、存錢目標 {db.Count("goals")} 個。\n\n" +
                "全部刪除後只會留下空的「現金」錢包與預設分類。\n" +
                "刪除前會自動另存一份完整備份,之後可在「更多 → 手機內的備份」還原。", "繼續", danger: true))
            return;
        if (await Dialogs.Prompt("再次確認刪除",
                $"這個動作會清空所有資料。\n確定要刪除的話,請輸入「{Word}」。",
                [new Field("確認文字", Hint: Word)],
                v => v[0] == Word ? null : $"請輸入「{Word}」才能刪除", "刪除全部資料", danger: true) == null)
            return;
        try { DataTransfer.SafetyBackup(db); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Dialogs.Error("沒有刪除", "無法先備份目前資料,所以沒有刪除任何東西。\n" + ex.Message);
            return;
        }
        db.ClearAll();
        SelWallet = null;
        CatFilter = null;
        NotifyChanged();
        await Dialogs.Success("已刪除全部資料", "原本的資料已備份,可在「更多 → 手機內的備份」還原。");
    }

    /// <summary>
    /// 手機內的備份:匯入前(.db)與刪除全部資料前(.json)自動存的備份。
    /// 放在 App 自己的資料夾,其他 App 看不到,所以在這裡提供還原與分享。
    /// </summary>
    public static async Task LocalBackups()
    {
        var files = Directory.Exists(DataTransfer.BackupDir)
            ? new DirectoryInfo(DataTransfer.BackupDir).GetFiles().OrderByDescending(f => f.LastWriteTime).ToList()
            : [];
        if (files.Count == 0) { await Dialogs.Info("手機內的備份", "目前沒有備份。\n匯入資料或刪除全部資料之前,會自動在這裡存一份。"); return; }
        var choice = await Dialogs.Choose("手機內的備份", "選一個備份還原或分享出去:",
            files.Take(20).Select(f => (f.Extension == ".json" ? "💾" : "🗄",
                f.Extension == ".json" ? "刪除前的完整備份" : "匯入前的資料庫備份",
                $"{f.LastWriteTime:yyyy-MM-dd HH:mm} · {f.Length / 1024.0:N0} KB")).ToArray());
        if (choice is not int i) return;
        var file = files[i];
        var actions = file.Extension == ".json" ? new[] { "還原這個備份", "分享出去" } : ["分享出去"];
        var act = await Dialogs.Menu(file.Name, actions);
        if (act == "還原這個備份") await ImportBackup(file.FullName);
        else if (act == "分享出去") await Share.Default.RequestAsync(new ShareFileRequest { Title = file.Name, File = new ShareFile(file.FullName) });
    }

    // ---------- 檢查更新 ----------
    public static async Task CheckUpdate(bool silent)
    {
        UpdateInfo? info;
        try { info = await Updater.CheckAsync(); }
        catch (Exception ex)
        {
            if (!silent) await Dialogs.Error("檢查更新", Updater.Explain(ex));
            return;
        }
        if (info == null)
        {
            if (!silent) await Dialogs.Success("檢查更新", $"目前已是最新版本(v{Updater.CurrentVersion})。");
            return;
        }
        var notes = info.Notes.Length > 400 ? info.Notes[..400] : info.Notes;
        if (notes == "") notes = "(無說明)";
        var how = info.PackageUrl != null
            ? "按「下載」會用瀏覽器下載新版 APK,下載完點開它安裝即可(會直接覆蓋舊版,記帳資料不會不見)。"
            : "按「下載」會打開下載頁面。";
        if (await Dialogs.Confirm("發現新版本", $"有新版本 {info.Version} 可用(目前 v{Updater.CurrentVersion})。\n\n{notes}\n\n{how}", "下載"))
            await Launcher.Default.OpenAsync(info.PackageUrl ?? info.Url);
    }
}
