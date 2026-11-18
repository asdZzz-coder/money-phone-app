using static ZZZ.Mobile.State;

namespace ZZZ.Mobile;

/// <summary>
/// 所有新增、修改、刪除的操作(對應電腦版 MainWindow.xaml.cs 的各個 _Click)。
/// 規則與檢查和電腦版一致;完成後呼叫 State.NotifyChanged() 讓畫面更新。
/// </summary>
public static partial class Ops
{
    static Database db => Db;

    // ---------- 錢包類型 ----------
    const string NormalWallet = "一般錢包 / 戶頭", SavingsWallet = "存錢用錢包";

    static string KindOption(string kind) => kind == Database.Savings ? SavingsWallet : kind == Database.CreditCard ? Database.CreditCard : NormalWallet;
    static string KindOf(string option) => option == SavingsWallet ? Database.Savings : option == Database.CreditCard ? Database.CreditCard : "";

    static int Day(string s) => int.TryParse(s, out var d) && d is >= 1 and <= 31 ? d : 0;
    static double AmountOrZero(string s) => TryAmount(s == "" ? "0" : s, out var a) ? a : 0;
    const string DateHint = "點右邊日曆選擇,或輸入 YYYY-MM-DD";

    public static async Task AddWallet()
    {
        var kind = await Dialogs.Choose("新增錢包", "要新增哪一種?",
        [
            ("💵", "錢包 / 戶頭", "現金、銀行帳戶、電子支付,記錄裡面有多少錢。"),
            ("🐷", SavingsWallet, "專門存錢的帳戶,餘額不算進流動資金。"),
            ("💳", "信用卡", "刷卡時記在卡上,結帳後從設定的扣款錢包繳卡費。會算出每期帳單和繳款日。"),
        ]);
        if (kind == null) return;
        if (kind == 2) { await AddCard(); return; }
        bool saving = kind == 1;
        var v = await Dialogs.Prompt("新增錢包", saving ? "建立一個存錢用的錢包,餘額不會算進流動資金。" : "建立一個新的錢包或銀行戶頭。",
            [new Field("名稱", "", "例如:現金、郵局、LINE Pay"), new Field("目前餘額", "0", "可為 0 或負數", Numeric: true)],
            vals =>
            {
                if (vals[0] == "") return "請輸入錢包名稱";
                if (!TryAmount(vals[1] == "" ? "0" : vals[1], out _)) return "餘額需為數字";
                return null;
            }, "建立");
        if (v == null) return;
        TryAmount(v[1] == "" ? "0" : v[1], out var init);
        var id = db.AddWallet(v[0], init, saving ? Database.Savings : "");
        if (id == null) { await Dialogs.Info("提示", "已有相同名稱的錢包"); return; }
        SelWallet = id;
        NotifyChanged();
    }

    /// <summary>
    /// 信用卡設定欄位:名稱、結帳日、繳款日、繳卡費的錢包、額度(前 5 欄,呼叫端可以再加欄位)。
    /// payers 是可以用來繳卡費的一般錢包。
    /// </summary>
    static (List<Field> Fields, Func<string[], string?> Validate) CardForm(Wallet? w, string name, List<Wallet> payers)
    {
        var fields = new List<Field>
        {
            new("卡片名稱", name, "例如:玉山卡、國泰 CUBE 卡"),
            new("每月結帳日", w?.ClosingDay > 0 ? w.ClosingDay.ToString() : "", "1–31,帳單上的結帳日,例如 5", Numeric: true),
            new("每月繳款日", w?.DueDay > 0 ? w.DueDay.ToString() : "", "1–31,繳款截止日,例如 20", Numeric: true),
            new("繳卡費的錢包", (payers.FirstOrDefault(p => p.Id == w?.PayWalletId) ?? payers[0]).Name,
                Options: payers.Select(p => p.Name).ToArray()),
            new("信用額度(可空白)", w?.Limit > 0 ? w.Limit.ToString("0.##") : "", "例如:80000", Numeric: true),
        };
        return (fields, v =>
        {
            if (v[0] == "") return "請輸入卡片名稱";
            if (Day(v[1]) == 0) return "結帳日請填 1 到 31 的數字";
            if (Day(v[2]) == 0) return "繳款日請填 1 到 31 的數字";
            if (v[4] != "" && (!TryAmount(v[4], out var limit) || limit < 0)) return "信用額度需為數字,或留空";
            return null;
        });
    }

