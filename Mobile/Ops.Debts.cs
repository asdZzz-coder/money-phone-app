using static ZZZ.Mobile.State;

namespace ZZZ.Mobile;

public static partial class Ops
{
    const string NoDebtWallet = "不經過錢包";

    /// <summary>欠款可選的錢包:「不經過錢包」加上信用卡以外的錢包。</summary>
    static string[] DebtWalletOptions(IEnumerable<Wallet> wallets) => [NoDebtWallet, .. wallets.Where(w => !w.IsCard).Select(w => w.Name)];

    static Wallet? DebtWalletNamed(string? name) =>
        name is null or NoDebtWallet ? null : db.Wallets().FirstOrDefault(w => w.Name == name && !w.IsCard);

    public static async Task AddDebt()
    {
        var options = DebtWalletOptions(db.Wallets());
        var v = await Dialogs.Prompt("新增欠款", "錢包:借出時從哪個錢包付、借入時存進哪個錢包;選「不經過錢包」就不影響錢包餘額。",
        [
            new Field("方向", Database.TheyOwe, Options: [Database.TheyOwe, Database.IOwe]),
            new Field("對象", "", "例如:小明"),
            new Field("金額", "", Numeric: true),
            new Field("經過的錢包", NoDebtWallet, Options: options),
            new Field("日期", Today, DateHint, IsDate: true),
            new Field("到期日(可空白)", "", DateHint, IsDate: true),
            new Field("備註(可空白)", ""),
        ], vals =>
        {
            if (vals[1] == "") return "對象不可空白";
            if (!TryAmount(vals[2], out var a) || a <= 0) return "金額需大於 0";
            if (!DateTime.TryParse(vals[4], out _)) return "日期格式為 YYYY-MM-DD";
            if (vals[5] != "" && !DateTime.TryParse(vals[5], out _)) return "到期日格式為 YYYY-MM-DD,或留空";
            if (vals[0] == Database.TheyOwe && DebtWalletNamed(vals[3]) is Wallet w && CheckOutflow(w, a) is string err)
                return $"{err},借不出 {Money(a)}";
            return null;
        }, "新增");
        if (v == null) return;
        TryAmount(v[2], out var amt);
        db.AddDebt(v[1], v[0], amt, NormalizeDate(v[4]), NormalizeDate(v[5]), v[6], DebtWalletNamed(v[3])?.Id);
        NotifyChanged();
    }

    public static async Task PayDebt(DebtRow d)
    {
        if (d.Done) { await Dialogs.Info("提示", "這筆欠款已經還清了。"); return; }
        var options = DebtWalletOptions(db.Wallets());
        var v = await Dialogs.Prompt("記錄還款",
            $"{d.Person}({d.Direction})目前未還 {Money(d.Rest)}\n" +
            (d.IOwe ? "選錢包會從那個錢包付出還款。" : "選錢包會把收到的還款存進那個錢包。"),
        [
            new Field("本次還款金額", "", $"最多 {Money(d.Rest)}", Numeric: true),
            new Field(d.IOwe ? "從哪個錢包付" : "存進哪個錢包", options.Contains(d.WalletName) ? d.WalletName : NoDebtWallet, Options: options),
            new Field("還款日期", Today, DateHint, IsDate: true),
            new Field("備註(可空白)", ""),
        ], vals =>
        {
            if (!TryAmount(vals[0], out var a) || a < 0.01) return "請輸入大於 0 的金額";
            if (a > d.Amount - d.Paid + 0.0001) return "還款金額超過未還金額";
            if (!DateTime.TryParse(vals[2], out _)) return "日期格式為 YYYY-MM-DD";
            if (d.IOwe && DebtWalletNamed(vals[1]) is Wallet w && CheckOutflow(w, a) is string err) return err;
            return null;
        }, "記錄");
        if (v == null) return;
        TryAmount(v[0], out var amt);
        db.PayDebt(d.Id, amt, DebtWalletNamed(v[1])?.Id, NormalizeDate(v[2]), v[3]);
        NotifyChanged();
    }

    public static async Task EditDebt(DebtRow d)
    {
        var options = DebtWalletOptions(db.Wallets());
        var v = await Dialogs.Prompt("編輯欠款", "錢包:借出時從哪個錢包付、借入時存進哪個錢包。",
        [
            new Field("對象", d.Person),
            new Field("方向", d.Direction, Options: [Database.IOwe, Database.TheyOwe]),
            new Field("金額", d.Amount.ToString("0.##"), Numeric: true),
            new Field("已還金額", d.Paid.ToString("0.##"), Numeric: true),
            new Field("錢包", options.Contains(d.WalletName) ? d.WalletName : NoDebtWallet, Options: options),
            new Field("日期", d.Date, DateHint, IsDate: true),
            new Field("到期日(可空白)", d.Due, DateHint, IsDate: true),
            new Field("備註(可空白)", d.Note),
        ], vals =>
        {
            if (vals[0] == "") return "對象不可空白";
            if (!TryAmount(vals[2], out var a) || a <= 0) return "金額需大於 0";
            if (!TryAmount(vals[3] == "" ? "0" : vals[3], out var p) || p < 0) return "已還金額需為 0 或正數";
            if (p > a + 0.0001) return "已還金額不能超過金額";
            if (!DateTime.TryParse(vals[5], out _)) return "日期格式為 YYYY-MM-DD";
            if (vals[6] != "" && !DateTime.TryParse(vals[6], out _)) return "到期日格式為 YYYY-MM-DD,或留空";
            return null;
        }, "儲存");
        if (v == null) return;
        TryAmount(v[2], out var amt);
        TryAmount(v[3] == "" ? "0" : v[3], out var paid);
        db.UpdateDebt(d.Id, v[0], v[1], amt, paid, NormalizeDate(v[5]), NormalizeDate(v[6]), v[7], DebtWalletNamed(v[4])?.Id);
        NotifyChanged();
    }

    public static async Task SettleDebt(DebtRow d)
    {
        if (!await Dialogs.Confirm("標記還清", $"將「{d.Person}」這筆標記為已還清?" +
                (d.WalletId != null ? "\n標記還清不會動到錢包餘額;要記錄錢進出錢包,請用「記錄還款」。" : ""))) return;
        db.SettleDebts([d.Id]);
        NotifyChanged();
    }

    public static async Task DeleteDebt(DebtRow d)
    {
        bool moved = d.WalletId != null || d.Payments > 0;
        if (!await Dialogs.Confirm("刪除欠款", $"確定刪除「{d.Person}」這筆欠款記錄?" +
                (moved ? "\n經過錢包的借款與還款會一起刪除,錢包餘額會跟著還原。" : ""), "刪除", danger: true)) return;
        db.DeleteDebts([d.Id]);
        NotifyChanged();
    }
}
