using static ZZZ.Mobile.State;

namespace ZZZ.Mobile;

public static partial class Ops
{
    const string NoWallet = "不指定錢包";

    /// <summary>
    /// 目標的輸入欄位:名稱、金額、日期、存放的錢包、備註(每年繳費的日期必填)。
    /// 同一個錢包可以放多個目標;換到別的錢包時,已存的錢要放得進那個錢包可分配的餘額。
    /// </summary>
    static (Field[] Fields, Func<string[], string?> Validate, Func<string, long?> WalletOf) GoalForm(bool yearly, GoalRow? g = null)
    {
        var wallets = db.Wallets();
        long? WalletOf(string name) => wallets.FirstOrDefault(w => w.Name == name)?.Id;
        var walletField = new Field("錢放在哪個錢包(存入時從這個錢包分配)", g?.Linked == true ? g.WalletName : NoWallet,
                                    Options: [NoWallet, .. wallets.Where(w => !w.IsCard).Select(w => w.Name)]);
        Field[] fields = yearly
        ? [
            new Field("費用名稱", g?.Name ?? "", "例如:汽車牌照稅、燃料稅、汽車保險"),
            new Field("每年金額", g?.Target.ToString("0.##") ?? "", "例如:11230", Numeric: true),
            new Field("下次繳費日", g?.Deadline ?? "", DateHint, IsDate: true),
            walletField,
            new Field("備註(可空白)", g?.Note ?? ""),
        ]
        : [
            new Field("目標名稱", g?.Name ?? "", "例如:日本旅行、緊急預備金"),
            new Field("目標金額", g?.Target.ToString("0.##") ?? "", "例如:50000", Numeric: true),
            new Field("目標日期(可空白)", g?.Deadline ?? "", DateHint, IsDate: true),
            walletField,
            new Field("備註(可空白)", g?.Note ?? ""),
        ];
        return (fields, v =>
        {
            if (v[0] == "") return yearly ? "請輸入費用名稱" : "請輸入目標名稱";
            if (!TryAmount(v[1], out var t) || t <= 0) return yearly ? "每年金額需大於 0" : "目標金額需大於 0";
            if (yearly && v[2] == "") return "請選擇下次繳費日";
            if (v[2] != "" && !DateTime.TryParse(v[2], out _)) return "日期格式為 YYYY-MM-DD" + (yearly ? "" : ",或留空");
            if (g != null && WalletOf(v[3]) is long w && w != g.WalletId && g.Saved > 0.0001)
            {
                var free = WalletFree(w, g.Id);
                if (g.Saved > free + 0.0001) return $"「{v[3]}」可分配的錢只有 {Money(Math.Max(free, 0))},這個目標已存 {Money(g.Saved)}";
            }
            return null;
        }, WalletOf);
    }

    public static async Task AddGoal()
    {
        var kind = await Dialogs.Choose("新增存錢目標", "要存哪一種?",
        [
            ("🎯", "一次性目標", "存到目標金額就完成,例如日本旅行、緊急預備金、新手機。"),
            ("📅", "每年繳費", "每年固定要繳的費用,例如汽車牌照稅、燃料稅、保險費。繳費後自動換到下一年。"),
        ]);
        if (kind == null) return;
        bool yearly = kind == 1;
        var (fields, validate, walletOf) = GoalForm(yearly);
        var v = await Dialogs.Prompt(yearly ? "新增每年繳費" : "新增存錢目標",
            yearly ? "設定每年要繳的金額與繳費日,程式會算出每個月該存多少。" : "設定想存到的金額,也可以加上目標日期。",
            fields, validate, "建立");
        if (v == null) return;
        TryAmount(v[1], out var target);
        db.AddGoal(v[0], target, NormalizeDate(v[2]), v[4], yearly ? Database.Yearly : "", walletOf(v[3]));
        NotifyChanged();
    }

    public static async Task EditGoal(GoalRow g)
    {
        var (fields, validate, walletOf) = GoalForm(g.Yearly, g);
        var v = await Dialogs.Prompt(g.Yearly ? "編輯每年繳費" : "編輯目標", "", fields, validate, "儲存");
        if (v == null) return;
        TryAmount(v[1], out var target);
        db.UpdateGoal(g.Id, v[0], target, NormalizeDate(v[2]), v[4], walletOf(v[3]));
        NotifyChanged();
    }