    static async Task AddCard()
    {
        var payers = db.Wallets().Where(w => !w.IsCard).ToList();
        if (payers.Count == 0)
        {
            await Dialogs.Info("新增信用卡", "請先新增一個一般錢包(例如銀行帳戶),信用卡的帳單要從它繳。");
            return;
        }
        var (fields, validate) = CardForm(null, "", payers);
        fields.Add(new Field("目前還沒繳的金額", "0", "已經刷了還沒繳的,沒有就填 0", Numeric: true));
        var v = await Dialogs.Prompt("新增信用卡", "刷卡時把錢包選這張卡;結帳後按「繳卡費」,從連結的錢包付款。",
            fields.ToArray(), vals => validate(vals) ??
                (TryAmount(vals[5] == "" ? "0" : vals[5], out var owed) && owed >= 0 ? null : "還沒繳的金額需為 0 或正數"),
            "建立");
        if (v == null) return;
        var id = db.AddCard(v[0], AmountOrZero(v[5]), AmountOrZero(v[4]), Day(v[1]), Day(v[2]), payers.First(p => p.Name == v[3]).Id);
        if (id == null) { await Dialogs.Info("提示", "已有相同名稱的錢包"); return; }
        SelWallet = id;
        NotifyChanged();
    }

    public static async Task EditWallet(long id)
    {
        var wallets = db.Wallets();
        if (wallets.FirstOrDefault(x => x.Id == id) is not Wallet w) return;
        var payers = wallets.Where(x => !x.IsCard && x.Id != id).ToList();
        if (w.IsCard) { await EditCard(w, w.Name, payers, converting: false); return; }
        var v = await Dialogs.Prompt("編輯錢包", "存錢用錢包的餘額不算進流動資金。",
            [new Field("名稱", w.Name), new Field("類型", KindOption(w.Kind), Options: [NormalWallet, SavingsWallet, Database.CreditCard])],
            vals =>
            {
                if (vals[0] == "") return "名稱不可空白";
                if (vals[1] != Database.CreditCard) return null;
                if (db.Goals().Any(g => g.WalletId == id)) return "這個錢包放了存錢目標,不能改成信用卡";
                if (wallets.Any(c => c.IsCard && c.PayWalletId == id)) return "有信用卡從這個錢包繳卡費,不能改成信用卡";
                if (db.WalletDebtCount(id) > 0) return "有欠款經過這個錢包,不能改成信用卡";
                if (payers.Count == 0) return "要先有另一個一般錢包,信用卡才能從它繳卡費";
                return null;
            });
        if (v == null) return;
        if (v[1] == Database.CreditCard) { await EditCard(w, v[0], payers, converting: true); return; }
        if (v[0] == w.Name && KindOf(v[1]) == w.Kind) return;
        if (!db.UpdateWallet(id, v[0], KindOf(v[1]), 0, 0, 0, null)) { await Dialogs.Info("提示", "已有相同名稱的錢包"); return; }
        NotifyChanged();
    }

