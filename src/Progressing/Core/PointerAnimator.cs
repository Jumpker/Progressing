using System.Windows.Media.Animation;
using System.Windows.Media;

namespace Progressing.Core;

/// <summary>
/// 指针补间动画：以 1s 线性 DoubleAnimation 追赶目标位置，形成"永远在途中"的平滑连续移动。
/// 24 小时刻度下每秒位移远小于 0.1px（默认 600px 全长约 0.007px/s），低于阈值时直接落到目标、
/// 不创建动画时钟（开销同样近零），保证指针持续跟随当前时刻而不冻结。
/// 只驱动 TranslateTransform，不触发布局。
/// </summary>
public sealed class PointerAnimator
{
    private const double SkipThreshold = 0.1;
    private static readonly TimeSpan TickDuration = TimeSpan.FromSeconds(1);

    private readonly TranslateTransform _transform;
    private bool _isXAxis;

    /// <summary>当前动画位置（DIP）。</summary>
    public double Current { get; private set; }

    /// <param name="transform">指针元素的 TranslateTransform。</param>
    /// <param name="isXAxis">true 沿 X 轴移动（横放），false 沿 Y 轴（竖放）。</param>
    public PointerAnimator(TranslateTransform transform, bool isXAxis)
    {
        _transform = transform;
        _isXAxis = isXAxis;
    }

    /// <summary>
    /// 切换补间轴（横放 ↔ 竖放旋转时调用）：
    /// 清空两轴动画并复位偏移，新轴上的目标位置由随后的 JumpTo / AnimateTo 设定。
    /// </summary>
    public void SetAxis(bool isXAxis)
    {
        if (_isXAxis == isXAxis)
            return;

        _isXAxis = isXAxis;
        _transform.BeginAnimation(TranslateTransform.XProperty, null);
        _transform.BeginAnimation(TranslateTransform.YProperty, null);
        _transform.X = 0;
        _transform.Y = 0;
        Current = 0;
    }

    /// <summary>
    /// 将指针补间到目标位置。
    /// 24 小时刻度下每秒位移仅约 0.007px（默认 600px 全长），远小于 0.1px 阈值：
    /// 若低于阈值时只更新内部数值而不落位，指针会永久停在原地（只有改设置触发 JumpTo 才会动）。
    /// 因此低于阈值时改为直接落到目标，保证指针每秒持续跟随当前时刻；
    /// 大于阈值（如休眠唤醒后追赶）时仍走 1s 线性动画，形成平滑滑动。
    /// </summary>
    public void AnimateTo(double target)
    {
        if (Math.Abs(target - Current) < SkipThreshold)
        {
            // 低于阈值也直接落位（无动画时钟），避免指针冻结在旧位置
            Current = target;
            ApplyPosition(target);
            return;
        }

        var animation = new DoubleAnimation(Current, target, TickDuration)
        {
            EasingFunction = null, // 线性
            FillBehavior = FillBehavior.HoldEnd,
        };
        Current = target;

        if (_isXAxis)
            _transform.BeginAnimation(TranslateTransform.XProperty, animation);
        else
            _transform.BeginAnimation(TranslateTransform.YProperty, animation);
    }

    /// <summary>瞬时跳转（每日重置、方向切换等），不播动画。</summary>
    public void JumpTo(double pos)
    {
        Current = pos;
        ApplyPosition(pos);
    }

    /// <summary>直接设置变换位置并清除该轴上残留动画。</summary>
    private void ApplyPosition(double pos)
    {
        if (_isXAxis)
        {
            _transform.BeginAnimation(TranslateTransform.XProperty, null);
            _transform.X = pos;
        }
        else
        {
            _transform.BeginAnimation(TranslateTransform.YProperty, null);
            _transform.Y = pos;
        }
    }
}
