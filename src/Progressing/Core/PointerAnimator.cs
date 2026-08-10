using System.Windows.Media.Animation;
using System.Windows.Media;

namespace Progressing.Core;

/// <summary>
/// 指针补间动画：以 1s 线性 DoubleAnimation 追赶目标位置，形成"永远在途中"的平滑连续移动。
/// 目标与当前位置差异 &lt; 0.1px 时跳过动画（零开销）。只驱动 TranslateTransform，不触发布局。
/// </summary>
public sealed class PointerAnimator
{
    private const double SkipThreshold = 0.1;
    private static readonly TimeSpan TickDuration = TimeSpan.FromSeconds(1);

    private readonly TranslateTransform _transform;
    private readonly bool _isXAxis;

    /// <summary>当前动画位置（DIP）。</summary>
    public double Current { get; private set; }

    /// <param name="transform">指针元素的 TranslateTransform。</param>
    /// <param name="isXAxis">true 沿 X 轴移动（横放），false 沿 Y 轴（竖放）。</param>
    public PointerAnimator(TranslateTransform transform, bool isXAxis)
    {
        _transform = transform;
        _isXAxis = isXAxis;
    }

    /// <summary>将指针补间到目标位置（1s 线性动画）。</summary>
    public void AnimateTo(double target)
    {
        if (Math.Abs(target - Current) < SkipThreshold)
        {
            Current = target;
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

        Current = pos;
    }
}
