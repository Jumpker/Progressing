using Progressing.Core;

namespace Progressing.Services;

/// <summary>
/// 色池取色服务（每实例独立）。规则（产品设计书 §3.4）：
/// - 随机取色：色池未用完前不允许复用已用颜色；用尽后允许全池随机复用；
/// - 手动选色 / 自定义颜色不进入占用统计，不占配额；
/// - 删除备注时，仅当该备注为随机取色时将其颜色回归未用池。
/// </summary>
public class ColorPoolService
{
    private readonly List<int> _used;

    /// <summary>内置色池颜色数。</summary>
    public int PoolSize => Palettes.Pool.Count;

    /// <summary>当前占用数。</summary>
    public int UsedCount => _used.Count;

    /// <param name="used">占用记录（直接引用 BarConfig.ColorPoolUsed，随配置持久化）。</param>
    public ColorPoolService(List<int> used)
    {
        _used = used;
    }

    /// <summary>随机取一个色池索引：池未用完前从"未用色"中取；用尽后全池随机。</summary>
    public int PickRandomIndex()
    {
        if (_used.Count < Palettes.Pool.Count)
        {
            var free = Enumerable.Range(0, Palettes.Pool.Count).Where(i => !_used.Contains(i)).ToList();
            var idx = free[Random.Shared.Next(free.Count)];
            _used.Add(idx);
            return idx;
        }

        return Random.Shared.Next(Palettes.Pool.Count);
    }

    /// <summary>释放一个随机取用的颜色（仅当备注 assignedBy==Random 时调用）。</summary>
    public bool Release(int poolIndex) => _used.Remove(poolIndex);
}
