using System.IO;
using System.Windows.Media.Imaging;
using SkiaSharp;
using Svg.Skia;

namespace Progressing.Services;

/// <summary>
/// 指针图标加载：内置指针程序化绘制（SkiaSharp 实心水滴，缓存）+ 自定义 PNG / SVG 文件栅格化。
/// 自定义图片始终保持原始方向显示（不旋转、不随镜像翻转）。
/// </summary>
public static class IconService
{
    private const int RasterSize = 64;

    private static BitmapImage? _builtin;

    /// <summary>加载内置指针（程序化绘制的实心水滴，缓存）。</summary>
    public static BitmapImage LoadBuiltinPointer() => _builtin ??= RenderBuiltinPointer();

    /// <summary>
    /// 程序化绘制内置指针：64×64 透明底、实心导航箭头（尖端朝上、尾部带凹口），居中缩放适配画布。
    /// 方向由 BarWindow 按"时间增长方向"旋转（横放向右 / 竖放向下，镜像反向）。
    /// </summary>
    private static BitmapImage RenderBuiltinPointer()
    {
        using var bitmap = new SKBitmap(RasterSize, RasterSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var builder = new SKPathBuilder();
        builder.MoveTo(32, 5);   // 尖端
        builder.LineTo(52, 50);  // 右下角（右尾）
        builder.LineTo(32, 44);  // 尾部缺口中心（V 形凹口）
        builder.LineTo(12, 50);  // 左下角（左尾）
        builder.Close();         // 闭合回到尖端，构成左缘
        using var path = builder.Detach();

        // 将箭头包围盒居中到画布中心：保证图形中心 = 位图中心，
        // 指针 Image 放大 / 缩小时图形始终以中心为基准缩放，不会左右漂移
        // （与 RasterizeSvg 自定义指针的居中逻辑保持一致）。
        var bounds = path.Bounds;
        canvas.Translate(
            (RasterSize - bounds.Width) / 2 - bounds.Left,
            (RasterSize - bounds.Height) / 2 - bounds.Top);

        // 先画白色描边：指针走在上色时间段上（如蓝色段）也保持辨识度
        using var outlinePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4,
            StrokeJoin = SKStrokeJoin.Round,
            Color = SKColors.White,
            IsAntialias = true,
        };
        canvas.DrawPath(path, outlinePaint);

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColor.Parse("#1677FF"), // 品牌现代蓝，与 Theme.xaml AccentBrush 同族
            IsAntialias = true,
        };
        canvas.DrawPath(path, paint);
        canvas.Flush();

        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var ms = new MemoryStream(data.ToArray());

        var result = new BitmapImage();
        result.BeginInit();
        result.CacheOption = BitmapCacheOption.OnLoad;
        result.StreamSource = ms;
        result.EndInit();
        result.Freeze();
        return result;
    }

    /// <summary>加载自定义指针文件；不存在的路径返回 null。</summary>
    public static BitmapImage? LoadFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                using var fs = File.OpenRead(path);
                return RasterizeSvg(fs);
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(Path.GetFullPath(path));
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage RasterizeSvg(Stream stream)
    {
        // Svg.Skia 5.x：SKSvg 解析 → SKPicture → 透明底 64×64 栅格化 PNG
        var svg = new SKSvg();
        svg.Load(stream);

        using var bitmap = new SKBitmap(RasterSize, RasterSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var picture = svg.Picture;
        if (picture is not null)
        {
            // 图形按比例缩放适配到整个画布并居中：保证图形中心 = 位图中心，
            // 指针 Image 放大 / 缩小时图形始终以中心为基准缩放，不会左右漂移。
            var bounds = picture.CullRect;
            if (!bounds.IsEmpty)
            {
                var scale = Math.Min(RasterSize / bounds.Width, RasterSize / bounds.Height);
                var scaledW = bounds.Width * scale;
                var scaledH = bounds.Height * scale;
                canvas.Translate(
                    (RasterSize - scaledW) / 2 - bounds.Left * scale,
                    (RasterSize - scaledH) / 2 - bounds.Top * scale);
                canvas.Scale(scale);
            }
            canvas.DrawPicture(picture);
        }

        canvas.Flush();

        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var ms = new MemoryStream(data.ToArray());

        var result = new BitmapImage();
        result.BeginInit();
        result.CacheOption = BitmapCacheOption.OnLoad;
        result.StreamSource = ms;
        result.EndInit();
        result.Freeze();
        return result;
    }
}