    /// <param name="converting">把一般錢包改成信用卡(目前餘額就當作欠款)</param>
    static async Task EditCard(Wallet w, string name, List<Wallet> payers, bool converting)
    {
        if (payers.Count == 0) { await Dialogs.Info("提示", "要先有另一個一般錢包,信用卡才能從它繳卡費。"); return; }
        var (fields, validate) = CardForm(converting ? null : w, name, payers);
        if (!converting) fields.Add(new Field("類型", Database.CreditCard, Options: [Database.CreditCard, NormalWallet, SavingsWallet]));
        bool ToCard(string[] vals) => converting || vals[5] == Database.CreditCard;
        var v = await Dialogs.Prompt(converting ? "改成信用卡" : "編輯信用卡",
            converting ? $"「{w.Name}」目前的餘額 {Money(w.Balance)} 會當作這張卡的" +
                         (w.Balance > 0 ? "溢繳款(多繳的錢,之後刷卡會先扣)。" : "欠款(負數代表欠款)。")
                       : "繳卡費一律從「繳卡費的錢包」扣款。",
            fields.ToArray(), vals => ToCard(vals) ? validate(vals) : vals[0] == "" ? "請輸入名稱"
                : db.Installments(w.Id).Count > 0 ? "這張卡還有分期付款,不能改成一般錢包" : null, "儲存");
        if (v == null) return;
        bool ok = ToCard(v)
            ? db.UpdateWallet(w.Id, v[0], Database.CreditCard, AmountOrZero(v[4]), Day(v[1]), Day(v[2]), payers.First(p => p.Name == v[3]).Id)
            : db.UpdateWallet(w.Id, v[0], KindOf(v[5]), 0, 0, 0, null);
        if (!ok) { await Dialogs.Info("提示", "已有相同名稱的錢包"); return; }
        NotifyChanged();
    }

    public static async Task DeleteWallet(long id)
    {
        if (db.Wallets().FirstOrDefault(w => w.Id == id) is not Wallet wallet) return;
        if (db.WalletCount() <= 1) { await Dialogs.Info("提示", "至少要保留一個錢包"); return; }
        int n = db.WalletRecordCount(id), t = db.WalletTransferCount(id), debts = db.WalletDebtCount(id),
            plans = db.Installments(id).Count;
        var goal = string.Join("、", db.Goals().Where(g => g.WalletId == id).Select(g => g.Name));
        if (!await Dialogs.Confirm("刪除錢包",
                $"刪除「{wallet.Name}」,並一併刪除其中 {n} 筆記錄?\n" +
                (plans > 0 ? $"這張卡的 {plans} 筆分期付款(含還沒到期的各期)也會一起刪除。\n" : "") +
                (goal != "" ? $"存錢目標「{goal}」會改成不指定錢包(已存金額不變)。\n" : "") +
                (t > 0 ? $"和其他錢包之間的 {t} 筆轉帳(轉移資金、繳卡費),會改記成對方錢包的收支(對方餘額不變)。\n" : "") +
                (debts > 0 ? $"經過這個錢包的 {debts} 筆借款、還款會改成不經過錢包(欠款金額與已還金額不變)。\n" : "") +
                "此動作無法復原。", "刪除", danger: true))
            return;
        db.DeleteWallet(id);
        SelWallet = null;
        NotifyChanged();
    }

    /// <summary>錢包的外觀風格:一般或可愛風(粉嫩配色、小動物圖示),只影響手機版的顯示。</summary>
    public static async Task SetStyle(Wallet w)
    {
        var choice = await Dialogs.Choose($"風格:{w.Name}", "選擇這個錢包在清單和餘額卡片上的樣子。",
        [
            ("👛", "一般風格" + (w.IsCute ? "" : "(目前)"), "簡潔的墨色與灰階。"),
            (Theme.CuteIcon(w), "可愛風" + (w.IsCute ? "(目前)" : ""), "粉嫩配色、圓潤卡片,配上小動物圖示。"),
        ]);
        if (choice == null) return;
        db.SetWalletStyle(w.Id, choice == 1 ? Database.Cute : "");
        NotifyChanged();
    }

    /// <summary>錢包往上或往下移一格(取代電腦版側欄的拖曳排序)。</summary>
    public static void MoveWallet(long id, int delta)
    {
        var ids = db.Wallets().Select(w => w.Id).ToList();
        int i = ids.IndexOf(id), j = i + delta;
        if (i < 0 || j < 0 || j >= ids.Count) return;
        (ids[i], ids[j]) = (ids[j], ids[i]);
        db.ReorderWallets(ids);
        NotifyChanged();
    }

