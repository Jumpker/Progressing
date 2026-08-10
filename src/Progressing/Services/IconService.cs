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
    /// 程序化绘制内置指针：64×64 透明底、实心水滴、尖端朝上（居中缩放适配画布）。
    /// 不依赖 SVG 资源文件与 Svg.Skia 解析，避免资源打包 / 解析失败导致指针空白。
    /// </summary>
    private static BitmapImage RenderBuiltinPointer()
    {
        using var bitmap = new SKBitmap(RasterSize, RasterSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var builder = new SKPathBuilder();
        builder.MoveTo(16, 4);                                  // 尖端
        builder.CubicTo(21, 10, 26, 14, 26, 19);                // 右上曲线
        builder.ArcTo(10, 10, 0, SKPathArcSize.Small, SKPathDirection.Clockwise, 6, 19); // 底部圆弧
        builder.CubicTo(6, 14, 11, 10, 16, 4);                  // 左上曲线回到尖端
        builder.Close();
        using var path = builder.Detach();

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColor.Parse("#2D82BC"),
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
