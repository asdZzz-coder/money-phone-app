namespace ZZZ.Mobile;

/// <summary>一個支出分類的統計:圓環圖的一塊,也是下方列表的一列。</summary>
public record CategoryStat(string Name, double Amount, int Count, double Share, Color Color)
{
    public string AmountText => Amount.ToString("N0");
    public string ShareText => $"{Share:P1}";
}

/// <summary>
/// 圖表專用的低飽和色(與電腦版相同)。介面其他地方的顏色只用來表示意義(綠收入、紅支出、橘逾期),
/// 分類需要分得出彼此才在這裡另外配色,並避開綠、紅、橘。
/// </summary>
public static class ChartPalette
{
    static readonly Color[] Colors = new[]
    {
        "#4E79A7", "#D4A373", "#8E7DBE", "#5E9EA0", "#C38D9E",
        "#B8A04A", "#7D8CA3", "#A0785A", "#6C8EBF", "#B39CD0",
    }.Select(Color.FromArgb).ToArray();

    static readonly Color Other = Color.FromArgb("#B8BEC7");

    /// <summary>依分類在「管理分類」中的順序配色,換月份顏色也不會變;「其他」固定灰色。</summary>
    public static Color For(string category, IReadOnlyList<string> categories)
    {
        if (category == "其他") return Other;
        int i = categories.ToList().IndexOf(category);
        if (i < 0) i = categories.Count + category.Sum(c => c); // 已刪除的分類:用名稱算出固定的顏色
        return Colors[i % Colors.Length];
    }
}

/// <summary>支出分類圓環圖。中間顯示總額,點某一塊改顯示該分類。</summary>
public class DonutDrawable : IDrawable
{
    IReadOnlyList<CategoryStat> slices = [];
    (string Title, string Value, string Sub) total = ("", "", "");
    public string? Highlighted { get; set; }

    public void SetData(IReadOnlyList<CategoryStat> data, string title, string value, string sub)
    {
        slices = data;
        total = (title, value, sub);
        if (Highlighted != null && data.All(s => s.Name != Highlighted)) Highlighted = null;
    }

    const float Grow = 1.06f;

    static (PointF C, float Outer, float Inner) Geometry(RectF r)
    {
        float size = Math.Min(r.Width, r.Height);
        float outer = size / 2 / Grow - 1;
        return (new PointF(r.Center.X, r.Center.Y), outer, outer * 0.64f);
    }

