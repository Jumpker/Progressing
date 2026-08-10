namespace Progressing.Services;

/// <summary>
/// 当前时刻源 + 跨日检测（每日重置）。所有实例共享一个实例。
/// </summary>
public class TimeService
{
    private DateTime _lastDate;

    public TimeService()
    {
        _lastDate = DateTime.Now.Date;
    }

    /// <summary>当前时刻。</summary>
    public DateTime Now => DateTime.Now;

    /// <summary>今日 0 点起经过的时长（0:00 → 24:00）。</summary>
    public TimeSpan TimeOfDay => Now.TimeOfDay;

    /// <summary>
    /// 检测是否跨日：若与缓存日期不同则更新缓存并返回 true（调用方执行每日重置）。
    /// </summary>
    public bool ConsumeDayChanged()
    {
        var today = Now.Date;
        if (today != _lastDate)
        {
            _lastDate = today;
            return true;
        }

        return false;
    }

    /// <summary>强制重置缓存日期（应用启动时调用，避免把开机前的日期误判为跨日）。</summary>
    public void Reset()
    {
        _lastDate = Now.Date;
    }
}