    // ---------- 轉移資金 ----------
    /// <summary>在一般錢包(含存錢用錢包)之間搬錢;信用卡只能用「繳卡費」從扣款錢包轉入。</summary>
    public static async Task Transfer()
    {
        var wallets = db.Wallets().Where(w => !w.IsCard).ToList();
        if (wallets.Count < 2) { await Dialogs.Info("轉移資金", "至少要有兩個錢包(不含信用卡)才能轉移資金。"); return; }
        var from = wallets.FirstOrDefault(w => w.Id == SelWallet) ?? wallets[0];
        var names = wallets.Select(w => w.Name).ToArray();
        var v = await Dialogs.Prompt("轉移資金", "把錢從一個錢包搬到另一個,不算收入也不算支出。信用卡請用「繳卡費」。",
        [
            new Field("從", from.Name, Options: names),
            new Field("轉到", wallets.First(w => w.Id != from.Id).Name, Options: names),
            new Field("金額", "", "例如:5000", Numeric: true),
            new Field("日期", Today, DateHint, IsDate: true),
            new Field("備註(可空白)", ""),
        ], vals =>
        {
            if (vals[0] == vals[1]) return "轉出和轉入不能是同一個錢包";
            if (!TryAmount(vals[2], out var a) || a <= 0) return "金額需大於 0";
            if (CheckOutflow(wallets.First(w => w.Name == vals[0]), a) is string err) return err;
            if (!DateTime.TryParse(vals[3], out _)) return "日期格式為 YYYY-MM-DD";
            return null;
        }, "轉帳");
        if (v == null) return;
        TryAmount(v[2], out var amt);
        db.AddTransfer(NormalizeDate(v[3]), wallets.First(w => w.Name == v[0]).Id, wallets.First(w => w.Name == v[1]).Id, amt, v[4]);
        NotifyChanged();
    }

    static async Task EditTransfer(TransferRow t)
    {
        if (t.ToCard) { await EditCardPayment(t); return; }
        var wallets = db.Wallets().Where(w => !w.IsCard || w.Id == t.FromId || w.Id == t.ToId).ToList();
        var names = wallets.Select(w => w.Name).ToArray();
        var v = await Dialogs.Prompt("編輯轉移資金", "",
        [
            new Field("日期", t.Date, DateHint, IsDate: true),
            new Field("從", t.From, Options: names),
            new Field("轉到", t.To, Options: names),
            new Field("金額", t.Amount.ToString("0.##"), Numeric: true),
            new Field("備註(可空白)", t.Note),
        ], vals =>
        {
            if (!DateTime.TryParse(vals[0], out _)) return "日期格式為 YYYY-MM-DD";
            if (vals[1] == vals[2]) return "轉出和轉入不能是同一個錢包";
            if (wallets.First(w => w.Name == vals[2]).IsCard) return "信用卡請用「繳卡費」";
            if (!TryAmount(vals[3], out var a) || a <= 0) return "金額需大於 0";
            return null;
        }, "儲存");
        if (v == null) return;
        TryAmount(v[3], out var amt);
        db.UpdateTransfer(t.Id, NormalizeDate(v[0]), wallets.First(w => w.Name == v[1]).Id, wallets.First(w => w.Name == v[2]).Id, amt, v[4]);
        NotifyChanged();
    }

    /// <summary>繳卡費的扣款錢包是當時卡片設定的那個,這裡只改日期、金額、備註。</summary>
    static async Task EditCardPayment(TransferRow t)
    {
        var v = await Dialogs.Prompt("編輯繳卡費", $"從「{t.From}」繳「{t.To}」的卡費。",
        [
            new Field("繳費日期", t.Date, DateHint, IsDate: true),
            new Field("已繳金額", t.Amount.ToString("0.##"), Numeric: true),
            new Field("備註(可空白)", t.Note),
        ], vals =>
        {
            if (!DateTime.TryParse(vals[0], out _)) return "日期格式為 YYYY-MM-DD";
            if (!TryAmount(vals[1], out var a) || a <= 0) return "金額需大於 0";
            return null;
        }, "儲存");
        if (v == null) return;
        TryAmount(v[1], out var amt);
        db.UpdateTransfer(t.Id, NormalizeDate(v[0]), t.FromId, t.ToId, amt, v[2]);
        NotifyChanged();
    }

