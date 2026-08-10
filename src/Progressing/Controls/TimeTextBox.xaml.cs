using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Progressing.Controls;

/// <summary>
/// hh:mm 时间输入：仅允许数字与冒号，输入满 4 位数字时自动插入冒号（"1430" → "14:30"）。
/// 支持任意位置插入 / 选中一段替换 / 全选重输（不再重排整串数字、不再强制补 0）；
/// 失焦时补全（"14" → "14:00"，"930" → "9:30"，越界收敛 0~23:59）。
/// 非法时 Time 置 null 并标红。
/// </summary>
public partial class TimeTextBox : UserControl
{
    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(
        nameof(Time),
        typeof(TimeSpan?),
        typeof(TimeTextBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTimePropertyChanged));

    /// <summary>当前时间值；非法输入时为 null。</summary>
    public TimeSpan? Time
    {
        get => (TimeSpan?)GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    /// <summary>时间变化事件（手动输入或属性赋值均触发）。</summary>
    public event EventHandler? TimeChanged;

    private bool _syncing;

    public TimeTextBox()
    {
        InitializeComponent();
        InputBox.TextChanged += OnTextChanged;
        InputBox.LostFocus += (_, _) => NormalizeAndCommit();
    }

    private static void OnTimePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (TimeTextBox)d;
        box.SyncFromTime();
        box.TimeChanged?.Invoke(box, EventArgs.Empty);
    }

    private void SyncFromTime()
    {
        if (_syncing)
            return;

        _syncing = true;
        if (Time is { } t)
            InputBox.Text = t.ToString(@"hh\:mm");
        else if (!InputBox.IsFocused)
            InputBox.Text = "";
        _syncing = false;
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing)
            return;

        _syncing = true;
        try
        {
            var text = InputBox.Text;
            var caret = InputBox.CaretIndex;

            // 仅保留数字与冒号，记录光标前的有效字符数；最长 5 个字符（HH:MM），超出从右端丢弃
            var kept = new List<char>(5);
            var validBeforeCaret = 0;
            for (var i = 0; i < text.Length && kept.Count < 5; i++)
            {
                var ch = text[i];
                if (ch != ':' && !char.IsAsciiDigit(ch))
                    continue;
                if (i < caret)
                    validBeforeCaret++;
                kept.Add(ch);
            }

            var cleaned = new string(kept.ToArray());

            // 恰好 4 位纯数字且无冒号 → 自动补冒号（输入新时间 / 整选替换时的便利）
            var formatted = cleaned;
            if (cleaned.Length == 4 && !cleaned.Contains(':') && cleaned.All(char.IsAsciiDigit))
            {
                formatted = $"{cleaned[..2]}:{cleaned[2..]}";
                if (validBeforeCaret >= 2)
                    validBeforeCaret++; // 冒号插在光标前，光标后移一位
            }

            if (formatted != text)
                InputBox.Text = formatted;

            InputBox.CaretIndex = Math.Clamp(validBeforeCaret, 0, formatted.Length);

            var valid = TimeSpan.TryParse(formatted, CultureInfo.InvariantCulture, out var parsed)
                        && parsed < TimeSpan.FromDays(1);
            InputBox.BorderBrush = valid ? new SolidColorBrush(Colors.Gray) : new SolidColorBrush(Colors.IndianRed);
            Time = valid ? parsed : null;
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>失焦补全：空 → 清空；1~2 位 → HH:00；3 位 → H:MM；4 位 → HH:MM；越界收敛到 0~23:59。</summary>
    private void NormalizeAndCommit()
    {
        if (_syncing)
            return;

        _syncing = true;
        try
        {
            var digits = new string(InputBox.Text.Where(char.IsAsciiDigit).Take(4).ToArray());

            string normalized = digits.Length switch
            {
                0 => "",
                1 or 2 => $"{ClampInt(digits, 0, 23):D2}:00",
                3 => $"{ClampInt(digits[..1], 0, 23)}:{ClampInt(digits[1..], 0, 59):D2}",
                _ => $"{ClampInt(digits[..2], 0, 23):D2}:{ClampInt(digits[2..], 0, 59):D2}",
            };

            if (InputBox.Text != normalized)
                InputBox.Text = normalized;

            var parsed = default(TimeSpan);
            var valid = normalized.Length > 0
                        && TimeSpan.TryParse(normalized, CultureInfo.InvariantCulture, out parsed)
                        && parsed < TimeSpan.FromDays(1);
            InputBox.BorderBrush = valid ? new SolidColorBrush(Colors.Gray) : new SolidColorBrush(Colors.IndianRed);
            Time = valid ? parsed : null;
        }
        finally
        {
            _syncing = false;
        }
    }

    private static int ClampInt(string s, int min, int max)
        => int.TryParse(s, out var v) ? Math.Clamp(v, min, max) : min;
}