    public static async Task PayGoal(GoalRow g)
    {
        var next = DateTime.TryParse(g.Deadline, out var due) ? due.AddYears(1) : DateTime.Today.AddYears(1);
        var year = due != default ? due.Year : DateTime.Today.Year;
        if (g.Linked) { await PayFromWallet(g, year, next); return; }
        var v = await Dialogs.Prompt($"繳費:{g.Name}",
            $"目前已存 {Money(g.Saved)}。繳費金額會從已存金額扣除,下次繳費日改為 {next:yyyy-MM-dd}。",
        [
            new Field("繳費金額", g.Target.ToString("0.##"), Numeric: true),
            new Field("繳費日期", Today, DateHint, IsDate: true),
        ], vals =>
        {
            if (!TryAmount(vals[0], out var a) || a <= 0) return "請輸入大於 0 的金額";
            if (!DateTime.TryParse(vals[1], out _)) return "日期格式為 YYYY-MM-DD";
            return null;
        }, "已繳費");
        if (v == null) return;
        TryAmount(v[0], out var paid);
        // 已存的不夠時只扣到 0,不足的部分當作從別處付
        var withdraw = Math.Min(paid, Math.Max(g.Saved, 0));
        var note = $"{year} 年繳費" + (paid > withdraw + 0.0001 ? $"(實繳 {Money(paid)})" : "");
        db.PayYearly(g.Id, NormalizeDate(v[1]), withdraw, note, next.ToString("yyyy-MM-dd"));
        NotifyChanged();
    }

    /// <summary>放在錢包裡的每年繳費:在該錢包記一筆支出,並只從這個目標已存的錢扣除(同錢包的其他目標不受影響)。</summary>
    static async Task PayFromWallet(GoalRow g, int year, DateTime next)
    {
        var cats = db.Categories(Database.Expense);
        if (cats.Count == 0) { await Dialogs.Info("提示", "請先在「更多 → 管理分類」建立支出分類。"); return; }
        var v = await Dialogs.Prompt($"繳費:{g.Name}",
            $"會在「{g.WalletName}」記一筆支出,並從這個目標已存的 {Money(g.Saved)} 扣除;下次繳費日改為 {next:yyyy-MM-dd}。",
        [
            new Field("繳費金額", g.Target.ToString("0.##"), Numeric: true),
            new Field("繳費日期", Today, DateHint, IsDate: true),
            new Field("支出分類", cats.Contains("其他") ? "其他" : cats[0], Options: cats.ToArray()),
        ], vals =>
        {
            if (!TryAmount(vals[0], out var a) || a <= 0) return "請輸入大於 0 的金額";
            if (!DateTime.TryParse(vals[1], out _)) return "日期格式為 YYYY-MM-DD";
            if (vals[2] == "") return "請先在「管理分類」建立支出分類";
            return null;
        }, "已繳費");
        if (v == null) return;
        TryAmount(v[0], out var paid);
        var withdraw = Math.Min(paid, Math.Max(g.Saved, 0));
        var note = $"{year} 年繳費" + (paid > withdraw + 0.0001 ? $"(實繳 {Money(paid)})" : "");
        db.PayYearlyFromWallet(g.Id, g.WalletId!.Value, NormalizeDate(v[1]), v[2], paid,
                               $"{g.Name} {year} 年繳費", withdraw, note, next.ToString("yyyy-MM-dd"));
        NotifyChanged();
    }

    public static async Task DeleteGoal(GoalRow g)
    {
        if (!await Dialogs.Confirm("刪除目標", $"刪除「{g.Name}」以及其中 {g.Count} 筆存取紀錄?\n此動作無法復原。", "刪除", danger: true))
            return;
        db.DeleteGoal(g.Id);
        NotifyChanged();
    }

