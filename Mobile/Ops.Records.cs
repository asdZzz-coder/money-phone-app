using static ZZZ.Mobile.State;

namespace ZZZ.Mobile;

public static partial class Ops
{
    const string TypeSep = " · "; // 編輯框的「類型 · 分類」選項,例如「支出 · 餐飲」

    /// <summary>點清單裡的一筆:依種類開對應的編輯(轉帳、欠款、分期、一般收支)。</summary>
    public static async Task EditRecord(RecordRow r)
    {
        if (r is TransferRow t) { await EditTransfer(t); return; }
        if (r is DebtMoveRow m) { await EditDebtMove(m); return; }
        if (r.PlanId is long planId) // 分期的一期:改整筆分期
        {
            if (db.Installments().FirstOrDefault(p => p.Id == planId) is InstallmentRow plan) await ManageInstallment(plan);
            return;
        }
        var wallets = db.Wallets();
        var kinds = new List<string>();
        foreach (var type in new[] { Database.Expense, Database.Income })
            kinds.AddRange(db.Categories(type).Select(c => type + TypeSep + c));
        var current = r.Type + TypeSep + r.Category;
        if (!kinds.Contains(current)) kinds.Insert(0, current); // 分類已被刪掉也保留原值
        var v = await Dialogs.Prompt("編輯記錄", "",
        [
            new Field("日期", r.Date, DateHint, IsDate: true),
            new Field("錢包", r.Wallet, Options: wallets.Select(w => w.Name).ToArray()),
            new Field("類型 · 分類", current, Options: kinds.ToArray()),
            new Field("金額", r.Amount.ToString("0.##"), Numeric: true),
            new Field("備註(可空白)", r.Note),
        ], vals =>
        {
            if (!DateTime.TryParse(vals[0], out _)) return "日期格式為 YYYY-MM-DD";
            if (!TryAmount(vals[3], out var a) || a <= 0) return "金額需大於 0";
            return null;
        }, "儲存");
        if (v == null) return;
        TryAmount(v[3], out var amt);
        var parts = v[2].Split(TypeSep, 2);
        var wallet = wallets.First(w => w.Name == v[1]);
        db.UpdateRecord(r.Id, NormalizeDate(v[0]), parts[0], parts[1], amt, v[4], wallet.Id);
        NotifyChanged();
    }

    public static async Task DeleteRecord(RecordRow r)
    {
        if (r is DebtMoveRow { Payment: false })
        {
            await Dialogs.Info("提示", "「借出」「借入」那一筆就是欠款本身,請到「欠款」分頁編輯或刪除。");
            return;
        }
        // 分期的每一期是自動產生的,刪掉其中一期就刪掉整筆分期
        var msg = r switch
        {
            DebtMoveRow => "確定刪除這筆還款?\n刪掉的還款會從欠款的已還金額扣回來。",
            TransferRow t => $"確定刪除這筆{t.Category}({Money(t.Amount)})?",
            { PlanId: not null } => $"這是分期付款的第 {r.Period} 期,刪除會連同這筆分期的全部 {r.Periods} 期一起刪除。確定刪除?",
            _ => $"確定刪除這筆記錄?\n{r.Date} · {r.Category} · {Money(r.Amount)}",
        };
        if (!await Dialogs.Confirm("刪除記錄", msg, "刪除", danger: true)) return;
        switch (r)
        {
            case DebtMoveRow m: db.DeleteDebtPayments([m.Id]); break;
            case TransferRow t: db.DeleteRecords([], [t.Id]); break;
            case { PlanId: long plan }: db.DeleteInstallments([plan]); break;
            default: db.DeleteRecords([r.Id]); break;
        }
        NotifyChanged();
    }

    /// <summary>
    /// 清單裡欠款經過錢包的那幾列:借出/借入那一筆直接開「編輯欠款」;
    /// 還款可改日期、錢包、金額、備註,欠款的已還金額跟著調整。
    /// </summary>
    static async Task EditDebtMove(DebtMoveRow m)
    {
        if (db.Debts().FirstOrDefault(d => d.Id == m.DebtId) is not DebtRow debt) return;
        if (!m.Payment) { await EditDebt(debt); return; }
        var ws = db.Wallets().Where(w => !w.IsCard).ToList();
        var v = await Dialogs.Prompt("編輯還款", $"{debt.Person}({debt.Direction})的還款。",
        [
            new Field("日期", m.Date, DateHint, IsDate: true),
            new Field(m.IOwe ? "從哪個錢包付" : "存進哪個錢包", m.WalletName, Options: ws.Select(w => w.Name).ToArray()),
            new Field("金額", m.Amount.ToString("0.##"), Numeric: true),
            new Field("備註(可空白)", m.Note),
        ], vals =>
        {
            if (!DateTime.TryParse(vals[0], out _)) return "日期格式為 YYYY-MM-DD";
            if (!TryAmount(vals[2], out var a) || a <= 0) return "金額需大於 0";
            if (a > debt.Rest + m.Amount + 0.0001) return "還款金額超過未還金額";
            return null;
        }, "儲存");
        if (v == null) return;
        TryAmount(v[2], out var amt);
        db.UpdateDebtPayment(m.Id, NormalizeDate(v[0]), ws.First(w => w.Name == v[1]).Id, amt, v[3]);
        NotifyChanged();
    }
}
