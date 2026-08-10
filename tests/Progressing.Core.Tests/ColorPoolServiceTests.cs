using Progressing.Core;
using Progressing.Services;

namespace Progressing.Core.Tests;

/// <summary>随机取色：池未用尽不重复；用尽后允许复用；手动/自定义不占配额；删除回归。</summary>
public class ColorPoolServiceTests
{
    private static ColorPoolService New(out List<int> used)
    {
        used = new List<int>();
        return new ColorPoolService(used);
    }

    [Fact]
    public void PickRandom_NoRepeatBeforePoolExhausted()
    {
        var pool = New(out var used);
        var picks = new HashSet<int>();
        for (var i = 0; i < pool.PoolSize; i++)
            picks.Add(pool.PickRandomIndex());

        Assert.Equal(pool.PoolSize, picks.Count); // 全部互不相同
        Assert.Equal(pool.PoolSize, pool.UsedCount);
    }

    [Fact]
    public void PickRandom_AllowsReuseAfterPoolExhausted()
    {
        var pool = New(out var used);
        for (var i = 0; i < pool.PoolSize; i++)
            pool.PickRandomIndex();

        // 池已用尽：第 11 次仍可返回（不抛异常），但占用数不再增长
        pool.PickRandomIndex();
        Assert.Equal(pool.PoolSize, pool.UsedCount);
    }

    [Fact]
    public void Release_ReturnsColorToFreePool()
    {
        var pool = New(out var used);
        var first = pool.PickRandomIndex();

        Assert.Equal(1, pool.UsedCount);
        Assert.True(pool.Release(first));
        Assert.Equal(0, pool.UsedCount);
        Assert.False(pool.Release(999)); // 未占用索引释放失败
        Assert.False(pool.Release(first)); // 重复释放失败
        Assert.DoesNotContain(first, used);
    }

    [Fact]
    public void ManualOrCustom_DoesNotOccupyQuota()
    {
        // 手动选色直接改 SegmentColor（AssignedBy=Manual）不进 ColorPoolUsed；
        // 删除时仅随机取用才 Release——由上层判定，此处验证 Release 语义。
        var pool = New(out var used);
        var idx = pool.PickRandomIndex();
        Assert.True(pool.Release(idx));
        Assert.Equal(0, pool.UsedCount);
    }
}