    // ---------- 信用卡 ----------
    /// <summary>
    /// 繳卡費:按「已繳費」才從卡片設定的扣款錢包轉一筆錢到信用卡
    /// (不算支出,刷卡時已經記過了)。要換扣款錢包請到「編輯錢包」。
    /// 隨時都能繳,沒有欠款或超過欠款也可以,多繳的就是溢繳款(卡片餘額為正)。
    /// </summary>
    public static async Task PayCard()
    {
        var wallets = db.Wallets();
        if (wallets.FirstOrDefault(w => w.Id == SelWallet && w.IsCard) is not Wallet card) return;
        if (wallets.FirstOrDefault(w => w.Id == card.PayWalletId && !w.IsCard) is not Wallet payer)
        {
            await Dialogs.Info("繳卡費", $"「{card.Name}」還沒有扣款錢包,請先到「錢包」編輯這張卡,設定繳卡費的錢包。");
            return;
        }
        var bill = db.Bill(card);
        bool owing = card.Owed > 0.0001;
        var msg = (!owing ? $"目前沒有欠款{(card.Credit > 0.0001 ? $"(已溢繳 {Money(card.Credit)})" : "")},現在繳的錢會成為溢繳款,之後刷卡會先扣。"
                   : bill.Settled ? $"本期帳單已繳清,目前未出帳 {Money(bill.Unbilled)}。"
                   : $"本期帳單({bill.Closing:M/d} 結帳)還要繳 {Money(bill.Remaining)},繳款日 {bill.Due:M/d}。")
                  + (bill.Future > 0.0001 ? $"\n分期還沒到期的 {Money(bill.Future)} 會在每期出帳後再繳。" : "")
                  + (owing ? "\n金額可以超過欠款,多繳的部分會成為溢繳款。" : "")
                  + $"\n按「已繳費」後從「{payer.Name}」扣款,不會重複算成支出。";
        var v = await Dialogs.Prompt($"繳卡費:{card.Name}", msg,
        [
            new Field("已繳金額", !owing ? "" : bill.Settled ? (bill.Unbilled > 0.0001 ? bill.Unbilled.ToString("0.##") : "")
                                             : bill.Remaining.ToString("0.##"), Numeric: true),
            new Field("繳費日期", Today, DateHint, IsDate: true),
            new Field("備註(可空白)", ""),
        ], vals =>
        {
            if (!TryAmount(vals[0], out var a) || a <= 0) return "金額需大於 0";
            if (!DateTime.TryParse(vals[1], out _)) return "日期格式為 YYYY-MM-DD";
            return null;
        }, "已繳費");
        if (v == null) return;
        TryAmount(v[0], out var amt);
        db.AddTransfer(NormalizeDate(v[1]), payer.Id, card.Id, amt, v[2]);
        NotifyChanged();
    }

    // ---------- 信用卡分期 ----------
    /// <summary>大卡片的「分期」:沒有分期就直接新增;有的話先選要新增還是管理哪一筆。</summary>
    public static async Task Installments()
    {
        if (db.Wallets().FirstOrDefault(w => w.Id == SelWallet && w.IsCard) is not Wallet card) return;
        var plans = db.Installments(card.Id);
        if (plans.Count == 0) { await InstallmentForm(card, null); return; }
        var choice = await Dialogs.Choose($"分期:{card.Name}", "新增一筆分期,或選一筆修改、刪除。",
            [("+", "新增分期", "例如刷卡買手機分 12 期,每一期自動記一筆支出"),
             .. plans.Select(p => ("", p.Note == "" ? p.Category : p.Note, p.Summary))]);
        if (choice is int i) { if (i == 0) await InstallmentForm(card, null); else await ManageInstallment(plans[i - 1]); }
    }