    public static async Task GoalMove(GoalRow g, bool deposit)
    {
        if (!deposit && g.Saved <= 0) { await Dialogs.Info("提示", "這個目標目前沒有存款可以取出。"); return; }
        double free = g.WalletId is long w ? WalletFree(w) : double.PositiveInfinity; // 錢包還能分配多少
        if (deposit && free <= 0.0001)
        {
            await Dialogs.Info("提示", $"「{g.WalletName}」的錢都已經分配給存錢目標了,目前沒有可以存入的餘額。");
            return;
        }
        var msg = deposit
            ? $"「{g.Name}」已存 {Money(g.Saved)},還差 {Money(g.Remaining)}"
            : $"「{g.Name}」目前已存 {Money(g.Saved)}";
        if (deposit && g.Linked) msg += $"\n從「{g.WalletName}」分配,可分配 {Money(free)}";
        var v = await Dialogs.Prompt(deposit ? "存入" : "取出", msg,
        [
            new Field("金額", "", deposit && g.Remaining > 0 ? $"還差 {Money(g.Remaining)}" : $"最多 {Money(g.Saved)}", Numeric: true),
            new Field("日期", Today, DateHint, IsDate: true),
            new Field("備註(可空白)", "", deposit ? "例如:薪水提撥" : "例如:臨時急用"),
        ], vals =>
        {
            if (!TryAmount(vals[0], out var a) || a <= 0) return "請輸入大於 0 的金額";
            if (!deposit && a > g.Saved + 0.0001) return "取出金額超過目前已存金額";
            if (deposit && a > free + 0.0001) return $"「{g.WalletName}」只剩 {Money(free)} 可以分配";
            if (!DateTime.TryParse(vals[1], out _)) return "日期格式為 YYYY-MM-DD";
            return null;
        }, deposit ? "存入" : "取出");
        if (v == null) return;
        TryAmount(v[0], out var amt);
        bool wasDone = g.Done;
        db.AddDeposit(g.Id, NormalizeDate(v[1]), deposit ? amt : -amt, v[2]);
        NotifyChanged();
        if (deposit && !wasDone && g.Saved + amt >= g.Target - 0.0001)
        {
            if (g.Yearly)
                await Dialogs.Success("已存足!", $"「{g.Name}」今年的 {Money(g.Target)} 已經存好了。\n繳完費後按「繳費」,就會換到下一年。");
            else
                await Dialogs.Success("目標達成!", $"恭喜!「{g.Name}」已經存到 {Money(g.Target)} 了。");
        }
    }

    public static async Task EditDeposit(GoalRow? g, DepositRow d)
    {
        var v = await Dialogs.Prompt("編輯存取紀錄", "",
        [
            new Field("類型", d.IsDeposit ? "存入" : "取出", Options: ["存入", "取出"]),
            new Field("金額", Math.Abs(d.Amount).ToString("0.##"), Numeric: true),
            new Field("日期", d.Date, DateHint, IsDate: true),
            new Field("備註(可空白)", d.Note),
        ], vals =>
        {
            if (!TryAmount(vals[1], out var a) || a <= 0) return "請輸入大於 0 的金額";
            if (!DateTime.TryParse(vals[2], out _)) return "日期格式為 YYYY-MM-DD";
            // 放在錢包的目標:多存入的部分不能超過錢包可分配的錢
            double more = (vals[0] == "存入" ? a : -a) - d.Amount;
            if (g?.WalletId is long w && more > 0.0001 && more > WalletFree(w) + 0.0001)
                return $"「{g.WalletName}」只剩 {Money(Math.Max(WalletFree(w), 0))} 可以分配";
            return null;
        }, "儲存");
        if (v == null) return;
        TryAmount(v[1], out var amt);
        db.UpdateDeposit(d.Id, NormalizeDate(v[2]), v[0] == "存入" ? amt : -amt, v[3]);
        NotifyChanged();
    }

    public static async Task DeleteDeposit(DepositRow d)
    {
        if (!await Dialogs.Confirm("刪除紀錄", $"刪除 {d.Date} 的這筆{(d.IsDeposit ? "存入" : "取出")}({d.AmountText})?", "刪除", danger: true))
            return;
        db.DeleteDeposit(d.Id);
        NotifyChanged();
    }
}
