using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Progressing.Core;

namespace Progressing.ViewModels;

/// <summary>布尔取反转换（重命名时切换 文本/输入框）。</summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>HEX 色值 → 画刷。</summary>
public class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string hex ? new SolidColorBrush(Palettes.FromHex(hex)) : Brushes.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