    public void Draw(ICanvas canvas, RectF rect)
    {
        var (c, outer, inner) = Geometry(rect);
        if (outer < 20) return;
        double sum = slices.Sum(s => s.Amount);
        if (sum <= 0)
        {
            canvas.FillColor = Theme.Ghost.Now;
            canvas.FillPath(Ring(c, outer, inner, 0, 360));
        }
        else
        {
            double start = -90; // 從正上方順時針畫
            foreach (var s in slices)
            {
                double sweep = s.Amount / sum * 360;
                bool hi = s.Name == Highlighted;
                float k = hi ? Grow : 1;
                canvas.FillColor = Highlighted == null || hi ? s.Color : s.Color.WithAlpha(0.35f);
                canvas.FillPath(Ring(c, outer * k, inner, start, sweep));
                if (slices.Count > 1) // 與卡片同色的細縫隔開每一塊
                {
                    canvas.StrokeColor = Theme.Surface.Now;
                    canvas.StrokeSize = 2;
                    var a = start * Math.PI / 180;
                    canvas.DrawLine(c.X + (float)(inner * Math.Cos(a)), c.Y + (float)(inner * Math.Sin(a)),
                                    c.X + (float)(outer * Grow * Math.Cos(a)), c.Y + (float)(outer * Grow * Math.Sin(a)));
                }
                start += sweep;
            }
        }
        var sel = Highlighted == null ? null : slices.FirstOrDefault(x => x.Name == Highlighted);
        var (t, v, sub) = sel == null ? total : (sel.Name, sel.AmountText, sel.ShareText);
        float w = inner * 1.7f;
        canvas.FontColor = Theme.Muted.Now;
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.FontSize = 12;
        canvas.DrawString(t, c.X - w / 2, c.Y - 34, w, 18, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.FontColor = Theme.Text.Now;
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.FontSize = 21;
        canvas.DrawString(v, c.X - w / 2, c.Y - 14, w, 28, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.FontColor = Theme.Muted.Now;
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.FontSize = 12;
        canvas.DrawString(sub, c.X - w / 2, c.Y + 16, w, 18, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    /// <summary>點到的那一塊;點在圓環外或中間回傳 null。</summary>
    public string? HitTest(PointF p, RectF rect)
    {
        var (c, outer, inner) = Geometry(rect);
        double dx = p.X - c.X, dy = p.Y - c.Y, dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < inner || dist > outer * Grow) return null;
        double sum = slices.Sum(s => s.Amount);
        if (sum <= 0) return null;
        double angle = Math.Atan2(dy, dx) * 180 / Math.PI + 90; // 0 = 正上方,順時針
        if (angle < 0) angle += 360;
        double start = 0;
        foreach (var s in slices)
        {
            double sweep = s.Amount / sum * 360;
            if (angle >= start && angle < start + sweep) return s.Name;
            start += sweep;
        }
        return slices.LastOrDefault()?.Name;
    }

    /// <summary>圓環的一段(角度單位為度,0 為三點鐘方向,順時針)。用多邊形逼近弧線。</summary>
    static PathF Ring(PointF c, float outer, float inner, double startDeg, double sweepDeg)
    {
        var path = new PathF();
        int n = Math.Max(2, (int)Math.Ceiling(sweepDeg / 2));
        PointF At(float r, double deg) => new(c.X + r * (float)Math.Cos(deg * Math.PI / 180), c.Y + r * (float)Math.Sin(deg * Math.PI / 180));
        path.MoveTo(At(outer, startDeg));
        for (int i = 1; i <= n; i++) path.LineTo(At(outer, startDeg + sweepDeg * i / n));
        for (int i = n; i >= 0; i--) path.LineTo(At(inner, startDeg + sweepDeg * i / n));
        path.Close();
        return path;
    }
}

/// <summary>每月收支長條圖:綠色收入、紅色支出,折線為結餘。點某個月會切換到那個月。</summary>
public class MonthBarsDrawable : IDrawable
{
    IReadOnlyList<MonthTotal> data = [];
    string? selected;
    const float Left = 40, Top = 8, Bottom = 34;

    public void SetData(IReadOnlyList<MonthTotal> months, string? selectedMonth)
    {
        data = months;
        selected = selectedMonth;
    }

    public void Draw(ICanvas canvas, RectF rect)
    {
        float w = rect.Width, h = rect.Height;
        if (w < 80 || h < 80 || data.Count == 0) return;
        double max = data.Max(m => Math.Max(Math.Max(m.Income, m.Expense), m.Net));
        double min = Math.Min(0, data.Min(m => m.Net));
        if (max <= 0 && min >= 0) max = 1000;
        double step = NiceStep((max - min) / 4);
        double hi = Math.Ceiling(max / step) * step, lo = Math.Floor(min / step) * step;
        float plotH = h - Top - Bottom, plotW = w - Left;
        float Y(double v) => (float)(Top + (hi - v) / (hi - lo) * plotH);

        float colW = plotW / data.Count, barW = Math.Clamp(colW * 0.28f, 3, 14), zero = Y(0);
        // 選到的月份底色
        for (int i = 0; i < data.Count; i++)
            if (data[i].Month == selected)
            {
                canvas.FillColor = Theme.Sel.Now;
                canvas.FillRoundedRectangle(Left + i * colW, 0, colW, h, 6);
            }

        // 格線與刻度
        canvas.FontSize = 10;
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        for (int k = 0; k <= Math.Round((hi - lo) / step); k++)
        {
            double v = lo + k * step;
            float y = Y(v);
            canvas.StrokeColor = Math.Abs(v) < step / 2 ? Theme.LineStrong.Now : Theme.Line.Now;
            canvas.StrokeSize = 1;
            canvas.DrawLine(Left, y, w, y);
            canvas.FontColor = Theme.Subtle.Now;
            canvas.DrawString(Axis(v), 0, y - 8, Left - 6, 16, HorizontalAlignment.Right, VerticalAlignment.Center);
        }

        var points = new List<PointF>();
        for (int i = 0; i < data.Count; i++)
        {
            var m = data[i];
            float x0 = Left + i * colW, cx = x0 + colW / 2;
            bool isSel = m.Month == selected;
            var month = DateTime.ParseExact(m.Month, "yyyy-MM", null);
            Bar(cx - barW - 1, m.Income, Theme.Inc.Now);
            Bar(cx + 1, m.Expense, Theme.Exp.Now);
            canvas.FontColor = isSel ? Theme.Text.Now : Theme.Subtle.Now;
            canvas.Font = isSel ? Microsoft.Maui.Graphics.Font.DefaultBold : Microsoft.Maui.Graphics.Font.Default;
            canvas.FontSize = 10;
            canvas.DrawString($"{month.Month}月", x0, h - Bottom + 4, colW, 14, HorizontalAlignment.Center, VerticalAlignment.Center);
            if (i == 0 || month.Month == 1)
                canvas.DrawString(month.Year.ToString(), x0 - 6, h - Bottom + 18, colW + 12, 14, HorizontalAlignment.Center, VerticalAlignment.Center);
            points.Add(new PointF(cx, Y(m.Net)));

            void Bar(float x, double v, Color fill)
            {
                if (v <= 0) return;
                float height = Math.Max(zero - Y(v), 2);
                canvas.FillColor = fill;
                canvas.FillRoundedRectangle(x, zero - height, barW, height, Math.Min(3, barW / 2));
            }
        }
        // 結餘折線
        canvas.StrokeColor = Theme.Ink.Now;
        canvas.StrokeSize = 1.6f;
        canvas.StrokeLineJoin = LineJoin.Round;
        for (int i = 1; i < points.Count; i++) canvas.DrawLine(points[i - 1], points[i]);
        foreach (var p in points)
        {
            canvas.FillColor = Theme.Surface.Now;
            canvas.FillCircle(p, 4.5f);
            canvas.FillColor = Theme.Ink.Now;
            canvas.FillCircle(p, 3f);
        }
    }

    /// <summary>點到的月份(yyyy-MM)。</summary>
    public string? HitTest(PointF p, RectF rect)
    {
        if (data.Count == 0 || p.X < Left) return null;
        float colW = (rect.Width - Left) / data.Count;
        int i = (int)((p.X - Left) / colW);
        return i >= 0 && i < data.Count ? data[i].Month : null;
    }

    /// <summary>把刻度間距取成 1、2、5 × 10 的次方。</summary>
    static double NiceStep(double raw)
    {
        if (raw <= 0) return 1;
        double mag = Math.Pow(10, Math.Floor(Math.Log10(raw))), n = raw / mag;
        return (n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10) * mag;
    }

    static string Axis(double v) => Math.Abs(v) >= 10000 ? $"{v / 10000:0.#}萬" : v.ToString("N0");
}
