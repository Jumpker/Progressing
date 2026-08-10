namespace Progressing.Core;

/// <summary>
/// 时间 ↔ 像素映射。核心不变量：进度条全长恒等于 24 小时（0:00 ~ 24:00）。
/// 镜像只影响归一化系数：镜像后 0:00 在最右 / 最下，时间反向递增。
/// </summary>
public static class TimeMapper
{
    /// <summary>一天的分钟数（24 * 60）。</summary>
    public const double MinutesPerDay = 1440.0;

    /// <summary>归一化：0:00 → 0.0，24:00 → 1.0。</summary>
    public static double Normalize(TimeSpan t)
        => Math.Clamp(t.TotalMinutes / MinutesPerDay, 0.0, 1.0);

    /// <summary>归一化并考虑镜像：非镜像同 Normalize；镜像取 1 - n。</summary>
    public static double NormalizeMirrored(TimeSpan t, bool mirrored)
    {
        var n = Normalize(t);
        return mirrored ? 1.0 - n : n;
    }

    /// <summary>时刻 → 进度条上的像素位置（横放为 X，竖放为 Y，均为 DIP）。</summary>
    public static double Map(TimeSpan t, double length, bool mirrored)
        => NormalizeMirrored(t, mirrored) * length;

    /// <summary>像素位置 → 时刻（Map 的逆运算；用于编辑模式交互等）。</summary>
    public static TimeSpan Unmap(double pos, double length, bool mirrored)
    {
        var n = length <= 0 ? 0.0 : Math.Clamp(pos / length, 0.0, 1.0);
        if (mirrored) n = 1.0 - n;
        return TimeSpan.FromMinutes(n * MinutesPerDay);
    }
}
