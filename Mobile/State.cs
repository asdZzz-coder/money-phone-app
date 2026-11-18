namespace ZZZ.Mobile;

/// <summary>
/// 各分頁共用的狀態(對應電腦版 MainWindow 的欄位):資料庫、目前選的錢包、收支記錄的期間與分類篩選、圖表期間。
/// 任何寫入後呼叫 <see cref="NotifyChanged"/>,開著的分頁會重新查詢。
/// </summary>
public static class State
{
    public static Database Db { get; } = new();

    /// <summary>null = 全部錢包</summary>
    public static long? SelWallet;
    /// <summary>收支記錄的期間:yyyy-MM、yyyy(整年)或空白(全部期間)</summary>
    public static string Month = DateTime.Today.ToString("yyyy-MM");
    /// <summary>從支出圖表點分類過來時,只顯示該分類的支出</summary>
    public static string? CatFilter;
    /// <summary>支出圖表的期間:yyyy-MM 一個月、yyyy 一整年、空白為全部期間</summary>
    public static string StatsPeriod = DateTime.Today.ToString("yyyy-MM");

    public static event Action? Changed;

    public static void NotifyChanged()
    {
        var wallets = Db.Wallets();
        if (SelWallet != null && wallets.All(w => w.Id != SelWallet)) SelWallet = null;
        Changed?.Invoke();
    }

    public static string Money(double v) => v.ToString("N0");

    public static bool TryAmount(string s, out double v) => Database.TryParseAmount(s, out v);

    public static string NormalizeDate(string s) => DateTime.TryParse(s, out var d) ? d.ToString("yyyy-MM-dd") : "";

    public static string Today => DateTime.Today.ToString("yyyy-MM-dd");

    /// <summary>流動資金:一般錢包的餘額合計(不含存錢用錢包與信用卡)。</summary>
    public static double Liquid(List<Wallet> wallets) => wallets.Where(w => w.Kind == "").Sum(w => w.Balance);

    /// <summary>錢包裡還沒分配給存錢目標的錢(不算 exceptGoal);可能是負的,表示分配超過餘額。</summary>
    public static double WalletFree(long walletId, long? exceptGoal = null) =>
        (Db.Wallets().FirstOrDefault(w => w.Id == walletId)?.Balance ?? 0)
        - Db.Goals().Where(x => x.WalletId == walletId && x.Id != exceptGoal).Sum(x => x.Saved);

    /// <summary>從錢包付出 amount(轉出、借出、還錢):不能超過餘額扣掉分配給存錢目標的錢。</summary>
    public static string? CheckOutflow(Wallet src, double amount)
    {
        var free = WalletFree(src.Id);
        if (amount <= free + 0.0001) return null;
        return free < src.Balance - 0.0001
            ? $"「{src.Name}」可以轉出的只有 {Money(Math.Max(free, 0))}(其餘已分配給存錢目標)"
            : $"「{src.Name}」的餘額只有 {Money(Math.Max(src.Balance, 0))}";
    }

    /// <summary>收支記錄清單(含分類篩選)。</summary>
    public static List<RecordRow> FilteredRecords()
    {
        var rows = Db.Records(Month, SelWallet);
        return CatFilter == null ? rows : rows.Where(r => !r.IsIncome && r.Category == CatFilter).ToList();
    }

    public static string WalletName => SelWallet is long id ? Db.Wallets().FirstOrDefault(w => w.Id == id)?.Name ?? "全部錢包" : "全部錢包";

    public static string PeriodName(string p) => p.Length switch
    {
        0 => "全部期間",
        4 => $"{p} 年",
        _ => DateTime.TryParseExact(p, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out var d) ? d.ToString("yyyy 年 M 月") : p,
    };

    /// <summary>期間往前或往後移;整年移一年,月份移一個月,空白(全部)不動。</summary>
    public static string ShiftPeriod(string p, int delta)
    {
        if (p.Length == 4 && int.TryParse(p, out var year)) return (year + delta).ToString();
        var d = DateTime.TryParseExact(p + "-01", "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var m)
            ? m : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        return d.AddMonths(delta).ToString("yyyy-MM");
    }
}