    static async Task ManageInstallment(InstallmentRow p)
    {
        if (db.Wallets().FirstOrDefault(w => w.Id == p.WalletId) is not Wallet card) return;
        var name = p.Note == "" ? p.Category : p.Note;
        var choice = await Dialogs.Choose($"分期:{name}", p.Summary + (p.Rate > 0 ? $"\n利息合計 {Money(p.Interest)}" : ""),
        [
            ("✎", "修改分期", "改金額、期數、利率或日期,每一期會依新內容重新產生"),
            ("🗑", "刪除分期", $"刪掉這筆分期和它全部 {p.Periods} 期的支出記錄"),
        ]);
        if (choice == 0) await InstallmentForm(card, p);
        else if (choice == 1 &&
                 await Dialogs.Confirm("刪除分期", $"刪除「{name}」的分期,連同全部 {p.Periods} 期的支出記錄?\n此動作無法復原。", "刪除", danger: true))
        {
            db.DeleteInstallments([p.Id]);
            NotifyChanged();
        }
    }

    /// <summary>新增或修改分期。輸入時下方即時試算每期要繳多少、利息合計。</summary>
    /// <param name="p">要修改的分期;null 為新增</param>
    static async Task InstallmentForm(Wallet card, InstallmentRow? p)
    {
        var cats = db.Categories(Database.Expense);
        if (p != null && !cats.Contains(p.Category)) cats.Insert(0, p.Category); // 分類已被刪掉也保留原值
        if (cats.Count == 0) { await Dialogs.Info("提示", "請先在「更多 → 管理分類」建立支出分類。"); return; }
        // 欄位:品項、分類、總金額、期數、年利率、第 1 期日期
        static string? Parse(string[] vals, out double amount, out int periods, out double rate)
        {
            periods = 0; rate = 0;
            if (!Database.TryParseAmount(vals[2], out amount) || amount <= 0) return "總金額需大於 0";
            if (!int.TryParse(vals[3], out periods) || periods < 2 || periods > 120) return "期數請輸入 2 到 120";
            if (!Database.TryParseAmount(vals[4].TrimEnd('%'), out rate) || rate < 0 || rate > 100) return "年利率請輸入 0 到 100(%)";
            if (amount < periods) return "總金額太少,每期至少要 1 元";
            return null;
        }
        var v = await Dialogs.Prompt(p == null ? $"新增分期:{card.Name}" : "修改分期",
            "每一期會自動在這張卡記一筆支出,出現在當期的帳單。\n利息用本息平均攤還:每期繳一樣多,利息按剩下的本金算,逐期遞減。",
        [
            new Field("品項", p?.Note ?? "", "例如:手機"),
            new Field("分類", p?.Category ?? (cats.Contains("購物") ? "購物" : cats[0]), Options: cats.ToArray()),
            new Field("總金額", p?.Amount.ToString("0.##") ?? "", Numeric: true),
            new Field("分幾期", p?.Periods.ToString() ?? "12", Numeric: true),
            new Field("年利率(%),0 為無息分期", p?.Rate.ToString("0.###") ?? "0", Numeric: true),
            new Field("第 1 期日期(刷卡日)", p?.Date ?? Today, DateHint, IsDate: true),
        ], vals =>
        {
            if (vals[0] == "") return "請輸入品項";
            if (Parse(vals, out _, out _, out _) is string err) return err;
            if (!DateTime.TryParse(vals[5], out _)) return "日期格式為 YYYY-MM-DD";
            return null;
        }, p == null ? "新增" : "儲存",
        preview: vals => Parse(vals, out var a, out var n, out var r) == null ? Installment.Summary(a, n, r) : "");
        if (v == null) return;
        Parse(v, out var amount, out var periods, out var rate);
        if (p == null) db.AddInstallment(card.Id, NormalizeDate(v[5]), v[1], amount, periods, rate, v[0]);
        else db.UpdateInstallment(p.Id, NormalizeDate(v[5]), v[1], amount, periods, rate, v[0]);
        NotifyChanged();
    }
}
