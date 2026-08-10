using Progressing.Models;

namespace Progressing.Services;

/// <summary>
/// 时间段重叠校验（产品设计书 §3.3.1）：
/// 两个时间段 [s1,e1] 与 [s2,e2] 重叠 ⇔ s1 &lt; e2 且 s2 &lt; e1；仅首尾相接（端点相同）不算重叠。
/// 跨午夜（start &gt;= end）非法，直接拒绝。
/// </summary>
public static class OverlapValidator
{
    /// <summary>两个时间段是否重叠。</summary>
    public static bool IsOverlap(TimeSpan s1, TimeSpan e1, TimeSpan s2, TimeSpan e2)
        => s1 < e2 && s2 < e1;

    /// <summary>两条备注是否重叠。</summary>
    public static bool IsOverlap(SegmentNote a, SegmentNote b)
        => IsOverlap(a.StartTime, a.EndTime, b.StartTime, b.EndTime);

    /// <summary>时间段是否合法（start &lt; end，不跨午夜）。</summary>
    public static bool IsValid(TimeSpan start, TimeSpan end) => start < end;

    /// <summary>找出与候选备注冲突的全部已有备注（排除候选自身，按 start 排序返回）。</summary>
    public static List<SegmentNote> FindConflicts(IEnumerable<SegmentNote> notes, SegmentNote candidate)
        => notes
           .Where(n => !string.Equals(n.Id, candidate.Id, StringComparison.Ordinal) && IsOverlap(n, candidate))
           .OrderBy(n => n.StartTime)
           .ToList();

    /// <summary>找出全部处于冲突中的备注（至少与一条其它备注重叠，按 start 排序返回）。</summary>
    public static List<SegmentNote> FindConflicts(IEnumerable<SegmentNote> notes)
    {
        var list = notes.ToList();
        return list
               .Where(a => list.Any(b => !string.Equals(a.Id, b.Id, StringComparison.Ordinal) && IsOverlap(a, b)))
               .OrderBy(n => n.StartTime)
               .ToList();
    }
}
