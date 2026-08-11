using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Progressing.Controls;

/// <summary>
/// 描边文字：在字形边缘绘制轮廓线（文字描边效果），描边紧贴字形、无空隙，
/// 而非矩形框。用 FormattedText.BuildGeometry 两次绘制：
/// 先以描边色填充并描边形成轮廓，再以文字色填充覆盖内部。
/// </summary>
public class StrokedText : FrameworkElement
{
    private static readonly Typeface DefaultTypeface =
        new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(StrokedText),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(
        nameof(FontSize), typeof(double), typeof(StrokedText),
        new FrameworkPropertyMetadata(26.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(StrokedText),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(StrokedText),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(StrokedText),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>文字填充色。</summary>
    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>描边色。</summary>
    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>描边粗细（DIP）。</summary>
    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var formatted = BuildText();
        return new Size(Math.Ceiling(formatted.Width), Math.Ceiling(formatted.Height));
    }

    protected override void OnRender(DrawingContext dc)
    {
        var text = Text;
        if (string.IsNullOrEmpty(text))
            return;

        var formatted = BuildText();
        if (formatted.Width <= 0 || formatted.Height <= 0)
            return;

        var fill = Foreground ?? Brushes.Black;
        var stroke = Stroke ?? Brushes.Black;
        var geometry = formatted.BuildGeometry(new Point(0, 0));

        // 第一遍：描边色填充 + 描边，形成紧贴字形的轮廓（无空隙）
        if (StrokeThickness > 0)
        {
            var pen = new Pen(stroke, StrokeThickness);
            pen.Freeze();
            dc.DrawGeometry(stroke, pen, geometry);
        }

        // 第二遍：文字色填充覆盖内部
        dc.DrawGeometry(fill, null, geometry);
    }

    private FormattedText BuildText()
        => new(
            Text ?? "",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            DefaultTypeface,
            FontSize,
            Foreground ?? Brushes.Black,
            1.0);
}
