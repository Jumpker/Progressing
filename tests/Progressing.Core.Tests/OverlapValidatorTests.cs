using Progressing.Models;
using Progressing.Services;

namespace Progressing.Core.Tests;

/// <summary>重叠判定：s1&lt;e2 且 s2&lt;e1 即重叠；首尾相接允许；跨午夜（start&gt;=end）拒绝。</summary>
public class OverlapValidatorTests
{
    private static SegmentNote Note(string start, string end, string id)
        => new() { Id = id, Start = start, End = end };

    [Theory]
    [InlineData("09:00", "11:00", "10:00", "12:00", true)]   // 普通交叉
    [InlineData("10:00", "12:00", "09:00", "11:00", true)]   // 反向交叉
    [InlineData("09:00", "11:00", "11:00", "13:00", false)]  // 首尾相接：允许
    [InlineData("11:00", "13:00", "09:00", "11:00", false)]  // 反向首尾相接：允许
    [InlineData("09:00", "10:00", "10:30", "11:00", false)]  // 完全分离
    [InlineData("09:00", "12:00", "10:00", "11:00", true)]   // 包含
    public void IsOverlap_RuleMatchesDesign(
        string s1, string e1, string s2, string e2, bool expected)
    {
        var a = Note(s1, e1, "a");
        var b = Note(s2, e2, "b");
        Assert.Equal(expected, OverlapValidator.IsOverlap(a, b));
        Assert.Equal(expected, OverlapValidator.IsOverlap(a.StartTime, a.EndTime, b.StartTime, b.EndTime));
    }

    [Theory]
    [InlineData("09:00", "10:00", true)]
    [InlineData("10:00", "10:00", false)]  // 零时长
    [InlineData("11:00", "10:00", false)]  // 跨午夜
    public void IsValid_RejectsCrossMidnight(string start, string end, bool expected)
    {
        var note = Note(start, end, "x");
        Assert.Equal(expected, OverlapValidator.IsValid(note.StartTime, note.EndTime));
    }

    [Fact]
    public void FindConflicts_ExcludesSelfAndSorted()
    {
        var self = Note("10:00", "12:00", "self");
        var later = Note("11:00", "13:00", "later");  // 与 self 冲突
        var earlier = Note("09:00", "10:30", "earlier"); // 与 self 冲突
        var far = Note("20:00", "21:00", "far");       // 不冲突
        var notes = new[] { far, later, self, earlier };

        var conflicts = OverlapValidator.FindConflicts(notes, self);

        Assert.DoesNotContain(conflicts, c => c.Id == "self");
        Assert.Equal(2, conflicts.Count);
        Assert.Equal(new[] { "earlier", "later" }, conflicts.Select(c => c.Id).ToArray());
    }
}
